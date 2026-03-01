// ============================================================
// ThemeParkGame - BusSystem
// バス停配置による来場者増加ボーナスシステム
// PS1「新テーマパーク」の仕様再現: バス停が多いほど来場者増加
// ============================================================

using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    /// <summary>
    /// バス停の配置数に基づいて来場者スポーン率にボーナスを与えるシステム。
    ///
    /// 【ゲームデザイン: PS1「新テーマパーク」再現】
    /// - バス停1つ: +10%の来場者ボーナス
    /// - バス停2つ: +18%（逓減効果）
    /// - バス停3つ: +25%
    /// - 最大5つまで効果あり（最大+35%）
    /// - バス停の維持費あり（毎月固定）
    /// </summary>
    public class BusSystem : MonoBehaviour
    {
        public static BusSystem Instance { get; private set; }

        /// <summary>バス停1つあたりの基本スポーンボーナス</summary>
        private const float BASE_BONUS_PER_STOP = 0.10f;

        /// <summary>バス停追加ごとのボーナス逓減率</summary>
        private const float DIMINISHING_FACTOR = 0.85f;

        /// <summary>効果のあるバス停の最大数</summary>
        private const int MAX_EFFECTIVE_STOPS = 5;

        /// <summary>バス停1つあたりの月間維持費</summary>
        public const float MONTHLY_MAINTENANCE_PER_STOP = 50f;

        /// <summary>バス停の建設費用</summary>
        public const float CONSTRUCTION_COST = 500f;

        /// <summary>現在のバス停数</summary>
        public int BusStopCount { get; private set; }

        /// <summary>現在のスポーンボーナス倍率</summary>
        public float CurrentSpawnMultiplier => CalculateSpawnMultiplier();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// バス停を追加する。
        /// </summary>
        public bool AddBusStop()
        {
            var em = GameManager.Instance?.EconomyManager;
            if (em == null) return false;

            if (!em.CanAfford(CONSTRUCTION_COST))
            {
                NotificationSystem.Instance?.Notify(
                    $"バス停の建設には${CONSTRUCTION_COST:N0}が必要です。", NotifLevel.Warning);
                return false;
            }

            em.PayExpense(CONSTRUCTION_COST, ExpenseCategory.Construction);
            BusStopCount++;

            float bonus = (CurrentSpawnMultiplier - 1f) * 100f;
            NotificationSystem.Instance?.Notify(
                $"バス停を設置しました！（{BusStopCount}箇所） 来場者+{bonus:F0}%", NotifLevel.Success);

            WebGLOptimizer.LogVerbose(
                $"[BusSystem] バス停追加: {BusStopCount}箇所, スポーン倍率: {CurrentSpawnMultiplier:F2}x");

            return true;
        }

        /// <summary>
        /// バス停を撤去する。
        /// </summary>
        public bool RemoveBusStop()
        {
            if (BusStopCount <= 0) return false;

            BusStopCount--;
            NotificationSystem.Instance?.Notify(
                $"バス停を撤去しました。（残り{BusStopCount}箇所）", NotifLevel.Info);

            WebGLOptimizer.LogVerbose(
                $"[BusSystem] バス停撤去: {BusStopCount}箇所, スポーン倍率: {CurrentSpawnMultiplier:F2}x");

            return true;
        }

        /// <summary>
        /// バス停数に基づくスポーン倍率を計算する。
        /// 逓減効果あり: 最初のバス停が最も効果的。
        /// </summary>
        private float CalculateSpawnMultiplier()
        {
            if (BusStopCount <= 0) return 1.0f;

            int effectiveStops = Mathf.Min(BusStopCount, MAX_EFFECTIVE_STOPS);
            float totalBonus = 0f;

            for (int i = 0; i < effectiveStops; i++)
            {
                totalBonus += BASE_BONUS_PER_STOP * Mathf.Pow(DIMINISHING_FACTOR, i);
            }

            return 1.0f + totalBonus;
        }

        /// <summary>
        /// バス停の月間維持費合計を取得する。
        /// </summary>
        public float GetMonthlyMaintenanceCost()
        {
            return BusStopCount * MONTHLY_MAINTENANCE_PER_STOP;
        }

        /// <summary>
        /// セーブデータからバス停数を復元する。
        /// </summary>
        public void RestoreFromSave(int busStopCount)
        {
            BusStopCount = Mathf.Max(0, busStopCount);
            WebGLOptimizer.LogVerbose($"[BusSystem] セーブから復元: {BusStopCount}箇所");
        }

        /// <summary>
        /// ステータスサマリーを取得する（UI表示用）。
        /// </summary>
        public string GetStatusSummary()
        {
            float bonus = (CurrentSpawnMultiplier - 1f) * 100f;
            float monthlyCost = GetMonthlyMaintenanceCost();
            return $"バス停: {BusStopCount}箇所 | 来場者+{bonus:F0}% | 維持費: ${monthlyCost:N0}/月";
        }
    }
}
