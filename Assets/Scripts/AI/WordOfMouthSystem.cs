// ============================================================
// ThemeParkGame - WordOfMouthSystem
// 口コミ・SNS投稿による集客強化システム
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;

namespace ThemeParkGame.AI
{
    /// <summary>口コミ投稿の種別</summary>
    public enum ReviewType
    {
        Positive,   // 好意的レビュー
        Neutral,    // 普通のレビュー
        Negative,   // 否定的レビュー
        Viral       // バズった投稿（影響力大）
    }

    /// <summary>口コミ投稿データ</summary>
    [Serializable]
    public class VisitorReview
    {
        public int ReviewId;
        public string AuthorName;
        public string Content;
        public ReviewType Type;
        public float ImpactScore;   // -10 ~ +10
        public float Timestamp;     // ゲーム内時間
        public int Likes;
        public bool IsViral;
    }

    /// <summary>
    /// 来場者の体験に基づいてSNS口コミを自動生成し、
    /// パークの集客力に影響を与えるシステム。
    ///
    /// 【ゲームデザイン】
    /// ・来場者が退園時に体験を口コミとして投稿
    /// ・満足度が高い→好意的投稿→口コミスコアUP→スポーン率UP
    /// ・満足度が低い→否定的投稿→口コミスコアDOWN→スポーン率DOWN
    /// ・稀にバズ投稿が発生し、大幅に集客が増加
    /// ・口コミスコアはSNSReputationSystemと連動
    /// </summary>
    public class WordOfMouthSystem : MonoBehaviour
    {
        private const int MAX_REVIEWS = 50;
        private const float VIRAL_CHANCE = 0.03f;
        private const float VIRAL_MULTIPLIER = 5f;

        private readonly List<VisitorReview> _reviews = new List<VisitorReview>();
        private int _nextReviewId = 1;
        private float _wordOfMouthScore = 50f; // 0-100
        private float _spawnBonus = 1f;

        // テンプレート
        private static readonly string[] PositiveTemplates = {
            "{name}: 最高のテーマパーク！特にアトラクションが素晴らしかった！",
            "{name}: 家族で楽しめる最高の場所！また来たい！",
            "{name}: スタッフの対応が素晴らしい。評価★★★★★",
            "{name}: 友達全員に勧めたい！コスパ最高！",
            "{name}: 期待以上の体験でした。食事も美味しかった！",
            "{name}: 絶叫系が最高！待ち時間も短くて満足！",
            "{name}: 園内が清潔で快適。子連れにもおすすめ！",
            "{name}: 特別イベントが最高でした！感動！",
        };

        private static readonly string[] NeutralTemplates = {
            "{name}: まあまあのテーマパーク。普通に楽しめた。",
            "{name}: 良いところもあるけど改善点もある。3点。",
            "{name}: アトラクションは良かったけど食事がイマイチ。",
            "{name}: 天気が悪くて残念だったけどそれなりに楽しめた。",
        };

        private static readonly string[] NegativeTemplates = {
            "{name}: 待ち時間が長すぎ！もう来ない！",
            "{name}: 料金に見合わない。がっかり。",
            "{name}: スタッフの対応が悪い。改善を望む。",
            "{name}: 汚い。トイレの数が足りない。最低。",
            "{name}: アトラクションが故障中ばかり。金返せ。",
            "{name}: 混みすぎで全然楽しめなかった。",
        };

        private static readonly string[] ViralTemplates = {
            "{name}: 【衝撃】このテーマパークが神すぎた件www 1000いいね",
            "{name}: 【拡散希望】穴場のテーマパーク発見！最高すぎ！",
            "{name}: 【バズ】テーマパークで奇跡の体験をしたので聞いてほしい",
        };

        // UI
        private GameObject _uiPanel;
        private Text _scoreLabel;
        private Text _reviewListText;
        private bool _visible;

        public static WordOfMouthSystem Instance { get; private set; }

        public float WordOfMouthScore => _wordOfMouthScore;
        public float SpawnMultiplier => _spawnBonus;
        public IReadOnlyList<VisitorReview> RecentReviews => _reviews;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.OnVisitorLeavePark += OnVisitorLeave;
        }

        private void OnDisable()
        {
            GameEvents.OnVisitorLeavePark -= OnVisitorLeave;
        }

