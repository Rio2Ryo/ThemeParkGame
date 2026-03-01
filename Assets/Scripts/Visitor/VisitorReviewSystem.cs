using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.AI;

namespace ThemeParkGame.Visitor
{
    [System.Serializable]
    public class DetailedReview
    {
        public int ReviewId;
        public int VisitorId;
        public string AuthorName;
        public string VisitorTypeName;
        public float OverallRating;
        public float Happiness;
        public string ReviewText;
        public string BestExperience;
        public string WorstExperience;
        public List<string> Tags;
        public float Timestamp;
        public int HelpfulCount;
        public PostSentiment Sentiment;

        public DetailedReview()
        {
            Tags = new List<string>();
            BestExperience = "";
            WorstExperience = "";
            ReviewText = "";
            AuthorName = "";
            VisitorTypeName = "";
        }
    }

    [System.Serializable]
    public class ReviewDashboard
    {
        public float AverageRating;
        public int TotalReviews;
        public Dictionary<string, int> TagCounts;
        public float PositiveRate;
        public float NeutralRate;
        public float NegativeRate;
        public string MostMentionedPositive;
        public string MostMentionedNegative;

        public ReviewDashboard()
        {
            TagCounts = new Dictionary<string, int>();
            MostMentionedPositive = "";
            MostMentionedNegative = "";
        }
    }

    /// <summary>
    /// 来園者レビューシステム（シングルトン）。退園時にテンプレートベースの
    /// 日本語レビューを自動生成し、ダッシュボード集計とSNS連携を行う。
    /// </summary>
    public class VisitorReviewSystem : MonoBehaviour
    {
        public static VisitorReviewSystem Instance { get; private set; }
        private const int MaxReviews = 200;
        private const float ReviewChance = 0.4f;
        private List<DetailedReview> _reviews = new List<DetailedReview>();
        private int _nextReviewId = 1;
        public ReviewDashboard Dashboard { get; private set; } = new ReviewDashboard();
        public event Action<DetailedReview> OnNewReview;

        // --- 開始文テンプレート（ポジティブ 8 / ニュートラル 6 / ネガティブ 6 = 20） ---
        private static readonly string[] OpeningPositive = {
            "最高の1日でした！", "家族みんなで大満足の1日になりました！",
            "期待以上の楽しさでした！", "何度来ても飽きないテーマパークです！",
            "素晴らしい体験ができました！", "大人も子どもも楽しめる最高のパークです！",
            "今まで行ったテーマパークの中で一番です！", "友達にもぜひおすすめしたいパークです！"
        };
        private static readonly string[] OpeningNeutral = {
            "普通に楽しめました。", "まあまあの体験でした。",
            "悪くはないですが、特別良くもなかったです。", "期待通りという感じでした。",
            "可もなく不可もなくといったところです。", "それなりに楽しめましたが改善点もあります。"
        };
        private static readonly string[] OpeningNegative = {
            "残念な体験でした。", "正直がっかりしました。",
            "期待外れの1日でした。", "もう少し改善してほしいです。",
            "今回は楽しめませんでした。", "お金を払った価値を感じませんでした。"
        };

        // --- アトラクション言及テンプレート（称賛 5 / 不満 5） ---
        private static readonly string[] AttractionPraise = {
            "特に『{0}』が素晴らしかった！", "『{0}』は本当に最高でした！また乗りたい！",
            "『{0}』のクオリティに感動しました。", "一番のお気に入りは『{0}』です。",
            "『{0}』は待ってでも乗る価値があります！"
        };
        private static readonly string[] AttractionComplaint = {
            "『{0}』は期待外れでした。", "『{0}』はもう少し改善が必要だと思います。",
            "『{0}』にはがっかりしました。", "『{0}』は正直おすすめできません。",
            "『{0}』は料金に見合っていないと感じました。"
        };

