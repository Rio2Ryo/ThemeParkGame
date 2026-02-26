// ============================================================
// ThemeParkGame - RivalParkSystem
// 競合パーク出現・価格競争・差別化システム
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    /// <summary>ライバルパークの戦略タイプ</summary>
    public enum RivalStrategy
    {
        PriceCutter,     // 低価格路線
        QualityFocused,  // 高品質路線
        EventDriven,     // イベント重視
        FamilyOriented,  // ファミリー向け
        ThrillSeeker     // スリル重視
    }

    /// <summary>ライバルパーク情報</summary>
    [Serializable]
    public class RivalPark
    {
        public int Id;
        public string Name;
        public RivalStrategy Strategy;
        public float Rating;           // 0-100
        public float PriceLevel;       // 0.5-2.0 (プレイヤー基準比)
        public float AttractionCount;
        public float VisitorStealRate; // 0-0.3 (プレイヤーからの奪取率)
        public float GrowthRate;       // 月間成長率
        public bool IsActive;
        public int AppearMonth;        // 出現月

        // 各指標のスコア（0-100）
        public float SafetyScore;
        public float ExcitementScore;
        public float ComfortScore;
        public float ValueScore;       // コストパフォーマンス

        /// <summary>ライバルの総合スコア</summary>
        public float OverallScore => (Rating + SafetyScore + ExcitementScore + ComfortScore + ValueScore) / 5f;
    }

    /// <summary>
    /// ライバルテーマパークの出現と競争を管理するシステム。
    ///
    /// 【ゲームデザイン】
    /// ・ゲーム進行に応じてライバルパークが出現
    /// ・ライバルは独自の戦略を持ち、プレイヤーの来場者を奪う
    /// ・プレイヤーはライバルに対して差別化戦略が必要:
    ///   - 価格競争: 入場料・チケット価格の調整
    ///   - 品質競争: 評価を高めてブランド力で勝負
    ///   - イベント競争: 特別イベントで集客
    /// ・ライバルの弱点を突くことで有利に立てる
    /// ・ライバルに勝つとゴールデンチケット獲得
    /// </summary>
    public class RivalParkSystem : MonoBehaviour
    {
        private readonly List<RivalPark> _rivals = new List<RivalPark>();
        private int _nextId = 1;
        private float _monthTimer;
        private float _competitionPenalty; // 来場者減少率 (0-0.3)

        // UI
        private GameObject _uiPanel;
        private Text _summaryText;
        private Text _detailText;
        private bool _visible;

        public static RivalParkSystem Instance { get; private set; }

        public IReadOnlyList<RivalPark> Rivals => _rivals;
        public float CompetitionPenalty => _competitionPenalty;

        // ライバルからの集客影響
        public float GetVisitorMultiplier()
        {
            return Mathf.Max(0.5f, 1f - _competitionPenalty);
        }

        private static readonly string[] RivalNames = {
            "FunWorld", "DreamLand", "Adventure Kingdom", "Star Park",
            "Wonder World", "Fantasy Island", "Mega Park", "Joy Land",
            "Thrill City", "Magic Gardens"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.OnParkYearPassed += OnYearPassed;
        }

        private void OnDisable()
        {
            GameEvents.OnParkYearPassed -= OnYearPassed;
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            float dt = Time.deltaTime;
            _monthTimer += dt;

            // ゲーム内1ヶ月ごとにライバル更新（5秒/h × 24h × 30d = 3600秒 ≈ 60分）
            // 簡易化: 60秒ごと（=ゲーム内約12時間ごと）
            if (_monthTimer >= 60f)
            {
                _monthTimer = 0f;
                UpdateRivals();
                CheckNewRivalSpawn();
            }

            // 競争ペナルティ計算
            CalculateCompetitionPenalty();
        }

        // ================================================================
        // ライバル管理
        // ================================================================

        private void CheckNewRivalSpawn()
        {
            var tm = GameManager.Instance?.TimeManager;
            if (tm == null) return;

            int totalMonths = (tm.CurrentYear - 1) * 12 + tm.CurrentMonth;

            // ゲーム開始3ヶ月後から最初のライバル
            if (_rivals.Count == 0 && totalMonths >= 3)
            {
                SpawnRival();
            }
            // 6ヶ月ごとに新ライバル（最大3つ）
            else if (_rivals.Count < 3 && totalMonths >= 3 + _rivals.Count * 6)
            {
                SpawnRival();
            }
        }

        private void SpawnRival()
        {
            var strategies = (RivalStrategy[])Enum.GetValues(typeof(RivalStrategy));
            var strategy = strategies[UnityEngine.Random.Range(0, strategies.Length)];
            string name = RivalNames[UnityEngine.Random.Range(0, RivalNames.Length)];

            // プレイヤーより少し低い初期値
            float playerRating = GameManager.Instance?.ParkManager?.Rating?.OverallRating ?? 50f;

            var rival = new RivalPark
            {
                Id = _nextId++,
                Name = name,
                Strategy = strategy,
                Rating = Mathf.Max(20f, playerRating * UnityEngine.Random.Range(0.5f, 0.8f)),
                PriceLevel = strategy == RivalStrategy.PriceCutter ? 0.7f : UnityEngine.Random.Range(0.8f, 1.3f),
                AttractionCount = UnityEngine.Random.Range(3, 8),
                VisitorStealRate = 0.05f,
                GrowthRate = UnityEngine.Random.Range(0.5f, 2f),
                IsActive = true,
                AppearMonth = GameManager.Instance?.TimeManager?.CurrentMonth ?? 1,
                SafetyScore = UnityEngine.Random.Range(40f, 70f),
                ExcitementScore = strategy == RivalStrategy.ThrillSeeker ?
                    UnityEngine.Random.Range(60f, 90f) : UnityEngine.Random.Range(30f, 70f),
                ComfortScore = strategy == RivalStrategy.FamilyOriented ?
                    UnityEngine.Random.Range(60f, 85f) : UnityEngine.Random.Range(30f, 65f),
                ValueScore = strategy == RivalStrategy.PriceCutter ?
                    UnityEngine.Random.Range(70f, 90f) : UnityEngine.Random.Range(40f, 70f)
            };

            _rivals.Add(rival);

            GameManager.Instance?.ShowNotification(
                $"競合パーク「{name}」が近くにオープン！({strategy})", NotifLevel.Warning);

            WebGLOptimizer.LogVerbose($"[RivalPark] New rival: {name} ({strategy})");
        }

        private void UpdateRivals()
        {
            float playerRating = GameManager.Instance?.ParkManager?.Rating?.OverallRating ?? 50f;

            foreach (var rival in _rivals)
            {
                if (!rival.IsActive) continue;

                // ライバルの成長
                rival.Rating = Mathf.Min(95f, rival.Rating + rival.GrowthRate * 0.5f);
                rival.AttractionCount += UnityEngine.Random.Range(0f, 0.3f);

                // 各スコアの微変動
                rival.SafetyScore = Mathf.Clamp(rival.SafetyScore + UnityEngine.Random.Range(-2f, 3f), 10f, 95f);
                rival.ExcitementScore = Mathf.Clamp(rival.ExcitementScore + UnityEngine.Random.Range(-2f, 3f), 10f, 95f);
                rival.ComfortScore = Mathf.Clamp(rival.ComfortScore + UnityEngine.Random.Range(-2f, 3f), 10f, 95f);
                rival.ValueScore = Mathf.Clamp(rival.ValueScore + UnityEngine.Random.Range(-2f, 3f), 10f, 95f);

                // プレイヤーより評価が高いとvisitor steal率が上がる
                float ratingDiff = rival.OverallScore - playerRating;
                rival.VisitorStealRate = Mathf.Clamp(ratingDiff / 200f + 0.05f, 0f, 0.3f);

                // プレイヤーに大きく負けているライバルは撤退
                if (rival.Rating < playerRating * 0.3f && UnityEngine.Random.value < 0.1f)
                {
                    rival.IsActive = false;
                    GameManager.Instance?.ShowNotification(
                        $"競合「{rival.Name}」が閉園しました！", NotifLevel.Success);
                }
            }
        }

        private void CalculateCompetitionPenalty()
        {
            float totalSteal = 0f;
            foreach (var rival in _rivals)
            {
                if (rival.IsActive)
                    totalSteal += rival.VisitorStealRate;
            }
            _competitionPenalty = Mathf.Min(0.4f, totalSteal);
        }

        private void OnYearPassed(int year)
        {
            // プレイヤーがライバル全員に勝っていたらゴールデンチケット
            bool allBeaten = true;
            foreach (var rival in _rivals)
            {
                if (rival.IsActive)
                {
                    float playerRating = GameManager.Instance?.ParkManager?.Rating?.OverallRating ?? 0f;
                    if (rival.OverallScore >= playerRating)
                    {
                        allBeaten = false;
                        break;
                    }
                }
            }

            if (allBeaten && _rivals.Count > 0)
            {
                GameManager.Instance?.AwardGoldenTicket();
                GameManager.Instance?.ShowNotification(
                    "全ライバルを凌駕！ゴールデンチケット獲得！", NotifLevel.Success);
            }
        }

        // ================================================================
        // UI
        // ================================================================

        public void ToggleUI()
        {
            if (_visible) HideUI(); else ShowUI();
        }

        public void ShowUI()
        {
            if (_uiPanel == null) BuildUI();
            _uiPanel.SetActive(true);
            _visible = true;
            RefreshUI();
        }

        public void HideUI()
        {
            if (_uiPanel != null) _uiPanel.SetActive(false);
            _visible = false;
        }

        private void BuildUI()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _uiPanel = new GameObject("RivalParkPanel");
            _uiPanel.transform.SetParent(canvas.transform, false);
            var rt = _uiPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.15f, 0.1f);
            rt.anchorMax = new Vector2(0.85f, 0.9f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bg = _uiPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);

            MakeText(_uiPanel.transform, "Title",
                new Vector2(0.02f, 0.92f), new Vector2(0.8f, 1f),
                "Rival Parks - Competition", 20, FontStyle.Bold, Color.white);

            MakeBtn(_uiPanel.transform, "Close",
                new Vector2(0.9f, 0.93f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            _summaryText = MakeText(_uiPanel.transform, "Summary",
                new Vector2(0.02f, 0.84f), new Vector2(0.98f, 0.92f),
                "", 14, FontStyle.Bold, new Color(0.9f, 0.85f, 0.5f));

            _detailText = MakeText(_uiPanel.transform, "Detail",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.82f),
                "", 13, FontStyle.Normal, new Color(0.7f, 0.75f, 0.85f));
            _detailText.alignment = TextAnchor.UpperLeft;

            _uiPanel.SetActive(false);
        }

        private void RefreshUI()
        {
            if (_summaryText != null)
            {
                int active = 0;
                foreach (var r in _rivals) if (r.IsActive) active++;
                _summaryText.text = $"アクティブライバル: {active} | 競争による来場者減: -{_competitionPenalty * 100f:F0}%";
            }

            if (_detailText != null)
            {
                var sb = new System.Text.StringBuilder();
                float playerRating = GameManager.Instance?.ParkManager?.Rating?.OverallRating ?? 0f;
                sb.AppendLine($"あなたのパーク評価: {playerRating:F0}/100\n");

                foreach (var rival in _rivals)
                {
                    string status = rival.IsActive ? "OPEN" : "CLOSED";
                    string vs = rival.OverallScore > playerRating ? "<<負けてます>>" :
                               rival.OverallScore < playerRating * 0.7f ? "<<圧勝中>>" : "<<互角>>";

                    sb.AppendLine($"--- {rival.Name} [{status}] ---");
                    sb.AppendLine($"  戦略: {rival.Strategy} | 評価: {rival.Rating:F0}");
                    sb.AppendLine($"  安全:{rival.SafetyScore:F0} 興奮:{rival.ExcitementScore:F0} " +
                        $"快適:{rival.ComfortScore:F0} CP:{rival.ValueScore:F0}");
                    sb.AppendLine($"  総合: {rival.OverallScore:F0} {vs}");
                    sb.AppendLine($"  来場者奪取率: {rival.VisitorStealRate * 100f:F1}%");
                    sb.AppendLine();
                }

                if (_rivals.Count == 0)
                    sb.AppendLine("まだライバルは出現していません。\nゲーム3ヶ月後に最初のライバルが出現します。");

                _detailText.text = sb.ToString();
            }
        }

        // UIヘルパー
        private Text MakeText(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string content, int size, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = new Vector2(5f, 0f); r.offsetMax = new Vector2(-5f, 0f);
            var t = obj.AddComponent<Text>();
            t.text = content; t.font = FontManager.Regular;
            t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            return t;
        }

        private void MakeBtn(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string text, Color bg,
            UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>(); img.color = bg;
            var btn = obj.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var txt = new GameObject("Text").AddComponent<Text>();
            txt.transform.SetParent(obj.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            txt.font = FontManager.Regular;
            txt.fontSize = 14; txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter; txt.text = text;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
