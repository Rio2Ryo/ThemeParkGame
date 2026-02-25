// ============================================================
// ThemeParkGame - ChallengeSystem
// デイリー/ウィークリーチャレンジ（来場者数・売上目標）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Economy;

namespace ThemeParkGame.Core
{
    public enum ChallengeType
    {
        DailyVisitors,      // 1日の来場者数目標
        DailyRevenue,       // 1日の売上目標
        WeeklyVisitors,     // 1週間の来場者数合計
        WeeklyRevenue,      // 1週間の売上合計
        WeeklyRating,       // 週末時点の評価目標
        DailySatisfaction   // 1日の平均満足度目標
    }

    [Serializable]
    public class Challenge
    {
        public int Id;
        public ChallengeType Type;
        public string Title;
        public string Description;
        public float TargetValue;
        public float CurrentValue;
        public float RewardMoney;
        public int RewardTickets;
        public bool IsCompleted;
        public bool IsExpired;
        public int RemainingDays;

        public float Progress => TargetValue > 0 ? Mathf.Clamp01(CurrentValue / TargetValue) : 0f;
    }

    /// <summary>
    /// デイリー・ウィークリーチャレンジを生成・追跡・報酬付与する。
    /// 毎日新しいデイリーチャレンジ、7日ごとにウィークリーチャレンジが発生する。
    /// </summary>
    public class ChallengeSystem : MonoBehaviour
    {
        public static ChallengeSystem Instance { get; private set; }

        // ---- 設定 ----
        private const int MAX_DAILY_CHALLENGES = 2;
        private const int MAX_WEEKLY_CHALLENGES = 1;
        private const float BASE_DAILY_VISITOR_TARGET = 15f;
        private const float BASE_DAILY_REVENUE_TARGET = 2000f;
        private const float BASE_WEEKLY_VISITOR_TARGET = 80f;
        private const float BASE_WEEKLY_REVENUE_TARGET = 12000f;

        // ---- 状態 ----
        private readonly List<Challenge> _activeChallenges = new List<Challenge>();
        private readonly List<Challenge> _completedHistory = new List<Challenge>();
        private int _nextChallengeId = 1;
        private int _dayCounter;
        private float _todayRevenue;
        private int _todayVisitors;
        private float _weekRevenue;
        private int _weekVisitors;
        private int _totalChallengesCompleted;

        // ---- UI ----
        private GameObject _challengePanel;
        private Text _challengeListText;
        private Text _challengeStatusText;

        // ---- プロパティ ----
        public IReadOnlyList<Challenge> ActiveChallenges => _activeChallenges.AsReadOnly();
        public int TotalChallengesCompleted => _totalChallengesCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            GameEvents.OnVisitorEnterPark += HandleVisitorEnter;
            GameEvents.OnRevenueEarned += HandleRevenueEarned;

            if (GameManager.Instance?.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayChanged += HandleDayChanged;
            }
        }

        private void OnDisable()
        {
            GameEvents.OnVisitorEnterPark -= HandleVisitorEnter;
            GameEvents.OnRevenueEarned -= HandleRevenueEarned;

            if (GameManager.Instance?.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnDayChanged -= HandleDayChanged;
            }
        }

        // ================================================================
        // イベントハンドラ
        // ================================================================

        private void HandleVisitorEnter(int visitorId)
        {
            _todayVisitors++;
            _weekVisitors++;
            UpdateChallengeProgress(ChallengeType.DailyVisitors, _todayVisitors);
            UpdateChallengeProgress(ChallengeType.WeeklyVisitors, _weekVisitors);
        }

        private void HandleRevenueEarned(float amount)
        {
            _todayRevenue += amount;
            _weekRevenue += amount;
            UpdateChallengeProgress(ChallengeType.DailyRevenue, _todayRevenue);
            UpdateChallengeProgress(ChallengeType.WeeklyRevenue, _weekRevenue);
        }