        // --- コンテキスト別不満テンプレート ---
        private static readonly string[] HungerComplaints = {
            "食べ物の選択肢が少ない気がしました。",
            "フードコートがもっと充実していたら良かったです。",
            "食事の種類が限られていて残念でした。"
        };
        private static readonly string[] ThirstComplaints = {
            "飲み物が高いと感じました。",
            "ドリンクの値段をもう少し抑えてほしいです。",
            "飲料の自販機がもっとあればいいのに。"
        };
        private static readonly string[] DirtyComplaints = {
            "清掃が行き届いていない場所がありました。",
            "ゴミが散らかっている場所が目立ちました。",
            "パーク内の清潔さにもう少し気を配ってほしいです。"
        };
        private static readonly string[] LongWaitComplaints = {
            "待ち時間が長すぎると感じました。",
            "人気アトラクションの待ち時間が非常に長かったです。",
            "もう少し効率的に運営してほしいです。"
        };

        // --- 締めくくりテンプレート ---
        private static readonly string[] ClosingPositive = {
            "また絶対来ます！", "次回も楽しみにしています！",
            "最高の思い出ができました。ありがとう！",
            "家族や友人にもおすすめしたいです！", "年パスを買おうか検討中です！"
        };
        private static readonly string[] ClosingNeutral = {
            "機会があればまた来るかもしれません。", "改善されたらまた訪れたいです。",
            "次回に期待したいと思います。", "もう少し工夫があれば良いパークになると思います。"
        };
        private static readonly string[] ClosingNegative = {
            "次回は他のパークに行こうと思います。", "しばらくは来ないと思います。",
            "改善を強く望みます。", "友人にはおすすめしにくいです。"
        };

        // --- タグ定義 ---
        private static readonly string[] PositiveTags = {
            "アトラクション充実", "清潔感あり", "スタッフ対応良い", "料理がおいしい",
            "景観が美しい", "子ども向け充実", "コスパ良い", "待ち時間短い",
            "雰囲気が良い", "リピート確定"
        };
        private static readonly string[] NegativeTags = {
            "待ち時間長い", "清掃不足", "食事が高い", "飲み物が高い",
            "アトラクション少ない", "混雑しすぎ", "スタッフ不足", "休憩場所少ない",
            "案内がわかりにくい", "期待外れ"
        };

        // --- 著者名テンプレート ---
        private static readonly string[] AuthorFirstNames = {
            "ゆうき", "はるか", "たくみ", "さくら", "れん", "みお", "そうた",
            "あおい", "こうへい", "ひなた", "かいと", "めい", "りく", "ゆい",
            "はると", "あかり", "そら", "ももか", "だいち", "ことね"
        };
        private static readonly string[] AuthorSuffixes = { "さん", "ママ", "パパ", "ファミリー", "" };

        private struct ReviewContext
        {
            public bool IsHungry, IsThirsty, SawDirtyAreas, ExperiencedLongWait;
            public int AttractionsRidden, ShopsVisited;
            public bool IsHighHappiness, IsLowHappiness;
        }

        // =====================================================================
        // ライフサイクル
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()  { GameEvents.OnVisitorLeavePark += HandleVisitorLeavePark; }
        private void OnDisable() { GameEvents.OnVisitorLeavePark -= HandleVisitorLeavePark; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameEvents.OnVisitorLeavePark -= HandleVisitorLeavePark;
        }

        // =====================================================================
        // 退園時ハンドラ
        // =====================================================================

        private void HandleVisitorLeavePark(int visitorId)
        {
            if (UnityEngine.Random.value > ReviewChance) return;
            try
            {
                DetailedReview review = GenerateReview(visitorId);
                if (review != null) AddReview(review);
            }
            catch (System.Exception ex)
            {
                WebGLOptimizer.LogVerbose($"[VisitorReviewSystem] レビュー生成エラー: {ex.Message}");
            }
        }

        // =====================================================================
        // レビュー生成
        // =====================================================================

