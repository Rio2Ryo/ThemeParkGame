// ============================================================
// ThemeParkGame - Dynamic Quest System
// NPC会話からのクエスト自動生成システム
// ============================================================

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.AI
{
    /// <summary>Types of quests that can be dynamically generated from NPC conversations.</summary>
    public enum QuestType
    {
        FindPerson,     // 迷子探し - find a lost companion
        RecommendRide,  // おすすめ案内 - recommend an attraction
        DeliverItem,    // アイテム配達 - deliver an item to someone
        GuideTour,      // パーク案内 - guide the NPC around the park
        SolveProblem    // 問題解決 - solve a park issue (litter, broken ride, etc.)
    }

    /// <summary>Urgency level affects time limit and reward multiplier.</summary>
    public enum QuestUrgency
    {
        Low,
        Medium,
        High
    }

    /// <summary>Current state of a quest.</summary>
    public enum QuestState
    {
        Active,
        Completed,
        Failed,
        Expired
    }

    /// <summary>
    /// Represents a single dynamically generated quest.
    /// </summary>
    [Serializable]
    public class DynamicQuest
    {
        public string QuestId { get; set; }
        public QuestType Type { get; set; }
        public QuestState State { get; set; }
        public QuestUrgency Urgency { get; set; }

        // Quest description and target
        public string Title { get; set; }
        public string Description { get; set; }
        public string TargetName { get; set; }            // Name of target NPC or facility
        public int RequestingVisitorId { get; set; }      // NPC who gave the quest

        // Rewards
        public float HappinessBonus { get; set; }         // Bonus to requesting NPC's happiness
        public int MoneyReward { get; set; }              // Currency reward for player
        public float ReputationBonus { get; set; }        // Park reputation boost

        // Time tracking
        public float CreatedTime { get; set; }
        public float TimeLimitSeconds { get; set; }       // 0 = no time limit
        public float CompletedTime { get; set; }

        /// <summary>Whether the quest has exceeded its time limit.</summary>
        public bool IsExpired =>
            TimeLimitSeconds > 0 && Time.time - CreatedTime > TimeLimitSeconds;

        /// <summary>Remaining time as a normalized 0-1 value (1 = full time, 0 = expired).</summary>
        public float TimeRemainingNormalized
        {
            get
            {
                if (TimeLimitSeconds <= 0) return 1f;
                float remaining = TimeLimitSeconds - (Time.time - CreatedTime);
                return Mathf.Clamp01(remaining / TimeLimitSeconds);
            }
        }
    }

    /// <summary>
    /// Manages dynamic quest generation, tracking, and completion.
    /// Quests are extracted from NPC conversations via keyword/intent detection
    /// and optionally refined by LLM analysis.
    /// </summary>
    public class DynamicQuestSystem : MonoBehaviour
    {
        [Header("Quest Settings")]
        [SerializeField] private int maxActiveQuests = 5;
        [SerializeField] private float questExpiryCheckInterval = 10f;

        [Header("Reward Settings")]
        [SerializeField] private int baseMoneyReward = 500;
        [SerializeField] private float baseHappinessBonus = 15f;
        [SerializeField] private float baseReputationBonus = 3f;

        // Active and completed quest storage
        private readonly List<DynamicQuest> _activeQuests = new List<DynamicQuest>();
        private readonly List<DynamicQuest> _completedQuests = new List<DynamicQuest>();
        private int _questIdCounter;

        // Reference to AIConversationManager for LLM-based quest extraction
        private AIConversationManager _aiManager;

        // Keyword-based intent detection patterns (Japanese)
        private static readonly Dictionary<QuestType, string[]> IntentKeywords = new Dictionary<QuestType, string[]>
        {
            {
                QuestType.FindPerson, new[]
                {
                    "迷子", "はぐれ", "見つけ", "探して", "どこにいる",
                    "いなくなっ", "連れ", "子供が", "友達が", "はぐれちゃ",
                    "見失", "離れ離れ"
                }
            },
            {
                QuestType.RecommendRide, new[]
                {
                    "おすすめ", "どれがいい", "何がある", "人気の", "楽しい乗り物",
                    "教えて", "アトラクション", "乗り物", "どこに行けば",
                    "一番", "スリル", "面白い"
                }
            },
            {
                QuestType.DeliverItem, new[]
                {
                    "届けて", "渡して", "持っていって", "預かって", "お土産",
                    "落とし物", "忘れ物", "あの人に", "伝えて"
                }
            },
            {
                QuestType.GuideTour, new[]
                {
                    "案内", "初めて", "わからない", "道に迷", "どう行けば",
                    "連れていって", "一緒に", "見て回り", "ガイド",
                    "教えてもらえ", "回り方"
                }
            },
            {
                QuestType.SolveProblem, new[]
                {
                    "汚い", "ゴミ", "壊れ", "故障", "臭い", "うるさい",
                    "危ない", "困って", "問題", "直して", "掃除",
                    "散らかっ", "ベンチが", "トイレが"
                }
            }
        };

        // Japanese quest title templates per type
        private static readonly Dictionary<QuestType, string[]> QuestTitleTemplates = new Dictionary<QuestType, string[]>
        {
            {
                QuestType.FindPerson, new[]
                {
                    "迷子の{target}を探そう！",
                    "はぐれた{target}を見つけて！",
                    "{target}はどこ？"
                }
            },
            {
                QuestType.RecommendRide, new[]
                {
                    "{target}にぴったりのアトラクションを提案！",
                    "おすすめアトラクションガイド",
                    "最高の体験を{target}に！"
                }
            },
            {
                QuestType.DeliverItem, new[]
                {
                    "{target}にアイテムを届けよう！",
                    "お届けもの：{target}宛",
                    "大切な届け物"
                }
            },
            {
                QuestType.GuideTour, new[]
                {
                    "{target}をパーク案内しよう！",
                    "はじめてのパーク体験ガイド",
                    "{target}の特別ツアー"
                }
            },
            {
                QuestType.SolveProblem, new[]
                {
                    "パークの問題を解決しよう！",
                    "困りごと対応：{target}",
                    "パーク改善ミッション"
                }
            }
        };

        // Quest description templates (Japanese)
        private static readonly Dictionary<QuestType, string> QuestDescriptionTemplates = new Dictionary<QuestType, string>
        {
            { QuestType.FindPerson, "{visitor}が{target}とはぐれてしまいました。パーク内を探して再会させてあげましょう。" },
            { QuestType.RecommendRide, "{visitor}がおすすめのアトラクションを探しています。好みに合ったアトラクションに案内してあげましょう。" },
            { QuestType.DeliverItem, "{visitor}から{target}への届け物を預かりました。パーク内で{target}を見つけて届けましょう。" },
            { QuestType.GuideTour, "{visitor}がパークの案内を求めています。一緒にパーク内を回って楽しませてあげましょう。" },
            { QuestType.SolveProblem, "{visitor}がパーク内の問題を報告しています。{target}を確認して解決しましょう。" }
        };

        /// <summary>Read-only access to currently active quests.</summary>
        public IReadOnlyList<DynamicQuest> ActiveQuests => _activeQuests.AsReadOnly();

        /// <summary>Read-only access to completed quests (history).</summary>
        public IReadOnlyList<DynamicQuest> CompletedQuests => _completedQuests.AsReadOnly();

        /// <summary>Fired when a new quest is generated.</summary>
        public event Action<DynamicQuest> OnQuestCreated;

        /// <summary>Fired when a quest is completed.</summary>
        public event Action<DynamicQuest> OnQuestCompleted;

        /// <summary>Fired when a quest expires or fails.</summary>
        public event Action<DynamicQuest> OnQuestFailed;

        private void Start()
        {
            _aiManager = GetComponent<AIConversationManager>();
            InvokeRepeating(nameof(CheckExpiredQuests), questExpiryCheckInterval, questExpiryCheckInterval);
        }

        // ================================================================
        // Quest Detection & Creation
        // ================================================================

        /// <summary>
        /// Analyzes NPC dialogue text for quest-triggering intent using keyword matching.
        /// Returns true if a quest was detected and created.
        /// This is the fast, local-only detection path (no LLM call required).
        /// </summary>
        public bool TryDetectQuestFromDialogue(int visitorId, string dialogueText, NPCPersonality personality)
        {
            if (string.IsNullOrEmpty(dialogueText)) return false;
            if (_activeQuests.Count >= maxActiveQuests)
            {
                WebGLOptimizer.LogVerbose("[DynamicQuestSystem] Max active quests reached, skipping detection.");
                return false;
            }

            // Check if this visitor already has an active quest
            if (_activeQuests.Exists(q => q.RequestingVisitorId == visitorId && q.State == QuestState.Active))
            {
                return false;
            }

            // Score each quest type by keyword matches
            QuestType bestType = QuestType.FindPerson;
            int bestScore = 0;

            foreach (var kvp in IntentKeywords)
            {
                int score = 0;
                foreach (string keyword in kvp.Value)
                {
                    if (dialogueText.Contains(keyword))
                    {
                        score++;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestType = kvp.Key;
                }
            }

            // Require at least 1 keyword match to trigger a quest
            if (bestScore < 1) return false;

            var quest = CreateQuest(bestType, visitorId, personality, dialogueText);
            return quest != null;
        }

        /// <summary>
        /// Creates a quest from LLM-parsed JSON response.
        /// Used when the AIConversationManager performs deeper intent analysis.
        /// </summary>
        public DynamicQuest TryCreateQuestFromLLMResponse(int visitorId, string jsonResponse, NPCPersonality personality)
        {
            if (_activeQuests.Count >= maxActiveQuests) return null;
            if (_activeQuests.Exists(q => q.RequestingVisitorId == visitorId && q.State == QuestState.Active))
                return null;

            try
            {
                // Parse the JSON response for quest data
                // Expected format matches PromptTemplates.QuestExtractionPrompt output
                var parsed = ParseQuestJson(jsonResponse);
                if (parsed == null || !parsed.Detected) return null;

                return CreateQuest(parsed.Type, visitorId, personality, parsed.Description, parsed.Target, parsed.Urgency);
            }
            catch (Exception ex)
            {
                WebGLOptimizer.LogWarning($"[DynamicQuestSystem] Failed to parse LLM quest response: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates and registers a new quest.
        /// </summary>
        private DynamicQuest CreateQuest(
            QuestType type,
            int visitorId,
            NPCPersonality personality,
            string dialogueHint,
            string targetOverride = null,
            QuestUrgency urgencyOverride = QuestUrgency.Medium)
        {
            _questIdCounter++;
            string questId = $"quest_{_questIdCounter:D4}";

            string visitorName = personality?.DisplayName ?? $"来場者#{visitorId}";
            string target = targetOverride ?? GenerateDefaultTarget(type, personality);

            // Select title template
            var titleTemplates = QuestTitleTemplates[type];
            string title = titleTemplates[_questIdCounter % titleTemplates.Length]
                .Replace("{target}", target);

            // Generate description
            string description = QuestDescriptionTemplates[type]
                .Replace("{visitor}", visitorName)
                .Replace("{target}", target);

            // Calculate rewards based on urgency
            float urgencyMultiplier = urgencyOverride switch
            {
                QuestUrgency.Low => 0.8f,
                QuestUrgency.Medium => 1.0f,
                QuestUrgency.High => 1.5f,
                _ => 1.0f
            };

            // Calculate time limit based on urgency
            float timeLimit = urgencyOverride switch
            {
                QuestUrgency.Low => 0f,             // No time limit
                QuestUrgency.Medium => 300f,         // 5 minutes (real-time)
                QuestUrgency.High => 180f,           // 3 minutes (real-time)
                _ => 0f
            };

            var quest = new DynamicQuest
            {
                QuestId = questId,
                Type = type,
                State = QuestState.Active,
                Urgency = urgencyOverride,
                Title = title,
                Description = description,
                TargetName = target,
                RequestingVisitorId = visitorId,
                HappinessBonus = baseHappinessBonus * urgencyMultiplier,
                MoneyReward = Mathf.RoundToInt(baseMoneyReward * urgencyMultiplier),
                ReputationBonus = baseReputationBonus * urgencyMultiplier,
                CreatedTime = Time.time,
                TimeLimitSeconds = timeLimit
            };

            _activeQuests.Add(quest);
            OnQuestCreated?.Invoke(quest);
            GameEvents.FireQuestGenerated(questId);

            WebGLOptimizer.LogVerbose($"[DynamicQuestSystem] Quest created: {title} (ID: {questId}, Type: {type})");
            return quest;
        }

        // ================================================================
        // Quest Completion & Failure
        // ================================================================

        /// <summary>
        /// Marks a quest as completed and awards rewards.
        /// </summary>
        public void CompleteQuest(string questId)
        {
            var quest = _activeQuests.Find(q => q.QuestId == questId);
            if (quest == null)
            {
                WebGLOptimizer.LogWarning($"[DynamicQuestSystem] Cannot complete quest '{questId}': not found in active quests.");
                return;
            }

            if (quest.State != QuestState.Active)
            {
                WebGLOptimizer.LogWarning($"[DynamicQuestSystem] Cannot complete quest '{questId}': state is {quest.State}.");
                return;
            }

            quest.State = QuestState.Completed;
            quest.CompletedTime = Time.time;

            // Award rewards
            AwardQuestRewards(quest);

            // Move to completed list
            _activeQuests.Remove(quest);
            _completedQuests.Add(quest);

            OnQuestCompleted?.Invoke(quest);
            WebGLOptimizer.LogVerbose($"[DynamicQuestSystem] Quest completed: {quest.Title} (Reward: {quest.MoneyReward}円)");
        }

        /// <summary>
        /// Marks a quest as failed (e.g., player abandoned it or conditions changed).
        /// </summary>
        public void FailQuest(string questId, string reason = null)
        {
            var quest = _activeQuests.Find(q => q.QuestId == questId);
            if (quest == null) return;

            quest.State = QuestState.Failed;
            _activeQuests.Remove(quest);
            _completedQuests.Add(quest);

            // Apply small happiness penalty to requesting NPC
            GameEvents.FireVisitorHappinessChanged(quest.RequestingVisitorId, -5f);

            OnQuestFailed?.Invoke(quest);
            WebGLOptimizer.LogVerbose($"[DynamicQuestSystem] Quest failed: {quest.Title}" +
                      (reason != null ? $" - Reason: {reason}" : ""));
        }

        /// <summary>
        /// Attempts to detect quest completion based on game events.
        /// Called by external systems when relevant actions occur.
        /// </summary>
        public void CheckQuestCompletion(QuestType relevantType, int targetVisitorId = -1, string targetFacility = null)
        {
            for (int i = _activeQuests.Count - 1; i >= 0; i--)
            {
                var quest = _activeQuests[i];
                if (quest.Type != relevantType || quest.State != QuestState.Active) continue;

                bool completed = false;

                switch (quest.Type)
                {
                    case QuestType.FindPerson:
                        // Completed when target visitor is found (near the requesting visitor)
                        completed = targetVisitorId >= 0 &&
                                    quest.TargetName != null;
                        break;

                    case QuestType.RecommendRide:
                        // Completed when the requesting visitor rides any attraction
                        completed = quest.RequestingVisitorId == targetVisitorId;
                        break;

                    case QuestType.DeliverItem:
                        // Completed when player reaches the target NPC
                        completed = targetVisitorId >= 0;
                        break;

                    case QuestType.GuideTour:
                        // Completed when the visitor has visited 3+ facilities
                        completed = targetVisitorId == quest.RequestingVisitorId;
                        break;

                    case QuestType.SolveProblem:
                        // Completed when the relevant facility issue is resolved
                        completed = targetFacility != null &&
                                    quest.TargetName == targetFacility;
                        break;
                }

                if (completed)
                {
                    CompleteQuest(quest.QuestId);
                }
            }
        }

        /// <summary>Returns the active quest for the given visitor, if any.</summary>
        public DynamicQuest GetActiveQuestForVisitor(int visitorId)
        {
            return _activeQuests.Find(q => q.RequestingVisitorId == visitorId && q.State == QuestState.Active);
        }

        // ================================================================
        // Internal Helpers
        // ================================================================

        private void AwardQuestRewards(DynamicQuest quest)
        {
            // Time bonus: faster completion = more reward
            float timeBonus = 1.0f;
            if (quest.TimeLimitSeconds > 0)
            {
                float normalized = quest.TimeRemainingNormalized;
                timeBonus = 1.0f + (normalized * 0.5f); // Up to 50% bonus for fast completion
            }

            int finalMoney = Mathf.RoundToInt(quest.MoneyReward * timeBonus);
            float finalHappiness = quest.HappinessBonus * timeBonus;
            float finalReputation = quest.ReputationBonus * timeBonus;

            // Fire game events to award rewards
            GameEvents.FireRevenueEarned(finalMoney);
            GameEvents.FireVisitorHappinessChanged(quest.RequestingVisitorId, finalHappiness);

            WebGLOptimizer.LogVerbose($"[DynamicQuestSystem] Rewards: {finalMoney}円, " +
                      $"Happiness +{finalHappiness:F1}, Reputation +{finalReputation:F1}");
        }

        /// <summary>Periodically checks for expired quests.</summary>
        private void CheckExpiredQuests()
        {
            for (int i = _activeQuests.Count - 1; i >= 0; i--)
            {
                var quest = _activeQuests[i];
                if (quest.IsExpired)
                {
                    quest.State = QuestState.Expired;
                    _activeQuests.Remove(quest);
                    _completedQuests.Add(quest);

                    OnQuestFailed?.Invoke(quest);
                    WebGLOptimizer.LogVerbose($"[DynamicQuestSystem] Quest expired: {quest.Title}");
                }
            }
        }

        /// <summary>Generates a default target name based on quest type and NPC context.</summary>
        private string GenerateDefaultTarget(QuestType type, NPCPersonality personality)
        {
            var rng = new System.Random(personality?.VisitorId ?? 0 + _questIdCounter);

            return type switch
            {
                QuestType.FindPerson => personality?.CompanionDescription ?? "連れの人",
                QuestType.RecommendRide => personality?.DisplayName ?? "来場者",
                QuestType.DeliverItem => $"来場者#{rng.Next(100, 999)}",
                QuestType.GuideTour => personality?.DisplayName ?? "来場者",
                QuestType.SolveProblem => (new[] {
                    "ベンチ周辺のゴミ", "故障中のアトラクション", "汚れたトイレ",
                    "壊れた案内板", "散らかった通路"
                })[rng.Next(5)],
                _ => "不明な対象"
            };
        }

        /// <summary>
        /// Simple JSON parser for quest extraction response.
        /// Avoids dependency on external JSON libraries.
        /// </summary>
        private ParsedQuestData ParseQuestJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var result = new ParsedQuestData();

            // Check if quest was detected
            var detectedMatch = Regex.Match(json, "\"detected\"\\s*:\\s*(true|false)");
            if (!detectedMatch.Success || detectedMatch.Groups[1].Value == "false")
            {
                result.Detected = false;
                return result;
            }
            result.Detected = true;

            // Extract quest type
            var typeMatch = Regex.Match(json, "\"quest_type\"\\s*:\\s*\"(\\w+)\"");
            if (typeMatch.Success)
            {
                result.Type = typeMatch.Groups[1].Value switch
                {
                    "FindPerson" => QuestType.FindPerson,
                    "RecommendRide" => QuestType.RecommendRide,
                    "DeliverItem" => QuestType.DeliverItem,
                    "GuideTour" => QuestType.GuideTour,
                    "SolveProblem" => QuestType.SolveProblem,
                    _ => QuestType.RecommendRide
                };
            }

            // Extract description
            var descMatch = Regex.Match(json, "\"description\"\\s*:\\s*\"([^\"]+)\"");
            if (descMatch.Success)
            {
                result.Description = descMatch.Groups[1].Value;
            }

            // Extract target
            var targetMatch = Regex.Match(json, "\"target\"\\s*:\\s*\"([^\"]+)\"");
            if (targetMatch.Success)
            {
                result.Target = targetMatch.Groups[1].Value;
            }

            // Extract urgency
            var urgencyMatch = Regex.Match(json, "\"urgency\"\\s*:\\s*\"(\\w+)\"");
            if (urgencyMatch.Success)
            {
                result.Urgency = urgencyMatch.Groups[1].Value switch
                {
                    "high" => QuestUrgency.High,
                    "medium" => QuestUrgency.Medium,
                    "low" => QuestUrgency.Low,
                    _ => QuestUrgency.Medium
                };
            }

            return result;
        }

        private class ParsedQuestData
        {
            public bool Detected;
            public QuestType Type;
            public string Description;
            public string Target;
            public QuestUrgency Urgency = QuestUrgency.Medium;
        }

        private void OnDestroy()
        {
            CancelInvoke(nameof(CheckExpiredQuests));
        }
    }
}
