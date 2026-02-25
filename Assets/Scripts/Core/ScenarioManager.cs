// ============================================================
// ThemeParkGame - ScenarioManager
// シナリオモードの進行管理・目標チェック・クリア判定
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Economy;
using ThemeParkGame.Park;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// シナリオモードのランタイム進行を管理する。
    /// 目標の進捗をリアルタイムで監視し、全目標達成でクリアを判定する。
    /// </summary>
    public class ScenarioManager : MonoBehaviour
    {
        public static ScenarioManager Instance { get; private set; }

        /// <summary>現在アクティブなシナリオデータ（nullならサンドボックスモード）</summary>
        public ScenarioData ActiveScenario { get; private set; }

        /// <summary>シナリオモードが有効か</summary>
        public bool IsScenarioActive => ActiveScenario != null;

        /// <summary>シナリオクリア済みか</summary>
        public bool IsScenarioCleared { get; private set; }

        /// <summary>シナリオ失敗か（制限時間切れ）</summary>
        public bool IsScenarioFailed { get; private set; }

        // 目標チェック間隔
        private float _checkTimer;
        private const float CheckInterval = 2f;

        // クリア済みシナリオの記録
        private static readonly HashSet<ScenarioCountry> _clearedScenarios = new HashSet<ScenarioCountry>();

        /// <summary>指定シナリオがクリア済みか</summary>
        public static bool IsCleared(ScenarioCountry country) => _clearedScenarios.Contains(country);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>シナリオを開始する</summary>
        public void StartScenario(ScenarioData scenario)
        {
            ActiveScenario = scenario;
            IsScenarioCleared = false;
            IsScenarioFailed = false;
            _checkTimer = CheckInterval;

            // 全目標をリセット
            if (scenario.Objectives != null)
            {
                foreach (var obj in scenario.Objectives)
                    obj.IsCompleted = false;
            }

            WebGLOptimizer.LogVerbose($"[ScenarioManager] Scenario started: {scenario.Country} ({scenario.Difficulty})");
        }

        /// <summary>シナリオを終了してサンドボックスに戻す</summary>
        public void EndScenario()
        {
            ActiveScenario = null;
            IsScenarioCleared = false;
            IsScenarioFailed = false;
        }

        private void Update()
        {
            if (ActiveScenario == null || IsScenarioCleared || IsScenarioFailed) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = CheckInterval;

            CheckObjectives();
            CheckTimeLimit();
        }

        // ================================================================
        // 目標チェック
        // ================================================================

        private void CheckObjectives()
        {
            if (ActiveScenario.Objectives == null) return;

            var gm = GameManager.Instance;
            bool allCompleted = true;

            foreach (var obj in ActiveScenario.Objectives)
            {
                if (obj.IsCompleted) continue;

                bool met = EvaluateObjective(obj, gm);
                if (met)
                {
                    obj.IsCompleted = true;
                    WebGLOptimizer.LogVerbose($"[ScenarioManager] Objective completed: {obj.DescriptionKey}");
                }
                else
                {
                    allCompleted = false;
                }
            }

            if (allCompleted && ActiveScenario.Objectives.Count > 0)
            {
                OnScenarioCleared();
            }
        }

        private bool EvaluateObjective(ScenarioObjective obj, GameManager gm)
        {
            switch (obj.Type)
            {
                case ObjectiveType.VisitorTarget:
                    return gm.VisitorManager != null &&
                           gm.VisitorManager.TotalVisitorsToday >= obj.TargetValue;

                case ObjectiveType.MonthlyProfitTarget:
                    return gm.EconomyManager != null &&
                           gm.EconomyManager.GetMonthlyProfit() >= obj.TargetValue;

                case ObjectiveType.ParkRatingTarget:
                    // パーク評価 = 平均満足度で代用
                    return gm.VisitorManager != null &&
                           gm.VisitorManager.AverageHappiness >= obj.TargetValue;

                case ObjectiveType.AttractionCountTarget:
                    return gm.AttractionManager != null &&
                           gm.AttractionManager.TotalCount >= (int)obj.TargetValue;

                case ObjectiveType.TotalRevenueTarget:
                    return gm.EconomyManager != null &&
                           gm.EconomyManager.TotalRevenueEarned >= obj.TargetValue;

                case ObjectiveType.HappinessTarget:
                    return gm.VisitorManager != null &&
                           gm.VisitorManager.AverageHappiness >= obj.TargetValue;

                case ObjectiveType.GoldenTicketTarget:
                    return gm.GoldenTickets >= (int)obj.TargetValue;

                case ObjectiveType.ObtainCertificate:
                    // 簡易実装: 満足度70以上で認定証取得とみなす
                    return gm.VisitorManager != null &&
                           gm.VisitorManager.AverageHappiness >= 70f;

                case ObjectiveType.UnlockZone:
                    // 簡易実装: 収益が一定以上でゾーンアンロックとみなす
                    return gm.EconomyManager != null &&
                           gm.EconomyManager.TotalRevenueEarned >= 100000f;

                default:
                    return false;
            }
        }

        private void CheckTimeLimit()
        {
            if (ActiveScenario.TimeLimitYears <= 0) return;
            if (GameManager.Instance.TimeManager == null) return;

            int currentYear = GameManager.Instance.TimeManager.CurrentYear;
            if (currentYear > ActiveScenario.TimeLimitYears)
            {
                OnScenarioFailed();
            }
        }

        // ================================================================
        // クリア/失敗
        // ================================================================

        private void OnScenarioCleared()
        {
            IsScenarioCleared = true;
            _clearedScenarios.Add(ActiveScenario.Country);

            // 報酬付与
            var reward = ActiveScenario.CompletionReward;
            if (reward != null)
            {
                var gm = GameManager.Instance;
                if (gm.EconomyManager != null && reward.BonusMoney > 0)
                    gm.EconomyManager.AddRevenue(reward.BonusMoney,
                        Economy.RevenueCategory.Other, 0);

                for (int i = 0; i < reward.GoldenTickets; i++)
                    gm.AwardGoldenTicket();
            }

            // ゲーム終了画面へ（クリアとして）
            GameManager.Instance.EndGame();
            WebGLOptimizer.LogVerbose($"[ScenarioManager] SCENARIO CLEARED: {ActiveScenario.Country}!");
        }

        private void OnScenarioFailed()
        {
            IsScenarioFailed = true;
            GameManager.Instance.EndGame();
            WebGLOptimizer.LogVerbose($"[ScenarioManager] Scenario failed (time limit): {ActiveScenario.Country}");
        }

        // ================================================================
        // 進捗表示用
        // ================================================================

        /// <summary>目標の進捗テキストを取得する</summary>
        public string GetObjectiveProgressText()
        {
            if (ActiveScenario == null || ActiveScenario.Objectives == null) return "";

            var gm = GameManager.Instance;
            var sb = new System.Text.StringBuilder();

            foreach (var obj in ActiveScenario.Objectives)
            {
                string check = obj.IsCompleted ? "[OK]" : "[  ]";
                string desc = GetObjectiveDescription(obj, gm);
                sb.AppendLine($"{check} {desc}");
            }

            return sb.ToString();
        }

        private string GetObjectiveDescription(ScenarioObjective obj, GameManager gm)
        {
            float current = 0f;

            switch (obj.Type)
            {
                case ObjectiveType.VisitorTarget:
                    current = gm.VisitorManager != null ? gm.VisitorManager.TotalVisitorsToday : 0;
                    return $"来場者 {current:F0}/{obj.TargetValue:F0}人";

                case ObjectiveType.MonthlyProfitTarget:
                    current = gm.EconomyManager != null ? gm.EconomyManager.GetMonthlyProfit() : 0;
                    return $"月間利益 ${current:N0}/${obj.TargetValue:N0}";

                case ObjectiveType.ParkRatingTarget:
                    current = gm.VisitorManager != null ? gm.VisitorManager.AverageHappiness : 0;
                    return $"パーク評価 {current:F0}/{obj.TargetValue:F0}";

                case ObjectiveType.AttractionCountTarget:
                    current = gm.AttractionManager != null ? gm.AttractionManager.TotalCount : 0;
                    return $"アトラクション数 {current:F0}/{obj.TargetValue:F0}基";

                case ObjectiveType.TotalRevenueTarget:
                    current = gm.EconomyManager != null ? gm.EconomyManager.TotalRevenueEarned : 0;
                    return $"累計収入 ${current:N0}/${obj.TargetValue:N0}";

                case ObjectiveType.HappinessTarget:
                    current = gm.VisitorManager != null ? gm.VisitorManager.AverageHappiness : 0;
                    return $"平均満足度 {current:F0}%/{obj.TargetValue:F0}%";

                case ObjectiveType.GoldenTicketTarget:
                    return $"ゴールデンチケット {gm.GoldenTickets}/{(int)obj.TargetValue}枚";

                case ObjectiveType.ObtainCertificate:
                    return $"認定証取得 ({(CertificateCategory)obj.SubParameter})";

                case ObjectiveType.UnlockZone:
                    return $"ゾーンアンロック ({(ThemeZone)obj.SubParameter})";

                default:
                    return obj.DescriptionKey;
            }
        }

        /// <summary>シナリオのステージ名を返す</summary>
        public static string GetStageName(ScenarioCountry country, int stageIndex)
        {
            return $"{GetCountryName(country)} Stage {stageIndex + 1}";
        }

        /// <summary>国名の日本語表示を返す</summary>
        public static string GetCountryName(ScenarioCountry country)
        {
            switch (country)
            {
                case ScenarioCountry.Japan: return "日本";
                case ScenarioCountry.UnitedStates: return "アメリカ";
                case ScenarioCountry.France: return "フランス";
                default: return country.ToString();
            }
        }
    }
}