        public DetailedReview GenerateReview(int visitorId)
        {
            VisitorAI visitorAI = FindVisitorAI(visitorId);
            if (visitorAI == null)
            {
                WebGLOptimizer.LogVerbose($"[VisitorReviewSystem] VisitorAI が見つかりません: {visitorId}");
                return null;
            }

            var parameters = visitorAI.GetComponent<VisitorParameters>();
            var profile = visitorAI.GetComponent<VisitorProfile>();
            if (parameters == null)
            {
                WebGLOptimizer.LogVerbose($"[VisitorReviewSystem] VisitorParameters が見つかりません: {visitorId}");
                return null;
            }

            float happiness = Mathf.Clamp(parameters.Happiness, 0f, 100f);
            float overallRating = MapHappinessToRating(happiness);
            PostSentiment sentiment = DetermineSentiment(happiness);

            var review = new DetailedReview
            {
                ReviewId = _nextReviewId++,
                VisitorId = visitorId,
                AuthorName = GenerateAuthorName(),
                VisitorTypeName = profile != null ? profile.VisitorTypeName : "一般来園者",
                OverallRating = overallRating,
                Happiness = happiness,
                Timestamp = Time.time,
                HelpfulCount = UnityEngine.Random.Range(0, 15),
                Sentiment = sentiment,
                BestExperience = GetExperience(parameters, true),
                WorstExperience = GetExperience(parameters, false)
            };

            var ctx = GatherContext(parameters);
            review.ReviewText = BuildReviewText(happiness, review.BestExperience,
                review.WorstExperience, ctx, sentiment);
            review.Tags = GenerateTags(happiness, ctx, review.BestExperience, review.WorstExperience);

            WebGLOptimizer.LogVerbose(
                $"[VisitorReviewSystem] レビュー生成: ID={review.ReviewId}, " +
                $"Visitor={visitorId}, Rating={overallRating:F1}, Sentiment={sentiment}");
            return review;
        }

        private void AddReview(DetailedReview review)
        {
            _reviews.Add(review);
            while (_reviews.Count > MaxReviews) _reviews.RemoveAt(0);
            UpdateDashboard();
            BridgeToSNSSystem(review);
            OnNewReview?.Invoke(review);
            WebGLOptimizer.LogVerbose(
                $"[VisitorReviewSystem] レビュー追加完了: ID={review.ReviewId}, 総数={_reviews.Count}");
        }

        // =====================================================================
        // ダッシュボード更新
        // =====================================================================

        public void UpdateDashboard()
        {
            if (_reviews.Count == 0) { Dashboard = new ReviewDashboard(); return; }

            var dash = new ReviewDashboard { TotalReviews = _reviews.Count };
            float ratingSum = 0f;
            int posCount = 0, neuCount = 0, negCount = 0;
            var posTagCounts = new Dictionary<string, int>();
            var negTagCounts = new Dictionary<string, int>();

            for (int i = 0; i < _reviews.Count; i++)
            {
                var r = _reviews[i];
                ratingSum += r.OverallRating;
                switch (r.Sentiment)
                {
                    case PostSentiment.Positive: posCount++; break;
                    case PostSentiment.Neutral:  neuCount++; break;
                    case PostSentiment.Negative: negCount++; break;
                }
                for (int t = 0; t < r.Tags.Count; t++)
                {
                    string tag = r.Tags[t];
                    if (!dash.TagCounts.ContainsKey(tag)) dash.TagCounts[tag] = 0;
                    dash.TagCounts[tag]++;
                    if (IsPositiveTag(tag))
                    {
                        if (!posTagCounts.ContainsKey(tag)) posTagCounts[tag] = 0;
                        posTagCounts[tag]++;
                    }
                    else if (IsNegativeTag(tag))
                    {
                        if (!negTagCounts.ContainsKey(tag)) negTagCounts[tag] = 0;
                        negTagCounts[tag]++;
                    }
                }
            }

            dash.AverageRating = ratingSum / _reviews.Count;
            dash.PositiveRate = (float)posCount / _reviews.Count;
            dash.NeutralRate = (float)neuCount / _reviews.Count;
            dash.NegativeRate = (float)negCount / _reviews.Count;
            dash.MostMentionedPositive = posTagCounts.Count > 0
                ? posTagCounts.OrderByDescending(kv => kv.Value).First().Key : "";
            dash.MostMentionedNegative = negTagCounts.Count > 0
                ? negTagCounts.OrderByDescending(kv => kv.Value).First().Key : "";
            Dashboard = dash;

            WebGLOptimizer.LogVerbose(
                $"[VisitorReviewSystem] ダッシュボード更新: 平均={dash.AverageRating:F2}, " +
                $"件数={dash.TotalReviews}, ポジ率={dash.PositiveRate:P0}");
        }