        private void HandleDayChanged()
        {
            _dayCounter++;

            // 満足度チャレンジの更新
            float avgSat = GameManager.Instance?.VisitorManager?.AverageSatisfaction ?? 0f;
            UpdateChallengeProgress(ChallengeType.DailySatisfaction, avgSat);

            // デイリーチャレンジの期限チェック
            ExpireDailyChallenges();

            // デイリーリセット
            _todayRevenue = 0f;
            _todayVisitors = 0;

            // 新しいデイリーチャレンジ生成
            GenerateDailyChallenges();

            // 7日ごとにウィークリーチャレンジ
            if (_dayCounter % 7 == 0)
            {
                // 評価チャレンジの最終チェック
                float rating = GameManager.Instance?.ParkManager?.Rating?.OverallRating ?? 0f;
                UpdateChallengeProgress(ChallengeType.WeeklyRating, rating);

                ExpireWeeklyChallenges();
                _weekRevenue = 0f;
                _weekVisitors = 0;
                GenerateWeeklyChallenges();
            }

            RefreshUI();
        }

        // ================================================================
        // チャレンジ生成
        // ================================================================

        public void Initialize()
        {
            _activeChallenges.Clear();
            _completedHistory.Clear();
            _dayCounter = 0;
            _todayRevenue = 0f;
            _todayVisitors = 0;
            _weekRevenue = 0f;
            _weekVisitors = 0;
            _totalChallengesCompleted = 0;
            _nextChallengeId = 1;

            GenerateDailyChallenges();
            GenerateWeeklyChallenges();
        }

        private void GenerateDailyChallenges()
        {
            int currentDaily = 0;
            foreach (var c in _activeChallenges)
            {
                if (!c.IsCompleted && !c.IsExpired && c.RemainingDays <= 1) currentDaily++;
            }
            if (currentDaily >= MAX_DAILY_CHALLENGES) return;

            float diffMul = GetDifficultyMultiplier();
            int gameDays = GameManager.Instance?.TimeManager?.CurrentDay ?? 1;
            float scaleFactor = 1f + (gameDays * 0.02f); // 日数経過で目標が少しずつ上昇

            // チャレンジタイプをランダム選択
            var types = new[] { ChallengeType.DailyVisitors, ChallengeType.DailyRevenue, ChallengeType.DailySatisfaction };

            for (int i = currentDaily; i < MAX_DAILY_CHALLENGES; i++)
            {
                var type = types[UnityEngine.Random.Range(0, types.Length)];
                Challenge challenge = CreateChallenge(type, diffMul, scaleFactor, 1);
                _activeChallenges.Add(challenge);
            }

            if (NotificationSystem.Instance != null)
                NotificationSystem.Instance.Notify("新しいデイリーチャレンジが追加されました！", NotifLevel.Info);
        }

        private void GenerateWeeklyChallenges()
        {
            int currentWeekly = 0;
            foreach (var c in _activeChallenges)
            {
                if (!c.IsCompleted && !c.IsExpired && c.RemainingDays > 1) currentWeekly++;
            }
            if (currentWeekly >= MAX_WEEKLY_CHALLENGES) return;

            float diffMul = GetDifficultyMultiplier();
            float scaleFactor = 1f + (_dayCounter * 0.01f);

            var types = new[] { ChallengeType.WeeklyVisitors, ChallengeType.WeeklyRevenue, ChallengeType.WeeklyRating };
            var type = types[UnityEngine.Random.Range(0, types.Length)];
            Challenge challenge = CreateChallenge(type, diffMul, scaleFactor, 7);
            _activeChallenges.Add(challenge);

            if (NotificationSystem.Instance != null)
                NotificationSystem.Instance.Notify("新しいウィークリーチャレンジが追加されました！", NotifLevel.Info);
        }

