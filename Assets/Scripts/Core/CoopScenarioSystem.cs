// ============================================================
// ThemeParkGame - CoopScenarioSystem
// Co-opモード実コンテンツ追加（共同シナリオ）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>Co-opシナリオの定義データ。2人協力プレイ用のシナリオ条件・目標・役割分担を保持する。</summary>
    [Serializable]
    public class CoopScenario
    {
        public int Id;
        public string Name;
        public string Description;
        public float TimeLimitMinutes;
        public float InitialFunds;
        public int TargetVisitors;
        public float TargetRating;
        public float TargetRevenue;
        public CoopRole Player1Role;
        public CoopRole Player2Role;
        public ScenarioDifficulty Difficulty;
    }

    /// <summary>Co-opシナリオの進捗追跡データ。各目標の達成状況をリアルタイムで管理する。</summary>
    [Serializable]
    public class CoopProgress
    {
        public int ScenarioId;
        public float ElapsedTime;
        public float CurrentVisitors;
        public float CurrentRating;
        public float CurrentRevenue;
        public bool IsVisitorGoalMet;
        public bool IsRatingGoalMet;
        public bool IsRevenueGoalMet;
        public bool IsCompleted;
    }

    /// <summary>
    /// Co-opシナリオシステム。2人プレイヤーが役割分担してパークを運営する。
    /// P1=アトラクション担当、P2=スタッフ＆経済担当。制限時間内に全目標達成を目指す。
    /// </summary>
    public class CoopScenarioSystem : MonoBehaviour
    {
        public static CoopScenarioSystem Instance { get; private set; }

        private static readonly Color BgDark = new Color(0.06f, 0.09f, 0.16f, 0.92f);
        private const float PROGRESS_SYNC_INTERVAL = 5f;
        private const float WEATHER_DISASTER_INTERVAL = 300f;

        private readonly List<CoopScenario> _scenarios = new List<CoopScenario>();
        private CoopScenario _activeScenario;
        private CoopProgress _progress;
        private bool _isRunning;
        private float _progressSyncTimer;
        private float _weatherDisasterTimer;
        private int _rivalSpawnCount;
        private CoopRole _localRole = CoopRole.FullAccess;

        private GameObject _selectionPanel, _progressOverlay, _completionPanel;
        private Text _timerText, _visitorGoalText, _ratingGoalText, _revenueGoalText;
        private Text _completionTitleText, _completionDetailText;
        private bool _selectionVisible;

        public CoopScenario ActiveScenario => _activeScenario;
        public CoopProgress Progress => _progress;
        public bool IsRunning => _isRunning;
        public CoopRole LocalRole => _localRole;
        public IReadOnlyList<CoopScenario> Scenarios => _scenarios.AsReadOnly();

        [Serializable] private class RoleAssignData { public int ScenarioId; public int Player1Role; public int Player2Role; }
        [Serializable] private class ScenarioCompleteData { public int ScenarioId; public bool Success; public float ElapsedTime; public float FinalVisitors; public float FinalRating; public float FinalRevenue; }

        // ---- ライフサイクル ----

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitializeScenarios();
            WebGLOptimizer.LogVerbose("[CoopScenario] システム初期化完了");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (CoopManager.Instance != null) CoopManager.Instance.OnActionReceived -= HandleCoopAction;
        }

        private void OnEnable()
        {
            if (CoopManager.Instance != null) CoopManager.Instance.OnActionReceived += HandleCoopAction;
        }

        private void OnDisable()
        {
            if (CoopManager.Instance != null) CoopManager.Instance.OnActionReceived -= HandleCoopAction;
        }

        private void Update()
        {
            if (!_isRunning || _activeScenario == null) return;
            _progress.ElapsedTime += Time.deltaTime;
            if (_progress.ElapsedTime >= _activeScenario.TimeLimitMinutes * 60f) { EndScenario(false); return; }
            CollectProgress();
            CheckGoals();
            if (_progress.IsVisitorGoalMet && _progress.IsRatingGoalMet && _progress.IsRevenueGoalMet)
            {
                _progress.IsCompleted = true; EndScenario(true); return;
            }
            _progressSyncTimer += Time.deltaTime;
            if (_progressSyncTimer >= PROGRESS_SYNC_INTERVAL) { _progressSyncTimer = 0f; SyncProgress(); }
            UpdateScenarioEvents();
            RefreshProgressOverlay();
        }

        // ---- シナリオ定義 ----

        /// <summary>3種類のCo-opシナリオを初期化する。</summary>
        private void InitializeScenarios()
        {
            _scenarios.Clear();
            _scenarios.Add(new CoopScenario {
                Id = 1, Name = "テーマパーク再建",
                Description = "廃墟と化したパークを2人で再建せよ！\nP1はアトラクション建設、P2はスタッフ雇用＆経済管理を担当。",
                TimeLimitMinutes = 20f, InitialFunds = 30000f,
                TargetVisitors = 50, TargetRating = 40f, TargetRevenue = 10000f,
                Player1Role = CoopRole.AttractionManager, Player2Role = CoopRole.StaffEconomyManager,
                Difficulty = ScenarioDifficulty.Easy
            });
            _scenarios.Add(new CoopScenario {
                Id = 2, Name = "嵐を乗り越えろ",
                Description = "台風や雷雨が頻発するパークを維持せよ！\nP1はアトラクション維持、P2はスタッフ配置＆危機管理。\n※5分ごとに天候災害が発生",
                TimeLimitMinutes = 25f, InitialFunds = 50000f,
                TargetVisitors = 80, TargetRating = 55f, TargetRevenue = 25000f,
                Player1Role = CoopRole.AttractionManager, Player2Role = CoopRole.StaffEconomyManager,
                Difficulty = ScenarioDifficulty.Normal
            });
            _scenarios.Add(new CoopScenario {
                Id = 3, Name = "究極のテーマパーク",
                Description = "全ゾーン解放済み、ライバル3つが同時出現！\nP1はアトラクション＆イベント、P2は経営戦略を担当。\n※開始時からライバル3パークが稼働中",
                TimeLimitMinutes = 30f, InitialFunds = 80000f,
                TargetVisitors = 150, TargetRating = 75f, TargetRevenue = 100000f,
                Player1Role = CoopRole.AttractionManager, Player2Role = CoopRole.StaffEconomyManager,
                Difficulty = ScenarioDifficulty.Hard
            });
            WebGLOptimizer.LogVerbose($"[CoopScenario] {_scenarios.Count}個のシナリオを登録");
        }

        // ---- シナリオ操作 ----

        /// <summary>指定IDのシナリオを開始する。ホスト側から呼び出す。</summary>
        public void StartScenario(int scenarioId)
        {
            var scenario = _scenarios.Find(s => s.Id == scenarioId);
            if (scenario == null) { WebGLOptimizer.LogVerbose($"[CoopScenario] シナリオID={scenarioId}が見つかりません"); return; }
            if (!CoopManager.Instance?.IsConnected ?? true)
            {
                GameManager.Instance?.ShowNotification("Co-opルームに接続してからシナリオを開始してください", NotifLevel.Warning);
                return;
            }

            _activeScenario = scenario;
            _progress = new CoopProgress { ScenarioId = scenario.Id };
            _isRunning = true;
            _progressSyncTimer = 0f;
            _weatherDisasterTimer = 0f;
            _rivalSpawnCount = 0;
            _localRole = CoopManager.Instance.IsHost ? scenario.Player1Role : scenario.Player2Role;
            GameManager.Instance?.EconomyManager?.Initialize((int)scenario.InitialFunds);

            var roleData = new RoleAssignData { ScenarioId = scenario.Id, Player1Role = (int)scenario.Player1Role, Player2Role = (int)scenario.Player2Role };
            CoopManager.Instance.SendAction("coop_role_assign", JsonUtility.ToJson(roleData));
            InitializeScenarioSpecific(scenario);
            HideSelectionUI();
            ShowProgressOverlay();

            GameManager.Instance?.ShowNotification($"シナリオ「{scenario.Name}」を開始！ 役割: {GetRoleDisplayName(_localRole)}", NotifLevel.Success);
            WebGLOptimizer.LogVerbose($"[CoopScenario] シナリオ開始: {scenario.Name} (Role={_localRole})");
        }

        /// <summary>シナリオ固有の初期設定を適用する。</summary>
        private void InitializeScenarioSpecific(CoopScenario scenario)
        {
            if (scenario.Id == 2) { _weatherDisasterTimer = 0f; }
            else if (scenario.Id == 3)
            {
                _rivalSpawnCount = 3;
                var rivalSystem = FindObjectOfType<Park.RivalParkSystem>();
                if (rivalSystem != null)
                {
                    for (int i = 0; i < _rivalSpawnCount; i++) rivalSystem.ForceSpawnRival();
                    WebGLOptimizer.LogVerbose("[CoopScenario] ライバル3パーク出現");
                }
            }
        }

        /// <summary>実行中のシナリオを終了する。</summary>
        private void EndScenario(bool success)
        {
            _isRunning = false;
            var data = new ScenarioCompleteData {
                ScenarioId = _activeScenario.Id, Success = success, ElapsedTime = _progress.ElapsedTime,
                FinalVisitors = _progress.CurrentVisitors, FinalRating = _progress.CurrentRating, FinalRevenue = _progress.CurrentRevenue
            };
            CoopManager.Instance?.SendAction("coop_scenario_complete", JsonUtility.ToJson(data));
            HideProgressOverlay();
            ShowCompletionUI(success);
            string r = success ? "成功" : "失敗";
            GameManager.Instance?.ShowNotification($"シナリオ「{_activeScenario.Name}」{r}！", success ? NotifLevel.Success : NotifLevel.Warning);
            WebGLOptimizer.LogVerbose($"[CoopScenario] シナリオ終了: {_activeScenario.Name} ({r})");
        }

        /// <summary>シナリオをキャンセルして中断する。</summary>
        public void CancelScenario()
        {
            if (!_isRunning) return;
            _isRunning = false;
            HideProgressOverlay();
            GameManager.Instance?.ShowNotification($"シナリオ「{_activeScenario.Name}」を中断しました", NotifLevel.Warning);
            WebGLOptimizer.LogVerbose($"[CoopScenario] シナリオ中断: {_activeScenario.Name}");
            _activeScenario = null; _progress = null; _localRole = CoopRole.FullAccess;
        }

        // ---- 進捗管理 ----

        /// <summary>GameManagerから現在の進捗値を収集する。</summary>
        private void CollectProgress()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.VisitorManager != null) _progress.CurrentVisitors = gm.VisitorManager.TotalVisitorsToday;
            if (gm.ParkManager != null) _progress.CurrentRating = gm.ParkManager.GetOverallRating();
            if (gm.EconomyManager != null) _progress.CurrentRevenue = gm.EconomyManager.CurrentMonthRevenue;
        }

        /// <summary>各目標の達成状況をチェックする。</summary>
        private void CheckGoals()
        {
            if (_activeScenario == null) return;
            _progress.IsVisitorGoalMet = _progress.CurrentVisitors >= _activeScenario.TargetVisitors;
            _progress.IsRatingGoalMet = _progress.CurrentRating >= _activeScenario.TargetRating;
            _progress.IsRevenueGoalMet = _progress.CurrentRevenue >= _activeScenario.TargetRevenue;
        }

        /// <summary>進捗データをネットワーク経由でパートナーに同期する。</summary>
        private void SyncProgress()
        {
            if (CoopManager.Instance == null || !CoopManager.Instance.IsConnected) return;
            CoopManager.Instance.SendAction("coop_progress", JsonUtility.ToJson(_progress));
        }

        // ---- シナリオ固有イベント ----

        /// <summary>嵐シナリオ: 5分ごとに天候災害を発生させる。</summary>
        private void UpdateScenarioEvents()
        {
            if (_activeScenario == null || _activeScenario.Id != 2) return;
            _weatherDisasterTimer += Time.deltaTime;
            if (_weatherDisasterTimer >= WEATHER_DISASTER_INTERVAL)
            {
                _weatherDisasterTimer = 0f;
                var weather = GameManager.Instance?.WeatherSystem;
                if (weather != null)
                {
                    string storm = UnityEngine.Random.Range(0, 2) == 0 ? "Typhoon" : "Thunderstorm";
                    weather.ForceWeather(storm);
                    WebGLOptimizer.LogVerbose($"[CoopScenario] 天候災害発生: {storm}");
                }
                GameManager.Instance?.ShowNotification("天候災害が発生しました！パークを守ってください！", NotifLevel.Warning);
            }
        }

        // ---- ネットワーク通信 ----

        /// <summary>CoopManagerから受信したアクションを処理する。</summary>
        private void HandleCoopAction(CoopManager.CoopAction action)
        {
            if (action == null) return;
            switch (action.type)
            {
                case "coop_role_assign":      HandleRoleAssign(action.data); break;
                case "coop_progress":         HandleProgressUpdate(action.data); break;
                case "coop_scenario_complete": HandleScenarioComplete(action.data); break;
            }
        }

        /// <summary>役割分担通知を処理する（ゲスト側で受信）。</summary>
        private void HandleRoleAssign(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var data = JsonUtility.FromJson<RoleAssignData>(json);
                var scenario = _scenarios.Find(s => s.Id == data.ScenarioId);
                if (scenario == null) return;

                _activeScenario = scenario;
                _progress = new CoopProgress { ScenarioId = scenario.Id };
                _isRunning = true;
                _progressSyncTimer = 0f;
                _weatherDisasterTimer = 0f;
                _localRole = scenario.Player2Role;
                GameManager.Instance?.EconomyManager?.Initialize((int)scenario.InitialFunds);
                InitializeScenarioSpecific(scenario);
                HideSelectionUI();
                ShowProgressOverlay();
                GameManager.Instance?.ShowNotification($"シナリオ「{scenario.Name}」に参加！ 役割: {GetRoleDisplayName(_localRole)}", NotifLevel.Success);
                WebGLOptimizer.LogVerbose($"[CoopScenario] 役割受信: {_localRole} (シナリオ={scenario.Name})");
            }
            catch (Exception e) { WebGLOptimizer.LogVerbose($"[CoopScenario] 役割データのパースに失敗: {e.Message}"); }
        }

        /// <summary>パートナーからの進捗更新を処理する。</summary>
        private void HandleProgressUpdate(string json)
        {
            if (string.IsNullOrEmpty(json) || _progress == null) return;
            try
            {
                var remote = JsonUtility.FromJson<CoopProgress>(json);
                if (remote.ScenarioId != _progress.ScenarioId) return;
                if (remote.CurrentVisitors > _progress.CurrentVisitors) _progress.CurrentVisitors = remote.CurrentVisitors;
                if (remote.CurrentRating > _progress.CurrentRating) _progress.CurrentRating = remote.CurrentRating;
                if (remote.CurrentRevenue > _progress.CurrentRevenue) _progress.CurrentRevenue = remote.CurrentRevenue;
                CheckGoals();
                RefreshProgressOverlay();
            }
            catch (Exception e) { WebGLOptimizer.LogVerbose($"[CoopScenario] 進捗データのパースに失敗: {e.Message}"); }
        }

        /// <summary>パートナーからのシナリオ完了通知を処理する。</summary>
        private void HandleScenarioComplete(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var data = JsonUtility.FromJson<ScenarioCompleteData>(json);
                if (_activeScenario == null || data.ScenarioId != _activeScenario.Id) return;
                _isRunning = false;
                _progress.IsCompleted = data.Success;
                _progress.CurrentVisitors = data.FinalVisitors;
                _progress.CurrentRating = data.FinalRating;
                _progress.CurrentRevenue = data.FinalRevenue;
                HideProgressOverlay();
                ShowCompletionUI(data.Success);
                string r = data.Success ? "成功" : "失敗";
                GameManager.Instance?.ShowNotification($"シナリオ「{_activeScenario.Name}」{r}！", data.Success ? NotifLevel.Success : NotifLevel.Warning);
            }
            catch (Exception e) { WebGLOptimizer.LogVerbose($"[CoopScenario] 完了データのパースに失敗: {e.Message}"); }
        }

        // ---- 役割権限チェック ----

        /// <summary>現在の役割でアトラクション関連操作が許可されているか。</summary>
        public bool CanManageAttractions()
        {
            if (!_isRunning) return true;
            return _localRole == CoopRole.AttractionManager || _localRole == CoopRole.FullAccess;
        }

        /// <summary>現在の役割でスタッフ関連操作が許可されているか。</summary>
        public bool CanManageStaff()
        {
            if (!_isRunning) return true;
            return _localRole == CoopRole.StaffEconomyManager || _localRole == CoopRole.FullAccess;
        }

        /// <summary>現在の役割で経済関連操作が許可されているか。</summary>
        public bool CanManageEconomy()
        {
            if (!_isRunning) return true;
            return _localRole == CoopRole.StaffEconomyManager || _localRole == CoopRole.FullAccess;
        }

        /// <summary>現在の役割でイベント関連操作が許可されているか。</summary>
        public bool CanManageEvents()
        {
            if (!_isRunning) return true;
            return _localRole == CoopRole.AttractionManager || _localRole == CoopRole.FullAccess;
        }

        // ---- ヘルパー ----

        private static string GetRoleDisplayName(CoopRole role)
        {
            switch (role)
            {
                case CoopRole.AttractionManager:  return "アトラクション担当";
                case CoopRole.StaffEconomyManager: return "スタッフ・経済担当";
                case CoopRole.FullAccess:          return "全権限";
                default:                           return "不明";
            }
        }

        private static string GetDifficultyDisplayName(ScenarioDifficulty d)
        {
            switch (d)
            {
                case ScenarioDifficulty.Easy:   return "やさしい";
                case ScenarioDifficulty.Normal: return "ふつう";
                case ScenarioDifficulty.Hard:   return "むずかしい";
                default:                        return "不明";
            }
        }

        private static Color GetDifficultyColor(ScenarioDifficulty d)
        {
            switch (d)
            {
                case ScenarioDifficulty.Easy:   return new Color(0.3f, 0.85f, 0.4f);
                case ScenarioDifficulty.Normal: return new Color(1f, 0.82f, 0.3f);
                case ScenarioDifficulty.Hard:   return new Color(1f, 0.35f, 0.3f);
                default:                        return Color.white;
            }
        }

        private static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:D2}:{s:D2}";
        }

        // ================================================================
        // UI: シナリオ選択パネル
        // ================================================================

        /// <summary>シナリオ選択UIを表示する。</summary>
        public void ShowSelectionUI()
        {
            if (_selectionPanel == null) BuildSelectionUI();
            _selectionPanel.SetActive(true); _selectionVisible = true;
        }

        /// <summary>シナリオ選択UIを非表示にする。</summary>
        public void HideSelectionUI()
        {
            if (_selectionPanel != null) _selectionPanel.SetActive(false);
            _selectionVisible = false;
        }

        /// <summary>シナリオ選択UIの表示をトグルする。</summary>
        public void ToggleSelectionUI() { if (_selectionVisible) HideSelectionUI(); else ShowSelectionUI(); }

        private void BuildSelectionUI()
        {
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            _selectionPanel = new GameObject("CoopScenarioSelectPanel");
            _selectionPanel.transform.SetParent(canvas.transform, false);
            var pr = _selectionPanel.AddComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.1f, 0.05f); pr.anchorMax = new Vector2(0.9f, 0.95f);
            pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
            var pi = _selectionPanel.AddComponent<Image>();
            pi.color = BgDark; pi.raycastTarget = true;

            MakeLabel(_selectionPanel.transform, "Title", new Vector2(0.05f, 0.9f), new Vector2(0.85f, 0.98f),
                "Co-op シナリオ選択", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            MakeButton(_selectionPanel.transform, "CloseBtn", new Vector2(0.88f, 0.92f), new Vector2(0.98f, 0.98f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideSelectionUI);

            float cardH = 0.27f, startY = 0.85f;
            for (int i = 0; i < _scenarios.Count; i++)
            {
                float top = startY - (cardH + 0.02f) * i;
                BuildScenarioCard(_selectionPanel.transform, _scenarios[i], new Vector2(0.03f, top - cardH), new Vector2(0.97f, top));
            }
            _selectionPanel.SetActive(false);
        }

        private void BuildScenarioCard(Transform parent, CoopScenario sc, Vector2 aMin, Vector2 aMax)
        {
            var card = new GameObject($"ScenarioCard_{sc.Id}");
            card.transform.SetParent(parent, false);
            var cr = card.AddComponent<RectTransform>();
            cr.anchorMin = aMin; cr.anchorMax = aMax; cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
            card.AddComponent<Image>().color = new Color(0.1f, 0.13f, 0.22f, 0.95f);

            MakeLabel(card.transform, "Name", new Vector2(0.02f, 0.72f), new Vector2(0.65f, 0.95f),
                sc.Name, 18, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            MakeLabel(card.transform, "Diff", new Vector2(0.65f, 0.72f), new Vector2(0.98f, 0.95f),
                $"[{GetDifficultyDisplayName(sc.Difficulty)}]", 14, FontStyle.Bold, GetDifficultyColor(sc.Difficulty), TextAnchor.MiddleRight);
            MakeLabel(card.transform, "Desc", new Vector2(0.02f, 0.3f), new Vector2(0.6f, 0.72f),
                sc.Description, 12, FontStyle.Normal, new Color(0.75f, 0.78f, 0.85f), TextAnchor.UpperLeft);
            MakeLabel(card.transform, "Goals", new Vector2(0.62f, 0.18f), new Vector2(0.98f, 0.72f),
                $"制限時間: {sc.TimeLimitMinutes:F0}分\n初期資金: ${sc.InitialFunds:N0}\n目標来場者: {sc.TargetVisitors}人\n目標評価: {sc.TargetRating:F0}\n目標売上: ${sc.TargetRevenue:N0}",
                12, FontStyle.Normal, new Color(0.6f, 0.8f, 1f), TextAnchor.UpperLeft);

            int id = sc.Id;
            MakeButton(card.transform, "StartBtn", new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.18f),
                "このシナリオを開始", new Color(0.15f, 0.45f, 0.25f), () => StartScenario(id));
        }

        // ================================================================
        // UI: 進捗オーバーレイ
        // ================================================================

        private void ShowProgressOverlay()
        {
            if (_progressOverlay == null) BuildProgressOverlay();
            _progressOverlay.SetActive(true); RefreshProgressOverlay();
        }

        private void HideProgressOverlay() { if (_progressOverlay != null) _progressOverlay.SetActive(false); }

        private void BuildProgressOverlay()
        {
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            _progressOverlay = new GameObject("CoopProgressOverlay");
            _progressOverlay.transform.SetParent(canvas.transform, false);
            var r = _progressOverlay.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0.01f, 0.7f); r.anchorMax = new Vector2(0.25f, 0.98f);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var bg = _progressOverlay.AddComponent<Image>();
            bg.color = BgDark; bg.raycastTarget = false;

            _timerText = MakeLabel(_progressOverlay.transform, "Timer", new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.98f),
                "00:00 / 00:00", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter).GetComponent<Text>();
            _visitorGoalText = MakeLabel(_progressOverlay.transform, "VGoal", new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.78f),
                "", 13, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft).GetComponent<Text>();
            _ratingGoalText = MakeLabel(_progressOverlay.transform, "RGoal", new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.54f),
                "", 13, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft).GetComponent<Text>();
            _revenueGoalText = MakeLabel(_progressOverlay.transform, "RevGoal", new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.3f),
                "", 13, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft).GetComponent<Text>();
            MakeButton(_progressOverlay.transform, "CancelBtn", new Vector2(0.6f, 0.01f), new Vector2(0.95f, 0.1f),
                "中断", new Color(0.6f, 0.2f, 0.15f), CancelScenario);
            _progressOverlay.SetActive(false);
        }

        private void RefreshProgressOverlay()
        {
            if (_activeScenario == null || _progress == null) return;
            if (_timerText != null)
            {
                float lim = _activeScenario.TimeLimitMinutes * 60f;
                float rem = Mathf.Max(0f, lim - _progress.ElapsedTime);
                string c = rem < 60f ? "#FF6666" : "#FFFFFF";
                _timerText.text = $"<color={c}>{FormatTime(_progress.ElapsedTime)} / {FormatTime(lim)}</color>";
            }
            if (_visitorGoalText != null)
            {
                string chk = _progress.IsVisitorGoalMet ? "<color=#66FF66>[達成]</color>" : "[ ]";
                _visitorGoalText.text = $"{chk} 来場者: {_progress.CurrentVisitors:F0} / {_activeScenario.TargetVisitors}";
            }
            if (_ratingGoalText != null)
            {
                string chk = _progress.IsRatingGoalMet ? "<color=#66FF66>[達成]</color>" : "[ ]";
                _ratingGoalText.text = $"{chk} 評価: {_progress.CurrentRating:F1} / {_activeScenario.TargetRating:F0}";
            }
            if (_revenueGoalText != null)
            {
                string chk = _progress.IsRevenueGoalMet ? "<color=#66FF66>[達成]</color>" : "[ ]";
                _revenueGoalText.text = $"{chk} 売上: ${_progress.CurrentRevenue:N0} / ${_activeScenario.TargetRevenue:N0}";
            }
        }

        // ================================================================
        // UI: 完了画面
        // ================================================================

        private void ShowCompletionUI(bool success)
        {
            if (_completionPanel == null) BuildCompletionUI();
            if (_completionTitleText != null)
            {
                _completionTitleText.text = success ? "シナリオクリア！" : "シナリオ失敗...";
                _completionTitleText.color = success ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.35f, 0.3f);
            }
            if (_completionDetailText != null && _activeScenario != null && _progress != null)
            {
                string vc = _progress.IsVisitorGoalMet ? "[達成]" : "[未達]";
                string rc = _progress.IsRatingGoalMet  ? "[達成]" : "[未達]";
                string ec = _progress.IsRevenueGoalMet ? "[達成]" : "[未達]";
                _completionDetailText.text =
                    $"シナリオ: {_activeScenario.Name}\n経過時間: {FormatTime(_progress.ElapsedTime)}\n" +
                    $"あなたの役割: {GetRoleDisplayName(_localRole)}\n\n--- 目標達成状況 ---\n" +
                    $"{vc} 来場者: {_progress.CurrentVisitors:F0} / {_activeScenario.TargetVisitors}\n" +
                    $"{rc} 評価: {_progress.CurrentRating:F1} / {_activeScenario.TargetRating:F0}\n" +
                    $"{ec} 売上: ${_progress.CurrentRevenue:N0} / ${_activeScenario.TargetRevenue:N0}";
            }
            _completionPanel.SetActive(true);
        }

        private void HideCompletionUI()
        {
            if (_completionPanel != null) _completionPanel.SetActive(false);
            _activeScenario = null; _progress = null; _localRole = CoopRole.FullAccess;
        }

        private void BuildCompletionUI()
        {
            var canvas = CanvasCache.Get();
            if (canvas == null) return;
            _completionPanel = new GameObject("CoopScenarioComplete");
            _completionPanel.transform.SetParent(canvas.transform, false);
            var r = _completionPanel.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0.2f, 0.15f); r.anchorMax = new Vector2(0.8f, 0.85f);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var bg = _completionPanel.AddComponent<Image>();
            bg.color = BgDark; bg.raycastTarget = true;

            _completionTitleText = MakeLabel(_completionPanel.transform, "CompTitle", new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.96f),
                "", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter).GetComponent<Text>();
            _completionDetailText = MakeLabel(_completionPanel.transform, "CompDetail", new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.78f),
                "", 15, FontStyle.Normal, new Color(0.85f, 0.88f, 0.92f), TextAnchor.UpperLeft).GetComponent<Text>();
            MakeButton(_completionPanel.transform, "CompCloseBtn", new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.15f),
                "閉じる", new Color(0.2f, 0.35f, 0.55f), HideCompletionUI);
            _completionPanel.SetActive(false);
        }

        // ================================================================
        // UIヘルパー
        // ================================================================

        private GameObject MakeLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            string content, int fontSize, FontStyle style, Color color, TextAnchor anchor)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(4f, 0f); rect.offsetMax = new Vector2(-4f, 0f);
            var txt = obj.AddComponent<Text>();
            txt.text = content; txt.font = FontManager.Regular;
            txt.fontSize = fontSize; txt.fontStyle = style;
            txt.color = color; txt.alignment = anchor; txt.supportRichText = true;
            return obj;
        }

        private void MakeButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            string label, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>(); img.color = bgColor;
            var btn = obj.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
            var lbl = new GameObject("Label");
            lbl.transform.SetParent(obj.transform, false);
            var lr = lbl.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            var t = lbl.AddComponent<Text>();
            t.text = label; t.font = FontManager.Regular; t.fontSize = 14;
            t.fontStyle = FontStyle.Bold; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
        }
    }
}