        // =====================================================================
        // クエリメソッド
        // =====================================================================

        public List<KeyValuePair<string, int>> GetTopTags(int count)
        {
            if (Dashboard.TagCounts == null || Dashboard.TagCounts.Count == 0)
                return new List<KeyValuePair<string, int>>();
            return Dashboard.TagCounts.OrderByDescending(kv => kv.Value).Take(count).ToList();
        }

        public List<DetailedReview> GetRecentReviews(int count)
        {
            int start = Mathf.Max(0, _reviews.Count - count);
            return _reviews.Skip(start).Take(Mathf.Min(count, _reviews.Count)).Reverse().ToList();
        }

        public List<DetailedReview> GetReviewsByRating(float minRating, float maxRating)
        {
            return _reviews.Where(r => r.OverallRating >= minRating && r.OverallRating <= maxRating).ToList();
        }

        public List<DetailedReview> GetAllReviews() { return new List<DetailedReview>(_reviews); }

        // =====================================================================
        // ヘルパー: 評価マッピング
        // =====================================================================

        /// <summary>幸福度(0-100)→星評価(1-5)。0-20→1, 20-40→2, 40-60→3, 60-80→4, 80-100→5</summary>
        private float MapHappinessToRating(float happiness)
        {
            if (happiness < 20f) return 1f;
            if (happiness < 40f) return 2f;
            if (happiness < 60f) return 3f;
            if (happiness < 80f) return 4f;
            return 5f;
        }

        /// <summary>幸福度→センチメント。>=60→Positive, 40-60→Neutral, <40→Negative</summary>
        private PostSentiment DetermineSentiment(float happiness)
        {
            if (happiness >= 60f) return PostSentiment.Positive;
            if (happiness >= 40f) return PostSentiment.Neutral;
            return PostSentiment.Negative;
        }

        // =====================================================================
        // ヘルパー: 来園者検索・情報取得
        // =====================================================================

        private VisitorAI FindVisitorAI(int visitorId)
        {
            var manager = FindObjectOfType<VisitorManager>();
            if (manager != null)
            {
                var visitors = manager.GetComponentsInChildren<VisitorAI>(true);
                for (int i = 0; i < visitors.Length; i++)
                    if (visitors[i].VisitorId == visitorId) return visitors[i];
            }
            var all = FindObjectsOfType<VisitorAI>();
            for (int i = 0; i < all.Length; i++)
                if (all[i].VisitorId == visitorId) return all[i];
            return null;
        }

        private string GetExperience(VisitorParameters p, bool best)
        {
            try
            {
                string val = best ? p.BestAttraction : p.WorstAttraction;
                if (!string.IsNullOrEmpty(val)) return val;
            }
            catch { }
            return "";
        }