        private void Update()
        {
            // スポーンボーナス計算
            float normalized = _wordOfMouthScore / 100f;
            _spawnBonus = Mathf.Lerp(0.6f, 1.8f, normalized);

            // 口コミスコアを徐々にニュートラルに近づける
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                float decay = 0.02f * Time.deltaTime;
                if (_wordOfMouthScore > 50f)
                    _wordOfMouthScore = Mathf.Max(50f, _wordOfMouthScore - decay);
                else if (_wordOfMouthScore < 50f)
                    _wordOfMouthScore = Mathf.Min(50f, _wordOfMouthScore + decay);
            }
        }

        // ================================================================
        // 口コミ生成
        // ================================================================

        private void OnVisitorLeave(int visitorId)
        {
            // 30%の確率で投稿
            if (UnityEngine.Random.value > 0.3f) return;

            var vm = GameManager.Instance?.VisitorManager;
            if (vm == null) return;

            // 来場者の満足度からレビュータイプを決定
            float happiness = vm.AverageHappiness;
            float satisfaction = vm.AverageSatisfaction;
            float avgScore = (happiness + satisfaction) / 2f;

            ReviewType type;
            string[] templates;
            float impact;

            // バズチャンス
            if (avgScore > 75f && UnityEngine.Random.value < VIRAL_CHANCE)
            {
                type = ReviewType.Viral;
                templates = ViralTemplates;
                impact = UnityEngine.Random.Range(5f, 10f) * VIRAL_MULTIPLIER;
            }
            else if (avgScore >= 70f)
            {
                type = ReviewType.Positive;
                templates = PositiveTemplates;
                impact = UnityEngine.Random.Range(1f, 5f);
            }
            else if (avgScore >= 40f)
            {
                type = ReviewType.Neutral;
                templates = NeutralTemplates;
                impact = UnityEngine.Random.Range(-1f, 1f);
            }
            else
            {
                type = ReviewType.Negative;
                templates = NegativeTemplates;
                impact = UnityEngine.Random.Range(-5f, -1f);
            }

            string authorName = GenerateReviewerName();
            string template = templates[UnityEngine.Random.Range(0, templates.Length)];
            string content = template.Replace("{name}", authorName);

            var review = new VisitorReview
            {
                ReviewId = _nextReviewId++,
                AuthorName = authorName,
                Content = content,
                Type = type,
                ImpactScore = impact,
                Timestamp = Time.time,
                Likes = type == ReviewType.Viral ? UnityEngine.Random.Range(500, 2000) :
                         type == ReviewType.Positive ? UnityEngine.Random.Range(5, 50) :
                         UnityEngine.Random.Range(0, 10),
                IsViral = type == ReviewType.Viral
            };

            _reviews.Add(review);
            if (_reviews.Count > MAX_REVIEWS) _reviews.RemoveAt(0);

            // 口コミスコア更新
            _wordOfMouthScore = Mathf.Clamp(_wordOfMouthScore + impact * 0.5f, 0f, 100f);

            // SNSReputationSystemとも連動
            if (SNSReputationSystem.Instance != null)
            {
                float sentScore = Mathf.Clamp01((impact + 10f) / 20f);
                SNSReputationSystem.Instance.AddTemplatePost(visitorId, authorName, content,
                    type == ReviewType.Negative ? PostSentiment.Negative :
                    type == ReviewType.Neutral ? PostSentiment.Neutral : PostSentiment.Positive,
                    sentScore);
            }

            if (type == ReviewType.Viral)
            {
                GameManager.Instance?.ShowNotification(
                    $"口コミがバズりました！集客UP！", NotifLevel.Success);
            }

            WebGLOptimizer.LogVerbose($"[WordOfMouth] Review: {type} impact={impact:F1} score={_wordOfMouthScore:F0}");
        }

        private string GenerateReviewerName()
        {
            string[] names = {
                "Yuki", "Hana", "Taro", "Sakura", "Ken", "Mika", "Ryo", "Aoi",
                "Alex", "Sam", "Pat", "Kim", "Chris", "Morgan", "Jordan", "Taylor",
                "Luna", "Kai", "Rin", "Sora", "Mei", "Haruto", "Hinata", "Yuto"
            };
            return names[UnityEngine.Random.Range(0, names.Length)];
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

            _uiPanel = new GameObject("WordOfMouthPanel");
            _uiPanel.transform.SetParent(canvas.transform, false);
            var rt = _uiPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.1f);
            rt.anchorMax = new Vector2(0.8f, 0.9f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bg = _uiPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);

            MakeText(_uiPanel.transform, "Title",
                new Vector2(0.02f, 0.92f), new Vector2(0.8f, 1f),
                "Word of Mouth - SNS Reviews", 20, FontStyle.Bold, Color.white);

            MakeBtn(_uiPanel.transform, "Close",
                new Vector2(0.9f, 0.93f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            _scoreLabel = MakeText(_uiPanel.transform, "Score",
                new Vector2(0.02f, 0.84f), new Vector2(0.98f, 0.92f),
                "", 16, FontStyle.Bold, new Color(0.9f, 0.85f, 0.5f));

            _reviewListText = MakeText(_uiPanel.transform, "Reviews",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.82f),
                "", 13, FontStyle.Normal, new Color(0.7f, 0.75f, 0.85f));
            _reviewListText.alignment = TextAnchor.UpperLeft;

            _uiPanel.SetActive(false);
        }

        private void RefreshUI()
        {
            if (_scoreLabel != null)
            {
                string trend = _spawnBonus >= 1.2f ? "UP" : _spawnBonus <= 0.8f ? "DOWN" : "---";
                _scoreLabel.text = $"口コミスコア: {_wordOfMouthScore:F0}/100 | " +
                    $"集客倍率: x{_spawnBonus:F2} | トレンド: {trend}";
            }

            if (_reviewListText != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = _reviews.Count - 1; i >= Mathf.Max(0, _reviews.Count - 15); i--)
                {
                    var r = _reviews[i];
                    string icon = r.Type switch
                    {
                        ReviewType.Viral => "[BUZZ]",
                        ReviewType.Positive => "[+]",
                        ReviewType.Negative => "[-]",
                        _ => "[=]"
                    };
                    sb.AppendLine($"{icon} {r.Content} ({r.Likes} likes)");
                }
                _reviewListText.text = sb.ToString();
            }
        }

        // ================================================================
        // UIヘルパー
        // ================================================================

        private Text MakeText(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string content, int size, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = new Vector2(5f, 0f); r.offsetMax = new Vector2(-5f, 0f);
            var t = obj.AddComponent<Text>();
            t.text = content; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 14; txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter; txt.text = text;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
