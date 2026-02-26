// ============================================================
// ThemeParkGame - SNS Reputation System
// 仮想SNS・評判管理システム
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.AI
{
    /// <summary>Sentiment classification for an SNS post.</summary>
    public enum PostSentiment
    {
        Positive,
        Neutral,
        Negative
    }

    /// <summary>
    /// Represents a single virtual SNS post written by an NPC about their park experience.
    /// </summary>
    [Serializable]
    public class SNSPost
    {
        public string PostId { get; set; }
        public int AuthorVisitorId { get; set; }
        public string AuthorName { get; set; }
        public string Content { get; set; }
        public PostSentiment Sentiment { get; set; }
        public float SentimentScore { get; set; }       // 0.0 (very negative) to 1.0 (very positive)
        public string[] Keywords { get; set; }
        public string Topic { get; set; }
        public float Timestamp { get; set; }             // In-game time when posted
        public int Likes { get; set; }
        public int Retweets { get; set; }
        public bool IsGenerated { get; set; }            // true if LLM-generated, false if template-based
    }

    /// <summary>
    /// Tracks a trending topic derived from aggregate SNS posts.
    /// </summary>
    [Serializable]
    public class TrendingTopic
    {
        public string TopicName { get; set; }
        public int MentionCount { get; set; }
        public PostSentiment OverallSentiment { get; set; }
        public float AverageSentimentScore { get; set; }
        public float TrendScore { get; set; }            // Composite score for ranking
    }

    /// <summary>
    /// Manages the virtual SNS system where NPCs post about their park experiences.
    /// Posts are generated via LLM or from templates. Aggregate sentiment drives
    /// the park's reputation score, which affects visitor spawn rates.
    /// </summary>
    public class SNSReputationSystem : MonoBehaviour
    {
        [Header("Reputation Settings")]
        [SerializeField] private float initialReputation = 50f;
        [SerializeField] private float reputationDecayRate = 0.1f;       // Per in-game hour
        [SerializeField] private float maxReputationChange = 5f;         // Max change per post
        [SerializeField] private float reputationSmoothingFactor = 0.1f; // Lerp speed

        [Header("SNS Feed Settings")]
        [SerializeField] private int maxFeedPosts = 100;
        [SerializeField] private int maxTrendingTopics = 10;
        [SerializeField] private float postGenerationChance = 0.7f;     // Chance an NPC posts on leaving

        [Header("Visitor Spawn Influence")]
        [SerializeField] private float spawnRateMultiplierMin = 0.5f;
        [SerializeField] private float spawnRateMultiplierMax = 2.0f;

        // ---- State ----
        private readonly List<SNSPost> _feed = new List<SNSPost>();
        private readonly List<TrendingTopic> _trendingTopics = new List<TrendingTopic>();
        private float _currentReputation;
        private float _targetReputation;
        private int _postIdCounter;

        // References
        private AIConversationManager _aiManager;

        // ---- Public Accessors ----

        /// <summary>Current park reputation score (0-100).</summary>
        public float Reputation => _currentReputation;

        /// <summary>The SNS feed, newest first.</summary>
        public IReadOnlyList<SNSPost> Feed => _feed.AsReadOnly();

        /// <summary>Current trending topics, sorted by trend score descending.</summary>
        public IReadOnlyList<TrendingTopic> TrendingTopics => _trendingTopics.AsReadOnly();

        /// <summary>
        /// Visitor spawn rate multiplier derived from reputation.
        /// 1.0 = baseline; higher reputation = more visitors.
        /// </summary>
        public float VisitorSpawnMultiplier
        {
            get
            {
                // Map reputation (0-100) to spawn multiplier range
                float normalized = Mathf.Clamp01(_currentReputation / 100f);
                return Mathf.Lerp(spawnRateMultiplierMin, spawnRateMultiplierMax, normalized);
            }
        }

        /// <summary>Fired when a new post is added to the feed.</summary>
        public event Action<SNSPost> OnNewPost;

        /// <summary>Fired when the reputation value changes.</summary>
        public event Action<float> OnReputationChanged;

        /// <summary>Fired when trending topics are recalculated.</summary>
        public event Action<IReadOnlyList<TrendingTopic>> OnTrendingTopicsUpdated;

        // ================================================================
        // Initialization
        // ================================================================

        public void Initialize()
        {
            _currentReputation = initialReputation;
            _targetReputation = initialReputation;
            _feed.Clear();
            _trendingTopics.Clear();
            _postIdCounter = 0;

            _aiManager = GetComponent<AIConversationManager>();
        }

        private void Start()
        {
            Initialize();

            // Listen for visitors leaving the park
            GameEvents.OnVisitorLeavePark += HandleVisitorLeaving;

            // Listen for day changes to apply reputation decay
            if (GameManager.Instance?.TimeManager != null)
                GameManager.Instance.TimeManager.OnDayChanged += HandleDayChanged;
        }

        private void Update()
        {
            // Smoothly interpolate reputation toward target
            if (Mathf.Abs(_currentReputation - _targetReputation) > 0.01f)
            {
                float oldReputation = _currentReputation;
                _currentReputation = Mathf.Lerp(_currentReputation, _targetReputation,
                    reputationSmoothingFactor * Time.deltaTime);

                if (Mathf.Abs(_currentReputation - oldReputation) > 0.01f)
                {
                    OnReputationChanged?.Invoke(_currentReputation);
                }
            }
        }

        // ================================================================
        // Post Generation
        // ================================================================

        /// <summary>
        /// Handles an NPC leaving the park. Decides whether to generate an SNS post.
        /// </summary>
        private void HandleVisitorLeaving(int visitorId)
        {
            // Random chance check - not every visitor posts
            if (UnityEngine.Random.value > postGenerationChance) return;

            // Try to get the NPC personality for richer post generation
            if (_aiManager != null && _aiManager.TryGetPersonality(visitorId, out NPCPersonality personality))
            {
                StartCoroutine(GeneratePostWithLLM(visitorId, personality));
            }
            else
            {
                // Fallback: generate a template-based post
                GenerateTemplatePost(visitorId);
            }
        }

        /// <summary>
        /// Generates an SNS post using the LLM API based on the visitor's experience.
        /// </summary>
        private IEnumerator GeneratePostWithLLM(int visitorId, NPCPersonality personality)
        {
            if (_aiManager == null || !_aiManager.IsAPIAvailable)
            {
                GenerateTemplatePost(visitorId, personality);
                yield break;
            }

            // Build the SNS post generation prompt
            string ridesStr = personality.RidesExperienced.Count > 0
                ? string.Join("、", personality.RidesExperienced)
                : "なし";
            string foodStr = personality.FoodEaten.Count > 0
                ? string.Join("、", personality.FoodEaten)
                : "なし";
            string souvenirsStr = personality.SouvenirsBought.Count > 0
                ? string.Join("、", personality.SouvenirsBought)
                : "なし";
            string memorableStr = personality.MemorableEvents.Count > 0
                ? string.Join("\n", personality.MemorableEvents)
                : "特になし";

            string personalityDesc = personality.Traits.ToString();

            string userPrompt = PromptTemplates.SNSPostUserPrompt
                .Replace("{name}", personality.DisplayName)
                .Replace("{age}", personality.Age.ToString())
                .Replace("{personality}", personalityDesc)
                .Replace("{rides}", ridesStr)
                .Replace("{food}", foodStr)
                .Replace("{souvenirs}", souvenirsStr)
                .Replace("{wait_time}", personality.TotalWaitTimeMinutes.ToString())
                .Replace("{satisfaction}", Mathf.RoundToInt(personality.Happiness).ToString())
                .Replace("{memorable_events}", memorableStr);

            var messages = new List<LLMMessage>
            {
                new LLMMessage("system", PromptTemplates.SNSPostSystemPrompt
                    .Replace("{safety}", PromptTemplates.SafetyPreamble)),
                new LLMMessage("user", userPrompt)
            };

            LLMResponse response = null;
            yield return _aiManager.SendLLMRequest(messages, r => response = r);

            if (response != null && response.Success && !string.IsNullOrEmpty(response.Content))
            {
                var post = CreatePost(visitorId, personality.DisplayName, response.Content, true);
                AnalyzeSentimentLocal(post, personality.Happiness);

                WebGLOptimizer.LogVerbose($"[SNSReputation] LLM post generated for {personality.DisplayName}: {post.Content}");
            }
            else
            {
                // Fallback to template if LLM fails
                GenerateTemplatePost(visitorId, personality);
            }
        }

        /// <summary>
        /// 外部システム（WordOfMouthSystem等）からテンプレート投稿を追加する公開API。
        /// </summary>
        public void AddTemplatePost(int visitorId, string authorName, string content,
            PostSentiment sentiment, float sentimentScore)
        {
            var post = CreatePost(visitorId, authorName, content, false);
            post.Sentiment = sentiment;
            post.SentimentScore = Mathf.Clamp01(sentimentScore);
            post.Topic = DetectTopic(content);
            UpdateReputationFromPost(post);
            RecalculateTrendingTopics();
            WebGLOptimizer.LogVerbose($"[SNSReputation] External post added: {authorName}: {content}");
        }

        /// <summary>
        /// Generates a template-based SNS post without LLM.
        /// Used as fallback or when API is unavailable.
        /// </summary>
        private void GenerateTemplatePost(int visitorId, NPCPersonality personality = null)
        {
            string authorName = personality?.DisplayName ?? $"来場者#{visitorId}";
            float happiness = personality?.Happiness ?? 50f;

            string content = GenerateTemplateContent(happiness, personality);
            var post = CreatePost(visitorId, authorName, content, false);
            AnalyzeSentimentLocal(post, happiness);

            WebGLOptimizer.LogVerbose($"[SNSReputation] Template post generated for {authorName}: {post.Content}");
        }

        /// <summary>
        /// Creates a post object and adds it to the feed.
        /// </summary>
        private SNSPost CreatePost(int visitorId, string authorName, string content, bool isGenerated)
        {
            _postIdCounter++;
            var rng = new System.Random(visitorId + _postIdCounter);

            var post = new SNSPost
            {
                PostId = $"sns_{_postIdCounter:D6}",
                AuthorVisitorId = visitorId,
                AuthorName = authorName,
                Content = content,
                Timestamp = Time.time,
                Likes = rng.Next(0, 50),
                Retweets = rng.Next(0, 15),
                IsGenerated = isGenerated,
                Keywords = Array.Empty<string>()
            };

            // Add to feed, trim old posts
            _feed.Insert(0, post);
            while (_feed.Count > maxFeedPosts)
            {
                _feed.RemoveAt(_feed.Count - 1);
            }

            OnNewPost?.Invoke(post);
            GameEvents.FireSNSPostGenerated(visitorId, content);

            return post;
        }

        // ================================================================
        // Sentiment Analysis
        // ================================================================

        /// <summary>
        /// Performs local (non-LLM) sentiment analysis based on keyword matching and happiness score.
        /// This is used for all posts to ensure we always have sentiment data.
        /// </summary>
        private void AnalyzeSentimentLocal(SNSPost post, float happinessScore)
        {
            // Base sentiment from happiness
            float baseSentiment = happinessScore / 100f;

            // Adjust based on keywords in the post content
            float keywordAdjustment = 0f;
            var detectedKeywords = new List<string>();

            // Positive keywords
            string[] positiveKeywords = {
                "最高", "楽しい", "楽しかった", "素敵", "おすすめ", "また来たい",
                "感動", "すごい", "きれい", "美味しい", "おいしい", "嬉しい",
                "大好き", "幸せ", "ワクワク", "満足", "サイコー", "神"
            };

            // Negative keywords
            string[] negativeKeywords = {
                "待ち時間", "長い", "高い", "汚い", "つまらない", "残念",
                "がっかり", "不満", "最悪", "ひどい", "混雑", "疲れた",
                "もう来ない", "期待はずれ", "微妙", "イマイチ"
            };

            foreach (string keyword in positiveKeywords)
            {
                if (post.Content.Contains(keyword))
                {
                    keywordAdjustment += 0.05f;
                    detectedKeywords.Add(keyword);
                }
            }

            foreach (string keyword in negativeKeywords)
            {
                if (post.Content.Contains(keyword))
                {
                    keywordAdjustment -= 0.05f;
                    detectedKeywords.Add(keyword);
                }
            }

            // Combine base and keyword-based sentiment
            float finalScore = Mathf.Clamp01(baseSentiment + keywordAdjustment);
            post.SentimentScore = finalScore;
            post.Keywords = detectedKeywords.ToArray();

            // Classify sentiment
            if (finalScore >= 0.6f)
            {
                post.Sentiment = PostSentiment.Positive;
            }
            else if (finalScore <= 0.4f)
            {
                post.Sentiment = PostSentiment.Negative;
            }
            else
            {
                post.Sentiment = PostSentiment.Neutral;
            }

            // Detect topic
            post.Topic = DetectTopic(post.Content);

            // Update reputation based on this post
            UpdateReputationFromPost(post);

            // Recalculate trending topics
            RecalculateTrendingTopics();
        }

        /// <summary>
        /// Detects the main topic of a post from its content.
        /// </summary>
        private string DetectTopic(string content)
        {
            // Topic detection via keyword groups
            var topicKeywords = new Dictionary<string, string[]>
            {
                { "アトラクション", new[] { "アトラクション", "乗り物", "ジェットコースター", "観覧車", "お化け屋敷", "ゴーカート" } },
                { "グルメ", new[] { "食べ", "美味", "おいし", "レストラン", "フード", "ご飯", "ランチ", "スイーツ" } },
                { "お土産", new[] { "お土産", "グッズ", "買い物", "ショップ", "ぬいぐるみ" } },
                { "混雑", new[] { "混", "待ち時間", "行列", "並ん" } },
                { "雰囲気", new[] { "雰囲気", "きれい", "素敵", "イルミネーション", "パレード", "景色" } },
                { "スタッフ", new[] { "スタッフ", "キャスト", "対応", "サービス", "接客" } },
                { "清潔さ", new[] { "汚", "きれい", "掃除", "清潔", "ゴミ" } },
                { "価格", new[] { "高い", "安い", "値段", "コスパ", "料金" } }
            };

            string bestTopic = "パーク全般";
            int bestScore = 0;

            foreach (var kvp in topicKeywords)
            {
                int score = 0;
                foreach (string keyword in kvp.Value)
                {
                    if (content.Contains(keyword)) score++;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTopic = kvp.Key;
                }
            }

            return bestTopic;
        }

        // ================================================================
        // Reputation Management
        // ================================================================

        /// <summary>
        /// Updates the target reputation score based on a new post's sentiment.
        /// </summary>
        private void UpdateReputationFromPost(SNSPost post)
        {
            // Map sentiment score to reputation change
            // 0.0 = -maxReputationChange, 0.5 = 0, 1.0 = +maxReputationChange
            float change = (post.SentimentScore - 0.5f) * 2f * maxReputationChange;

            // Posts with more engagement have more impact
            float engagementMultiplier = 1f + (post.Likes + post.Retweets * 2) * 0.01f;
            change *= Mathf.Min(engagementMultiplier, 2f);

            _targetReputation = Mathf.Clamp(_targetReputation + change, 0f, 100f);
        }

        /// <summary>
        /// Applies gradual reputation decay over time.
        /// Call this from the game's time system (e.g., once per in-game hour).
        /// </summary>
        public void ApplyReputationDecay(float deltaHours)
        {
            // Reputation slowly drifts toward 50 (neutral) if no new posts
            float decayAmount = reputationDecayRate * deltaHours;
            if (_targetReputation > 50f)
            {
                _targetReputation = Mathf.Max(50f, _targetReputation - decayAmount);
            }
            else if (_targetReputation < 50f)
            {
                _targetReputation = Mathf.Min(50f, _targetReputation + decayAmount);
            }
        }

        // ================================================================
        // Trending Topics
        // ================================================================

        /// <summary>
        /// Recalculates trending topics from recent posts in the feed.
        /// </summary>
        private void RecalculateTrendingTopics()
        {
            _trendingTopics.Clear();

            // Consider only recent posts (last 50 or all if fewer)
            int recentCount = Mathf.Min(_feed.Count, 50);
            var recentPosts = _feed.GetRange(0, recentCount);

            // Group by topic
            var topicGroups = new Dictionary<string, List<SNSPost>>();
            foreach (var post in recentPosts)
            {
                if (string.IsNullOrEmpty(post.Topic)) continue;

                if (!topicGroups.ContainsKey(post.Topic))
                {
                    topicGroups[post.Topic] = new List<SNSPost>();
                }
                topicGroups[post.Topic].Add(post);
            }

            // Build trending topic entries
            foreach (var kvp in topicGroups)
            {
                var posts = kvp.Value;
                float avgSentiment = posts.Average(p => p.SentimentScore);
                int totalEngagement = posts.Sum(p => p.Likes + p.Retweets);

                PostSentiment overallSentiment;
                if (avgSentiment >= 0.6f)
                    overallSentiment = PostSentiment.Positive;
                else if (avgSentiment <= 0.4f)
                    overallSentiment = PostSentiment.Negative;
                else
                    overallSentiment = PostSentiment.Neutral;

                // Trend score combines mention count and engagement
                float trendScore = posts.Count * 10f + totalEngagement * 0.5f;

                _trendingTopics.Add(new TrendingTopic
                {
                    TopicName = kvp.Key,
                    MentionCount = posts.Count,
                    OverallSentiment = overallSentiment,
                    AverageSentimentScore = avgSentiment,
                    TrendScore = trendScore
                });
            }

            // Sort by trend score descending and limit
            _trendingTopics.Sort((a, b) => b.TrendScore.CompareTo(a.TrendScore));
            while (_trendingTopics.Count > maxTrendingTopics)
            {
                _trendingTopics.RemoveAt(_trendingTopics.Count - 1);
            }

            OnTrendingTopicsUpdated?.Invoke(_trendingTopics.AsReadOnly());
        }

        // ================================================================
        // Public Query Methods
        // ================================================================

        /// <summary>
        /// Gets posts filtered by sentiment.
        /// </summary>
        public List<SNSPost> GetPostsBySentiment(PostSentiment sentiment, int maxCount = 20)
        {
            return _feed
                .Where(p => p.Sentiment == sentiment)
                .Take(maxCount)
                .ToList();
        }

        /// <summary>
        /// Gets posts filtered by topic keyword.
        /// </summary>
        public List<SNSPost> GetPostsByTopic(string topic, int maxCount = 20)
        {
            return _feed
                .Where(p => p.Topic == topic)
                .Take(maxCount)
                .ToList();
        }

        /// <summary>
        /// Gets the most popular posts (by engagement: likes + retweets).
        /// </summary>
        public List<SNSPost> GetPopularPosts(int maxCount = 10)
        {
            return _feed
                .OrderByDescending(p => p.Likes + p.Retweets * 2)
                .Take(maxCount)
                .ToList();
        }

        /// <summary>
        /// Returns a summary string of the current SNS state for use in LLM context.
        /// </summary>
        public string GetReputationSummaryForContext()
        {
            string reputationLevel = _currentReputation switch
            {
                >= 80f => "非常に高い（大人気のパーク）",
                >= 60f => "高い（評判の良いパーク）",
                >= 40f => "普通",
                >= 20f => "低い（不満の声が多い）",
                _ => "非常に低い（悪評が広がっている）"
            };

            string topTrend = _trendingTopics.Count > 0
                ? $"トレンド1位: {_trendingTopics[0].TopicName}（{_trendingTopics[0].OverallSentiment}）"
                : "トレンドなし";

            int recentPositive = _feed.Count(p => p.Sentiment == PostSentiment.Positive);
            int recentNegative = _feed.Count(p => p.Sentiment == PostSentiment.Negative);

            return $"パーク評判: {reputationLevel}（スコア: {Mathf.RoundToInt(_currentReputation)}/100）\n" +
                   $"最近の投稿: ポジティブ{recentPositive}件 / ネガティブ{recentNegative}件\n" +
                   topTrend;
        }

        // ================================================================
        // Template-Based Post Generation (Fallback)
        // ================================================================

        /// <summary>Generates post content from templates based on happiness level.</summary>
        private string GenerateTemplateContent(float happiness, NPCPersonality personality = null)
        {
            var rng = new System.Random((int)(Time.time * 1000));

            if (happiness >= 80f)
            {
                return PickFromTemplates(rng, HighHappinessTemplates, personality);
            }
            else if (happiness >= 50f)
            {
                return PickFromTemplates(rng, MediumHappinessTemplates, personality);
            }
            else
            {
                return PickFromTemplates(rng, LowHappinessTemplates, personality);
            }
        }

        private string PickFromTemplates(System.Random rng, string[] templates, NPCPersonality personality)
        {
            string template = templates[rng.Next(templates.Length)];
            string parkName = "ドリームパーク"; // Default park name
            string rideName = personality?.RidesExperienced.Count > 0
                ? personality.RidesExperienced[rng.Next(personality.RidesExperienced.Count)]
                : "ジェットコースター";
            string foodName = personality?.FoodEaten.Count > 0
                ? personality.FoodEaten[rng.Next(personality.FoodEaten.Count)]
                : "パークフード";

            return template
                .Replace("{park}", parkName)
                .Replace("{ride}", rideName)
                .Replace("{food}", foodName);
        }

        private static readonly string[] HighHappinessTemplates = new[]
        {
            "{park}最高すぎた！！{ride}がヤバかった...また絶対来る！ #テーマパーク #最高の休日",
            "今日の{park}、控えめに言って神でした。{food}も美味しかったし幸せ～ #パーク日和",
            "{ride}に乗ってきた！スリル満点で叫びすぎた笑 {park}サイコー！ #アトラクション好き",
            "家族で{park}！子供たちも大喜びで最高の一日でした♪ #家族の思い出 #テーマパーク",
            "{park}のイルミネーションが綺麗すぎて感動...写真いっぱい撮った！ #映えスポット",
            "リピ確定！{park}の{ride}、何回乗っても飽きない！ #パーク好きと繋がりたい"
        };

        private static readonly string[] MediumHappinessTemplates = new[]
        {
            "{park}に来てます。{ride}は楽しかったけど待ち時間がちょっと長かったかな #テーマパーク",
            "久しぶりの{park}。まあまあ楽しめたかな。{food}は普通 #休日",
            "{park}、思ったより混んでた...でも{ride}は乗れたからOK #テーマパーク日記",
            "天気が微妙だったけど{park}それなりに楽しめた。次は晴れの日に来たい #テーマパーク",
            "{park}のお土産ショップでいろいろ買っちゃった。{ride}はまた次回かな #買い物"
        };

        private static readonly string[] LowHappinessTemplates = new[]
        {
            "う～ん、{park}ちょっと期待はずれだったかも...待ち時間長すぎない？ #正直レビュー",
            "{park}来たけどゴミが多いのが気になった。もう少し清潔だといいのに #改善希望",
            "{ride}が故障で乗れなかった...{park}残念すぎる #テーマパーク",
            "{park}の{food}、値段の割に微妙...コスパ悪すぎ #正直な感想",
            "混みすぎて全然楽しめなかった{park}。平日に来るべきだったかな #混雑注意"
        };

        // ================================================================
        // Time Integration
        // ================================================================

        /// <summary>日替わりでレピュテーション減衰を適用する</summary>
        private void HandleDayChanged()
        {
            // 1日 = 約14時間のパーク営業時間分の減衰を適用
            ApplyReputationDecay(14f);
        }

        // ================================================================
        // Save/Load
        // ================================================================

        /// <summary>セーブデータからレピュテーションを復元する</summary>
        public void RestoreReputation(float reputation)
        {
            _currentReputation = Mathf.Clamp(reputation, 0f, 100f);
            _targetReputation = _currentReputation;
        }

        // ================================================================
        // Cleanup
        // ================================================================

        private void OnDestroy()
        {
            GameEvents.OnVisitorLeavePark -= HandleVisitorLeaving;
            if (GameManager.Instance?.TimeManager != null)
                GameManager.Instance.TimeManager.OnDayChanged -= HandleDayChanged;
        }
    }
}