        private ReviewContext GatherContext(VisitorParameters p)
        {
            var ctx = new ReviewContext();
            try
            {
                ctx.IsHungry = p.Hunger > 70f;
                ctx.IsThirsty = p.Thirst > 70f;
                ctx.SawDirtyAreas = p.Cleanliness < 30f;
                ctx.ExperiencedLongWait = p.WaitFrustration > 60f;
                ctx.AttractionsRidden = p.AttractionsRidden;
                ctx.ShopsVisited = p.ShopsVisited;
                ctx.IsHighHappiness = p.Happiness >= 70f;
                ctx.IsLowHappiness = p.Happiness < 30f;
            }
            catch
            {
                ctx.IsHighHappiness = p.Happiness >= 70f;
                ctx.IsLowHappiness = p.Happiness < 30f;
            }
            return ctx;
        }

        private string GenerateAuthorName()
        {
            return AuthorFirstNames[UnityEngine.Random.Range(0, AuthorFirstNames.Length)]
                 + AuthorSuffixes[UnityEngine.Random.Range(0, AuthorSuffixes.Length)];
        }

        private bool IsPositiveTag(string tag)
        {
            for (int i = 0; i < PositiveTags.Length; i++)
                if (PositiveTags[i] == tag) return true;
            return false;
        }

        private bool IsNegativeTag(string tag)
        {
            for (int i = 0; i < NegativeTags.Length; i++)
                if (NegativeTags[i] == tag) return true;
            return false;
        }

        // =====================================================================
        // レビューテキスト構築
        // =====================================================================

        private string BuildReviewText(float happiness, string best, string worst,
            ReviewContext ctx, PostSentiment sentiment)
        {
            var sb = new System.Text.StringBuilder();

            // 開始文
            sb.Append(Pick(sentiment, OpeningPositive, OpeningNeutral, OpeningNegative));

            // ベスト体験
            if (!string.IsNullOrEmpty(best))
            {
                sb.Append(" ");
                sb.Append(string.Format(
                    AttractionPraise[UnityEngine.Random.Range(0, AttractionPraise.Length)], best));
            }

            // ワースト体験
            if (!string.IsNullOrEmpty(worst))
            {
                sb.Append(" ");
                sb.Append(string.Format(
                    AttractionComplaint[UnityEngine.Random.Range(0, AttractionComplaint.Length)], worst));
            }

            // コンテキスト別不満（最大2つ）
            var complaints = new List<string>();
            if (ctx.IsHungry)
                complaints.Add(HungerComplaints[UnityEngine.Random.Range(0, HungerComplaints.Length)]);
            if (ctx.IsThirsty)
                complaints.Add(ThirstComplaints[UnityEngine.Random.Range(0, ThirstComplaints.Length)]);
            if (ctx.SawDirtyAreas)
                complaints.Add(DirtyComplaints[UnityEngine.Random.Range(0, DirtyComplaints.Length)]);
            if (ctx.ExperiencedLongWait)
                complaints.Add(LongWaitComplaints[UnityEngine.Random.Range(0, LongWaitComplaints.Length)]);

            for (int i = 0; i < Mathf.Min(complaints.Count, 2); i++)
            {
                sb.Append(" ");
                sb.Append(complaints[i]);
            }

            // アトラクション数言及
            if (ctx.AttractionsRidden >= 5 && sentiment == PostSentiment.Positive)
                sb.Append($" {ctx.AttractionsRidden}個のアトラクションに乗れて大満足です。");
            else if (ctx.AttractionsRidden <= 1 && sentiment != PostSentiment.Positive)
                sb.Append(" アトラクションにあまり乗れなかったのが残念です。");

            // ショップ言及
            if (ctx.ShopsVisited >= 3 && happiness >= 50f)
                sb.Append(" お土産もたくさん買えました。");

            // 締めくくり
            sb.Append(" ");
            sb.Append(Pick(sentiment, ClosingPositive, ClosingNeutral, ClosingNegative));

            return sb.ToString();
        }