        private Challenge CreateChallenge(ChallengeType type, float diffMul, float scaleFactor, int days)
        {
            float target;
            float reward;
            int tickets = 0;
            string title;
            string desc;

            switch (type)
            {
                case ChallengeType.DailyVisitors:
                    target = Mathf.Round(BASE_DAILY_VISITOR_TARGET * diffMul * scaleFactor);
                    reward = target * 50f;
                    title = "来場者チャレンジ";
                    desc = $"本日中に来場者を{target:F0}人迎えよう";
                    break;
                case ChallengeType.DailyRevenue:
                    target = Mathf.Round(BASE_DAILY_REVENUE_TARGET * diffMul * scaleFactor / 100f) * 100f;
                    reward = target * 0.3f;
                    title = "売上チャレンジ";
                    desc = $"本日の売上を${target:N0}にしよう";
                    break;
                case ChallengeType.DailySatisfaction:
                    target = 70f + UnityEngine.Random.Range(0f, 15f);
                    reward = 1500f;
                    title = "満足度チャレンジ";
                    desc = $"来場者の平均満足度を{target:F0}%以上にしよう";
                    break;
                case ChallengeType.WeeklyVisitors:
                    target = Mathf.Round(BASE_WEEKLY_VISITOR_TARGET * diffMul * scaleFactor);
                    reward = target * 80f;
                    tickets = 1;
                    title = "週間来場者チャレンジ";
                    desc = $"今週中に来場者を合計{target:F0}人迎えよう";
                    break;
                case ChallengeType.WeeklyRevenue:
                    target = Mathf.Round(BASE_WEEKLY_REVENUE_TARGET * diffMul * scaleFactor / 500f) * 500f;
                    reward = target * 0.25f;
                    tickets = 1;
                    title = "週間売上チャレンジ";
                    desc = $"今週の売上を合計${target:N0}にしよう";
                    break;
                case ChallengeType.WeeklyRating:
                    target = 50f + UnityEngine.Random.Range(0f, 20f);
                    reward = 3000f;
                    tickets = 1;
                    title = "評価チャレンジ";
                    desc = $"パーク評価を{target:F0}以上にしよう";
                    break;
                default:
                    target = 10f;
                    reward = 500f;
                    title = "チャレンジ";
                    desc = "目標を達成しよう";
                    break;
            }

            return new Challenge
            {
                Id = _nextChallengeId++,
                Type = type,
                Title = title,
                Description = desc,
                TargetValue = target,
                CurrentValue = 0f,
                RewardMoney = reward,
                RewardTickets = tickets,
                IsCompleted = false,
                IsExpired = false,
                RemainingDays = days
            };
        }

        // ================================================================
        // 進捗更新
        // ================================================================

        private void UpdateChallengeProgress(ChallengeType type, float value)
        {
            for (int i = 0; i < _activeChallenges.Count; i++)
            {
                var c = _activeChallenges[i];
                if (c.IsCompleted || c.IsExpired) continue;
                if (c.Type != type) continue;

                c.CurrentValue = value;

                if (c.CurrentValue >= c.TargetValue && !c.IsCompleted)
                {
                    CompleteChallenge(c);
                }
            }
        }

        private void CompleteChallenge(Challenge c)
        {
            c.IsCompleted = true;
            _totalChallengesCompleted++;

            // 報酬付与
            if (c.RewardMoney > 0f && GameManager.Instance?.EconomyManager != null)
            {
                GameManager.Instance.EconomyManager.AddRevenue(c.RewardMoney, RevenueCategory.Other);
            }

            if (c.RewardTickets > 0 && GameManager.Instance != null)
            {
                for (int t = 0; t < c.RewardTickets; t++)
                    GameManager.Instance.AwardGoldenTicket();
            }

            _completedHistory.Add(c);

            string rewardText = $"${c.RewardMoney:N0}";
            if (c.RewardTickets > 0) rewardText += $" + ゴールデンチケット{c.RewardTickets}枚";

            if (NotificationSystem.Instance != null)
            {
                NotificationSystem.Instance.Notify(
                    $"チャレンジ達成！「{c.Title}」 報酬: {rewardText}",
                    NotifLevel.Success);
            }

            WebGLOptimizer.LogVerbose($"[ChallengeSystem] Challenge completed: {c.Title} (Reward: {rewardText})");
            RefreshUI();
        }

        private void ExpireDailyChallenges()
        {
            for (int i = _activeChallenges.Count - 1; i >= 0; i--)
            {
                var c = _activeChallenges[i];
                if (c.IsCompleted || c.IsExpired) continue;

                c.RemainingDays--;
                if (c.RemainingDays <= 0)
                {
                    c.IsExpired = true;
                    _activeChallenges.RemoveAt(i);
                }
            }
        }

        private void ExpireWeeklyChallenges()
        {
            for (int i = _activeChallenges.Count - 1; i >= 0; i--)
            {
                var c = _activeChallenges[i];
                if (c.IsCompleted) { _activeChallenges.RemoveAt(i); continue; }
                if (c.IsExpired) { _activeChallenges.RemoveAt(i); continue; }

                if (c.RemainingDays > 1)
                {
                    c.RemainingDays -= 7;
                    if (c.RemainingDays <= 0)
                    {
                        c.IsExpired = true;
                        _activeChallenges.RemoveAt(i);
                    }
                }
            }
        }

