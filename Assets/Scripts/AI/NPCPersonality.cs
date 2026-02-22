// ============================================================
// ThemeParkGame - NPC Personality & Profile System
// NPC性格・プロフィール生成システム
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.AI
{
    /// <summary>Personality trait flags that can be combined.</summary>
    [Flags]
    public enum PersonalityTrait
    {
        None        = 0,
        Cheerful    = 1 << 0,   // 陽気
        Grumpy      = 1 << 1,   // 気難しい
        Adventurous = 1 << 2,   // 冒険好き
        Timid       = 1 << 3,   // 臆病
        Foodie      = 1 << 4,   // グルメ
        Thrifty     = 1 << 5,   // 節約家
        Romantic    = 1 << 6,   // ロマンチスト
        Energetic   = 1 << 7,   // 元気いっぱい
        Calm        = 1 << 8,   // 穏やか
        Curious     = 1 << 9,   // 好奇心旺盛
        PhotoLover  = 1 << 10,  // 写真好き
        Nostalgic   = 1 << 11   // 懐かしがり
    }

    /// <summary>Conversation style determines how the NPC speaks.</summary>
    public enum ConversationStyle
    {
        Formal,         // 丁寧語
        Casual,         // カジュアル
        Childlike,      // 子供風
        PoliteElderly,  // 上品な年配者
        Enthusiastic    // テンション高め
    }

    /// <summary>Visit purpose determines the NPC's primary goal in the park.</summary>
    public enum VisitPurpose
    {
        Thrill,         // スリルを求めて
        Relaxation,     // リラックス目的
        FoodTour,       // グルメ巡り
        DateSpot,       // デート
        FamilyOuting,   // 家族のお出かけ
        Anniversary,    // 記念日
        FirstVisit,     // 初めての来園
        Repeat,         // リピーター
        Photography,    // 写真撮影
        EventSpecial    // 特別イベント目当て
    }

    /// <summary>
    /// Represents the complete personality and profile of an NPC visitor.
    /// Used to generate consistent LLM system prompts and guide NPC behavior.
    /// </summary>
    [Serializable]
    public class NPCPersonality
    {
        // ---- Identity ----
        public int VisitorId { get; private set; }
        public string DisplayName { get; private set; }
        public int Age { get; private set; }
        public string Occupation { get; private set; }
        public VisitorType VisitorType { get; private set; }

        // ---- Personality ----
        public PersonalityTrait Traits { get; private set; }
        public ConversationStyle Style { get; private set; }
        public VisitPurpose Purpose { get; private set; }
        public string CompanionDescription { get; private set; }

        // ---- Dynamic State ----
        public float Happiness { get; set; }
        public float Hunger { get; set; }
        public float Fatigue { get; set; }
        public float RemainingMoney { get; set; }

        // ---- Memory ----
        public List<string> ConversationMemories { get; private set; } = new List<string>();
        public List<string> RidesExperienced { get; private set; } = new List<string>();
        public List<string> FoodEaten { get; private set; } = new List<string>();
        public List<string> SouvenirsBought { get; private set; } = new List<string>();
        public List<string> MemorableEvents { get; private set; } = new List<string>();
        public int TotalWaitTimeMinutes { get; set; }

        // ---- Cached prompt (regenerated when state changes significantly) ----
        private string _cachedSystemPrompt;
        private bool _promptDirty = true;

        // ================================================================
        // Japanese Name Generator Data
        // ================================================================

        private static readonly string[] LastNames = new[]
        {
            "田中", "鈴木", "佐藤", "高橋", "渡辺",
            "伊藤", "山本", "中村", "小林", "加藤",
            "吉田", "山田", "佐々木", "松本", "井上",
            "木村", "林", "斉藤", "清水", "山口",
            "森", "池田", "橋本", "阿部", "石川",
            "前田", "藤田", "小川", "後藤", "岡田",
            "村上", "長谷川", "近藤", "石井", "坂本",
            "遠藤", "青木", "藤井", "西村", "福田"
        };

        private static readonly string[] MaleFirstNames = new[]
        {
            "太郎", "健太", "翔太", "大輝", "蓮",
            "悠真", "陽翔", "湊", "朝陽", "蒼",
            "颯太", "陸", "海斗", "結翔", "樹",
            "一郎", "正男", "清", "勝", "誠"
        };

        private static readonly string[] FemaleFirstNames = new[]
        {
            "花子", "美咲", "陽菜", "凛", "結衣",
            "さくら", "葵", "結菜", "芽依", "紬",
            "莉子", "美月", "心春", "彩花", "愛莉",
            "和子", "幸子", "節子", "恵子", "洋子"
        };

        private static readonly string[] ChildFirstNames = new[]
        {
            "ゆうき", "はると", "あおい", "ひなた", "そら",
            "みく", "ゆい", "りん", "こはる", "めい"
        };

        private static readonly string[] Occupations = new[]
        {
            "会社員", "主婦", "学生", "エンジニア", "教師",
            "看護師", "デザイナー", "公務員", "フリーランス", "営業",
            "医師", "料理人", "アーティスト", "店員", "事務員"
        };

        private static readonly string[] ChildOccupations = new[]
        {
            "小学生", "中学生", "幼稚園児"
        };

        private static readonly string[] SeniorOccupations = new[]
        {
            "退職済み", "元教師", "元会社役員", "趣味で農業", "ボランティア活動"
        };

        // ================================================================
        // Factory Methods
        // ================================================================

        /// <summary>
        /// Generates a random NPC personality based on the given visitor type.
        /// Uses seeded randomness from visitor ID for reproducibility.
        /// </summary>
        public static NPCPersonality Generate(int visitorId, VisitorType visitorType)
        {
            // Use visitor ID as seed for deterministic personality generation
            var rng = new System.Random(visitorId);
            var personality = new NPCPersonality
            {
                VisitorId = visitorId,
                VisitorType = visitorType
            };

            // Generate age based on visitor type
            personality.Age = GenerateAge(visitorType, rng);

            // Generate name
            personality.DisplayName = GenerateName(visitorType, personality.Age, rng);

            // Generate occupation
            personality.Occupation = GenerateOccupation(visitorType, personality.Age, rng);

            // Assign personality traits based on visitor type
            personality.Traits = GenerateTraits(visitorType, rng);

            // Determine conversation style
            personality.Style = DetermineConversationStyle(visitorType, personality.Age);

            // Generate visit purpose
            personality.Purpose = GenerateVisitPurpose(visitorType, rng);

            // Generate companion description
            personality.CompanionDescription = GenerateCompanionDescription(visitorType, rng);

            // Initialize dynamic state
            personality.Happiness = 60f + (float)(rng.NextDouble() * 20.0);
            personality.Hunger = (float)(rng.NextDouble() * 30.0);
            personality.Fatigue = (float)(rng.NextDouble() * 20.0);
            personality.RemainingMoney = 3000f + (float)(rng.NextDouble() * 12000.0);

            return personality;
        }

        private static int GenerateAge(VisitorType type, System.Random rng)
        {
            return type switch
            {
                VisitorType.Kids => rng.Next(5, 13),
                VisitorType.Young => rng.Next(15, 30),
                VisitorType.Family => rng.Next(28, 50),
                VisitorType.Couple => rng.Next(18, 40),
                VisitorType.Senior => rng.Next(60, 80),
                VisitorType.VIP => rng.Next(30, 65),
                _ => rng.Next(20, 50)
            };
        }

        private static string GenerateName(VisitorType type, int age, System.Random rng)
        {
            string lastName = LastNames[rng.Next(LastNames.Length)];

            string[] firstNamePool;
            if (type == VisitorType.Kids || age < 13)
            {
                firstNamePool = ChildFirstNames;
            }
            else if (rng.Next(2) == 0)
            {
                firstNamePool = age > 50 ? MaleFirstNames : MaleFirstNames;
            }
            else
            {
                firstNamePool = age > 50 ? FemaleFirstNames : FemaleFirstNames;
            }

            string firstName = firstNamePool[rng.Next(firstNamePool.Length)];
            return $"{lastName} {firstName}";
        }

        private static string GenerateOccupation(VisitorType type, int age, System.Random rng)
        {
            if (age < 13) return ChildOccupations[rng.Next(ChildOccupations.Length)];
            if (age >= 60) return SeniorOccupations[rng.Next(SeniorOccupations.Length)];
            if (age < 20) return "学生";
            return Occupations[rng.Next(Occupations.Length)];
        }

        private static PersonalityTrait GenerateTraits(VisitorType type, System.Random rng)
        {
            // Each visitor type has a primary trait and may get 1-2 secondary traits
            PersonalityTrait primary = type switch
            {
                VisitorType.Kids => PersonalityTrait.Energetic | PersonalityTrait.Curious,
                VisitorType.Young => rng.Next(2) == 0 ? PersonalityTrait.Adventurous : PersonalityTrait.Cheerful,
                VisitorType.Family => PersonalityTrait.Calm,
                VisitorType.Couple => PersonalityTrait.Romantic,
                VisitorType.Senior => PersonalityTrait.Calm | PersonalityTrait.Nostalgic,
                VisitorType.VIP => PersonalityTrait.Curious,
                _ => PersonalityTrait.Cheerful
            };

            // Add 0-2 random secondary traits
            PersonalityTrait[] secondaryPool = new[]
            {
                PersonalityTrait.Foodie,
                PersonalityTrait.Thrifty,
                PersonalityTrait.PhotoLover,
                PersonalityTrait.Cheerful,
                PersonalityTrait.Grumpy,
                PersonalityTrait.Timid
            };

            int extraTraits = rng.Next(0, 3);
            for (int i = 0; i < extraTraits; i++)
            {
                var candidate = secondaryPool[rng.Next(secondaryPool.Length)];
                // Avoid conflicting traits
                if ((candidate == PersonalityTrait.Cheerful && primary.HasFlag(PersonalityTrait.Grumpy)) ||
                    (candidate == PersonalityTrait.Grumpy && primary.HasFlag(PersonalityTrait.Cheerful)) ||
                    (candidate == PersonalityTrait.Adventurous && primary.HasFlag(PersonalityTrait.Timid)) ||
                    (candidate == PersonalityTrait.Timid && primary.HasFlag(PersonalityTrait.Adventurous)))
                {
                    continue;
                }
                primary |= candidate;
            }

            return primary;
        }

        private static ConversationStyle DetermineConversationStyle(VisitorType type, int age)
        {
            if (age < 13) return ConversationStyle.Childlike;
            if (age >= 60) return ConversationStyle.PoliteElderly;

            return type switch
            {
                VisitorType.Kids => ConversationStyle.Childlike,
                VisitorType.Young => ConversationStyle.Casual,
                VisitorType.Family => ConversationStyle.Formal,
                VisitorType.Couple => ConversationStyle.Casual,
                VisitorType.Senior => ConversationStyle.PoliteElderly,
                VisitorType.VIP => ConversationStyle.Formal,
                _ => ConversationStyle.Formal
            };
        }

        private static VisitPurpose GenerateVisitPurpose(VisitorType type, System.Random rng)
        {
            return type switch
            {
                VisitorType.Kids => VisitPurpose.FamilyOuting,
                VisitorType.Young => (VisitPurpose)(new[] {
                    VisitPurpose.Thrill, VisitPurpose.FoodTour, VisitPurpose.Photography
                })[rng.Next(3)],
                VisitorType.Family => (VisitPurpose)(new[] {
                    VisitPurpose.FamilyOuting, VisitPurpose.FirstVisit, VisitPurpose.Repeat
                })[rng.Next(3)],
                VisitorType.Couple => (VisitPurpose)(new[] {
                    VisitPurpose.DateSpot, VisitPurpose.Anniversary
                })[rng.Next(2)],
                VisitorType.Senior => (VisitPurpose)(new[] {
                    VisitPurpose.Relaxation, VisitPurpose.Repeat
                })[rng.Next(2)],
                VisitorType.VIP => VisitPurpose.EventSpecial,
                _ => VisitPurpose.FirstVisit
            };
        }

        private static string GenerateCompanionDescription(VisitorType type, System.Random rng)
        {
            return type switch
            {
                VisitorType.Kids => "お父さんとお母さんと一緒",
                VisitorType.Young => (new[] {
                    "友達3人と一緒", "一人で来園", "サークルの仲間と一緒", "友達と二人で"
                })[rng.Next(4)],
                VisitorType.Family => (new[] {
                    "家族4人で来園", "妻と子供2人と一緒", "夫と娘と一緒", "三世代で来園"
                })[rng.Next(4)],
                VisitorType.Couple => "恋人と二人で",
                VisitorType.Senior => (new[] {
                    "妻と二人で", "夫と二人で", "孫と一緒に", "老人会の仲間と"
                })[rng.Next(4)],
                VisitorType.VIP => "秘書同行",
                _ => "一人で来園"
            };
        }

        // ================================================================
        // Prompt Generation
        // ================================================================

        /// <summary>
        /// Generates the complete LLM system prompt for this NPC personality.
        /// Caches the result and only regenerates when the state is marked dirty.
        /// </summary>
        public string GenerateSystemPrompt()
        {
            if (!_promptDirty && _cachedSystemPrompt != null)
                return _cachedSystemPrompt;

            string personalityDesc = BuildPersonalityDescription();
            string styleDesc = GetStyleDescription();
            string moodDesc = GetMoodDescription();

            _cachedSystemPrompt = PromptTemplates.NPCSystemPromptBase
                .Replace("{safety}", PromptTemplates.SafetyPreamble)
                .Replace("{name}", DisplayName)
                .Replace("{age}", Age.ToString())
                .Replace("{occupation}", Occupation)
                .Replace("{personality}", personalityDesc)
                .Replace("{visit_purpose}", GetVisitPurposeJP())
                .Replace("{companions}", CompanionDescription)
                .Replace("{conversation_style}", styleDesc)
                .Replace("{mood}", moodDesc)
                .Replace("{happiness}", Mathf.RoundToInt(Happiness).ToString())
                .Replace("{hunger}", GetHungerDescription())
                .Replace("{fatigue}", GetFatigueDescription())
                .Replace("{money}", Mathf.RoundToInt(RemainingMoney).ToString())
                .Replace("{park_context}", "（コンテキスト注入待ち）");

            _promptDirty = false;
            return _cachedSystemPrompt;
        }

        /// <summary>
        /// Generates a context update string reflecting the current park state.
        /// Called each time a conversation turn requires fresh context.
        /// </summary>
        public string GenerateContextUpdate(
            Weather weather,
            int hour,
            float congestionLevel,
            ThemeZone currentZone,
            string[] nearbyFacilities,
            string[] recentEvents,
            float parkReputation)
        {
            string facilitiesStr = nearbyFacilities != null && nearbyFacilities.Length > 0
                ? string.Join("、", nearbyFacilities)
                : "特になし";

            string eventsStr = recentEvents != null && recentEvents.Length > 0
                ? string.Join("、", recentEvents)
                : "特になし";

            string congestionDesc = congestionLevel switch
            {
                < 0.3f => "空いている",
                < 0.6f => "普通",
                < 0.8f => "混雑している",
                _ => "非常に混雑している"
            };

            return PromptTemplates.ParkContextTemplate
                .Replace("{weather}", PromptTemplates.GetWeatherDescriptionJP(weather))
                .Replace("{time}", $"{hour}:00")
                .Replace("{time_period}", PromptTemplates.GetTimePeriodJP(hour))
                .Replace("{congestion}", congestionDesc)
                .Replace("{current_zone}", PromptTemplates.GetZoneNameJP(currentZone))
                .Replace("{nearby_facilities}", facilitiesStr)
                .Replace("{recent_events}", eventsStr)
                .Replace("{reputation}", $"{Mathf.RoundToInt(parkReputation)}/100");
        }

        /// <summary>
        /// Marks the cached system prompt as stale, forcing regeneration on next access.
        /// Call this when NPC state changes significantly (e.g., happiness drops, hunger increases).
        /// </summary>
        public void MarkPromptDirty()
        {
            _promptDirty = true;
        }

        /// <summary>Adds a conversation memory entry (kept to most recent N entries).</summary>
        public void AddMemory(string memory)
        {
            const int MaxMemories = 10;
            ConversationMemories.Add(memory);
            if (ConversationMemories.Count > MaxMemories)
            {
                ConversationMemories.RemoveAt(0);
            }
        }

        /// <summary>Records that the NPC experienced a ride.</summary>
        public void RecordRide(string rideName)
        {
            if (!RidesExperienced.Contains(rideName))
                RidesExperienced.Add(rideName);
        }

        /// <summary>Records that the NPC ate something.</summary>
        public void RecordFood(string foodName)
        {
            FoodEaten.Add(foodName);
        }

        /// <summary>Records a souvenir purchase.</summary>
        public void RecordSouvenir(string souvenirName)
        {
            SouvenirsBought.Add(souvenirName);
        }

        /// <summary>Records a memorable event for SNS post generation.</summary>
        public void RecordMemorableEvent(string eventDescription)
        {
            MemorableEvents.Add(eventDescription);
        }

        // ================================================================
        // Internal Description Builders
        // ================================================================

        private string BuildPersonalityDescription()
        {
            var parts = new List<string>();

            if (Traits.HasFlag(PersonalityTrait.Cheerful)) parts.Add("陽気");
            if (Traits.HasFlag(PersonalityTrait.Grumpy)) parts.Add("気難しい");
            if (Traits.HasFlag(PersonalityTrait.Adventurous)) parts.Add("冒険好き");
            if (Traits.HasFlag(PersonalityTrait.Timid)) parts.Add("臆病");
            if (Traits.HasFlag(PersonalityTrait.Foodie)) parts.Add("グルメ");
            if (Traits.HasFlag(PersonalityTrait.Thrifty)) parts.Add("節約家");
            if (Traits.HasFlag(PersonalityTrait.Romantic)) parts.Add("ロマンチスト");
            if (Traits.HasFlag(PersonalityTrait.Energetic)) parts.Add("元気いっぱい");
            if (Traits.HasFlag(PersonalityTrait.Calm)) parts.Add("穏やか");
            if (Traits.HasFlag(PersonalityTrait.Curious)) parts.Add("好奇心旺盛");
            if (Traits.HasFlag(PersonalityTrait.PhotoLover)) parts.Add("写真好き");
            if (Traits.HasFlag(PersonalityTrait.Nostalgic)) parts.Add("懐かしがり");

            return parts.Count > 0 ? string.Join("、", parts) : "普通";
        }

        private string GetStyleDescription()
        {
            return Style switch
            {
                ConversationStyle.Formal => PromptTemplates.StyleFormal,
                ConversationStyle.Casual => PromptTemplates.StyleCasual,
                ConversationStyle.Childlike => PromptTemplates.StyleChildlike,
                ConversationStyle.PoliteElderly => PromptTemplates.StylePoliteElderly,
                ConversationStyle.Enthusiastic => PromptTemplates.StyleEnthusiastic,
                _ => PromptTemplates.StyleFormal
            };
        }

        private string GetMoodDescription()
        {
            return Happiness switch
            {
                >= 80f => "とても楽しんでいる",
                >= 60f => "まあまあ楽しんでいる",
                >= 40f => "普通",
                >= 20f => "少し不満",
                _ => "かなり不機嫌"
            };
        }

        private string GetHungerDescription()
        {
            return Hunger switch
            {
                >= 80f => "とてもお腹が空いている",
                >= 60f => "お腹が空いてきた",
                >= 30f => "少し小腹が空いた",
                _ => "満腹"
            };
        }

        private string GetFatigueDescription()
        {
            return Fatigue switch
            {
                >= 80f => "ヘトヘト",
                >= 60f => "かなり疲れている",
                >= 30f => "少し疲れた",
                _ => "元気"
            };
        }

        private string GetVisitPurposeJP()
        {
            return Purpose switch
            {
                VisitPurpose.Thrill => "スリル満点のアトラクションを楽しみたい",
                VisitPurpose.Relaxation => "のんびりリラックスしたい",
                VisitPurpose.FoodTour => "パーク内のグルメを堪能したい",
                VisitPurpose.DateSpot => "恋人とのデートを楽しみたい",
                VisitPurpose.FamilyOuting => "家族で楽しい思い出を作りたい",
                VisitPurpose.Anniversary => "記念日のお祝い",
                VisitPurpose.FirstVisit => "初めてのパーク体験",
                VisitPurpose.Repeat => "お気に入りのパークに再訪",
                VisitPurpose.Photography => "映える写真を撮りたい",
                VisitPurpose.EventSpecial => "特別イベントを楽しみたい",
                _ => "テーマパークを楽しみたい"
            };
        }

        /// <summary>
        /// Returns the personality description template text for use in LLM prompts.
        /// Selects the most dominant trait's template.
        /// </summary>
        public string GetPrimaryPersonalityTemplate()
        {
            if (Traits.HasFlag(PersonalityTrait.Grumpy)) return PromptTemplates.PersonalityGrumpy;
            if (Traits.HasFlag(PersonalityTrait.Adventurous)) return PromptTemplates.PersonalityAdventurous;
            if (Traits.HasFlag(PersonalityTrait.Timid)) return PromptTemplates.PersonalityTimid;
            if (Traits.HasFlag(PersonalityTrait.Foodie)) return PromptTemplates.PersonalityFoodie;
            if (Traits.HasFlag(PersonalityTrait.Thrifty)) return PromptTemplates.PersonalityThrifty;
            if (Traits.HasFlag(PersonalityTrait.Romantic)) return PromptTemplates.PersonalityCouple;
            if (Traits.HasFlag(PersonalityTrait.Nostalgic)) return PromptTemplates.PersonalitySenior;
            if (Traits.HasFlag(PersonalityTrait.Energetic) && Age < 13) return PromptTemplates.PersonalityChild;
            if (Traits.HasFlag(PersonalityTrait.Cheerful)) return PromptTemplates.PersonalityCheerful;
            return PromptTemplates.PersonalityCheerful;
        }
    }
}