        private string Pick(PostSentiment s, string[] pos, string[] neu, string[] neg)
        {
            switch (s)
            {
                case PostSentiment.Positive: return pos[UnityEngine.Random.Range(0, pos.Length)];
                case PostSentiment.Neutral:  return neu[UnityEngine.Random.Range(0, neu.Length)];
                case PostSentiment.Negative: return neg[UnityEngine.Random.Range(0, neg.Length)];
                default: return neu[0];
            }
        }

        // =====================================================================
        // タグ自動生成
        // =====================================================================

        private List<string> GenerateTags(float happiness, ReviewContext ctx,
            string best, string worst)
        {
            var tags = new List<string>();

            if (happiness >= 80f) tags.Add("リピート確定");
            if (happiness >= 60f) tags.Add("雰囲気が良い");
            if (happiness < 30f)  tags.Add("期待外れ");

            if (ctx.AttractionsRidden >= 5) tags.Add("アトラクション充実");
            if (ctx.AttractionsRidden <= 1) tags.Add("アトラクション少ない");

            if (!string.IsNullOrEmpty(best) && happiness >= 60f) tags.Add("コスパ良い");

            if (ctx.IsHungry) tags.Add("食事が高い");
            else if (ctx.ShopsVisited >= 2 && happiness >= 50f) tags.Add("料理がおいしい");

            if (ctx.IsThirsty) tags.Add("飲み物が高い");

            if (ctx.SawDirtyAreas) tags.Add("清掃不足");
            else if (happiness >= 50f) tags.Add("清潔感あり");

            if (ctx.ExperiencedLongWait) { tags.Add("待ち時間長い"); tags.Add("混雑しすぎ"); }
            else if (ctx.AttractionsRidden >= 3) tags.Add("待ち時間短い");

            if (happiness >= 60f && UnityEngine.Random.value > 0.6f) tags.Add("子ども向け充実");
            if (happiness >= 70f && UnityEngine.Random.value > 0.5f) tags.Add("スタッフ対応良い");
            else if (happiness < 30f && UnityEngine.Random.value > 0.6f) tags.Add("スタッフ不足");
            if (happiness >= 65f && UnityEngine.Random.value > 0.5f) tags.Add("景観が美しい");
            if (ctx.IsHungry && ctx.IsThirsty && UnityEngine.Random.value > 0.5f)
                tags.Add("休憩場所少ない");

            return tags.Distinct().ToList();
        }

        // =====================================================================
        // SNSシステム連携
        // =====================================================================

        private void BridgeToSNSSystem(DetailedReview review)
        {
            try
            {
                if (SNSReputationSystem.Instance != null)
                {
                    string snsContent = BuildSNSPostContent(review);
                    SNSReputationSystem.Instance.AddTemplatePost(
                        review.AuthorName, snsContent, review.Sentiment);
                    WebGLOptimizer.LogVerbose(
                        $"[VisitorReviewSystem] SNS投稿連携完了: ReviewId={review.ReviewId}");
                }

                var womSystem = FindObjectOfType<WordOfMouthSystem>();
                if (womSystem != null)
                    WebGLOptimizer.LogVerbose(
                        $"[VisitorReviewSystem] WordOfMouth連携: ReviewId={review.ReviewId}");
            }
            catch (System.Exception ex)
            {
                WebGLOptimizer.LogVerbose($"[VisitorReviewSystem] SNS連携エラー: {ex.Message}");
            }
        }

        private string BuildSNSPostContent(DetailedReview review)
        {
            var sb = new System.Text.StringBuilder();
            int stars = Mathf.RoundToInt(review.OverallRating);
            for (int i = 0; i < stars; i++) sb.Append("★");
            for (int i = stars; i < 5; i++) sb.Append("☆");
            sb.Append(" ");

            string text = review.ReviewText;
            sb.Append(text.Length > 60 ? text.Substring(0, 57) + "..." : text);

            int tagCount = Mathf.Min(review.Tags.Count, 3);
            if (tagCount > 0)
            {
                sb.Append(" ");
                for (int i = 0; i < tagCount; i++) sb.Append($"#{review.Tags[i]} ");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