        private float GetDifficultyMultiplier()
        {
            if (GameManager.Instance == null) return 1f;
            switch (GameManager.Instance.CurrentDifficulty)
            {
                case GameDifficulty.Easy: return 0.7f;
                case GameDifficulty.Hard: return 1.4f;
                default: return 1f;
            }
        }

        // ================================================================
        // UI
        // ================================================================

        public void ShowChallengeUI()
        {
            if (_challengePanel == null) BuildUI();
            RefreshUI();
            _challengePanel.SetActive(true);
        }

        public void HideChallengeUI()
        {
            if (_challengePanel != null) _challengePanel.SetActive(false);
        }

        public void ToggleUI()
        {
            if (_challengePanel != null && _challengePanel.activeSelf)
                HideChallengeUI();
            else
                ShowChallengeUI();
        }

        private void RefreshUI()
        {
            if (_challengeListText == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=#FFE070>ACTIVE CHALLENGES</color>");
            sb.AppendLine("─────────────────────────────");

            int count = 0;
            foreach (var c in _activeChallenges)
            {
                if (c.IsCompleted || c.IsExpired) continue;
                count++;

                string progress = c.Type == ChallengeType.DailySatisfaction || c.Type == ChallengeType.WeeklyRating
                    ? $"{c.CurrentValue:F0}/{c.TargetValue:F0}"
                    : $"{c.CurrentValue:N0}/{c.TargetValue:N0}";

                string bar = MakeProgressBar(c.Progress, 12);
                string dayLabel = c.RemainingDays <= 1 ? "<color=#FF8888>今日中</color>" : $"残り{c.RemainingDays}日";

                sb.AppendLine($"  {c.Title} [{dayLabel}]");
                sb.AppendLine($"    {c.Description}");
                sb.AppendLine($"    {bar} {progress} ({c.Progress * 100f:F0}%)");
                sb.AppendLine($"    報酬: ${c.RewardMoney:N0}" + (c.RewardTickets > 0 ? $" +チケット{c.RewardTickets}" : ""));
                sb.AppendLine();
            }

            if (count == 0)
            {
                sb.AppendLine("  チャレンジはありません");
            }

            sb.AppendLine($"\n<color=#88AACC>達成数: {_totalChallengesCompleted}</color>");

            _challengeListText.text = sb.ToString();
        }

        private string MakeProgressBar(float ratio, int width)
        {
            int filled = Mathf.RoundToInt(ratio * width);
            string bar = "[";
            for (int i = 0; i < width; i++)
                bar += i < filled ? "#" : "-";
            bar += "]";
            return bar;
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("ChallengeCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 93;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            _challengePanel = new GameObject("ChallengePanel");
            _challengePanel.transform.SetParent(canvasGo.transform, false);
            var panelRt = _challengePanel.AddComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(460f, 420f);
            var bg = _challengePanel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.06f, 0.14f, 0.96f);
            bg.raycastTarget = true;

            // タイトル
            MakeLabel(panelRt, "Title", "CHALLENGES", 22,
                new Color(0.95f, 0.88f, 0.45f), FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, 185f), new Vector2(420f, 32f));

            // チャレンジリスト
            _challengeListText = MakeLabel(panelRt, "List", "", 13,
                new Color(0.85f, 0.88f, 0.92f), FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0f, 10f), new Vector2(420f, 320f));

            // 閉じるボタン
            MakeBtn(panelRt, "CloseBtn", "CLOSE",
                new Color(0.5f, 0.25f, 0.2f),
                new Vector2(0f, -190f), new Vector2(120f, 32f),
                HideChallengeUI);

            _challengePanel.SetActive(false);
        }

        private Text MakeLabel(RectTransform p, string name, string text, int size,
            Color color, FontStyle style, TextAnchor anchor, Vector2 pos, Vector2 sz)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = GetFont();
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = anchor;
            return t;
        }

        private void MakeBtn(RectTransform p, string name, string label,
            Color bgColor, Vector2 pos, Vector2 sz, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform, false);
            var lblRt = lbl.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            var t = lbl.AddComponent<Text>();
            t.text = label;
            t.font = GetFont();
            t.fontSize = 15;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
        }

        private static Font GetFont()
        {
            var f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
