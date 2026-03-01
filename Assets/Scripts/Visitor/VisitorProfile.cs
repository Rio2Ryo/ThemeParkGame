// ============================================================
// ThemeParkGame - VisitorProfile
// 来場者の個別アイデンティティ（AI会話用プロファイル）
// 名前・年齢・性格特性・訪問目的・体験記憶を管理
// ============================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// 来場者の性格特性。AI会話の口調やリアクションに影響する。
    /// 【ゲームデザイン】性格特性はLLMプロンプトに埋め込まれ、
    /// 住人視点モードでの会話の多様性を生み出す。
    /// </summary>
    [Serializable]
    public struct PersonalityTraits
    {
        /// <summary>外向性（0-1）。高いほど話好き、低いほど内向的</summary>
        [Range(0f, 1f)] public float Extroversion;

        /// <summary>冒険心（0-1）。高いほどスリル好き、低いほど安全志向</summary>
        [Range(0f, 1f)] public float Adventurousness;

        /// <summary>忍耐力（0-1）。高いほど行列に強い、低いほどすぐ怒る</summary>
        [Range(0f, 1f)] public float Patience;

        /// <summary>倹約度（0-1）。高いほど値段にうるさい、低いほど気前が良い</summary>
        [Range(0f, 1f)] public float Frugality;

        /// <summary>好奇心（0-1）。高いほど色々なアトラクションを試したがる</summary>
        [Range(0f, 1f)] public float Curiosity;

        /// <summary>ランダムに性格特性を生成する</summary>
        public static PersonalityTraits GenerateRandom(VisitorType type)
        {
            var traits = new PersonalityTraits();

            // VisitorTypeに応じた傾向を持たせつつランダム性を付与
            switch (type)
            {
                case VisitorType.Kids:
                    traits.Extroversion = UnityEngine.Random.Range(0.5f, 1.0f);
                    traits.Adventurousness = UnityEngine.Random.Range(0.3f, 0.7f);
                    traits.Patience = UnityEngine.Random.Range(0.1f, 0.4f);
                    traits.Frugality = UnityEngine.Random.Range(0.0f, 0.3f);
                    traits.Curiosity = UnityEngine.Random.Range(0.6f, 1.0f);
                    break;

                case VisitorType.Young:
                    traits.Extroversion = UnityEngine.Random.Range(0.4f, 0.9f);
                    traits.Adventurousness = UnityEngine.Random.Range(0.6f, 1.0f);
                    traits.Patience = UnityEngine.Random.Range(0.2f, 0.6f);
                    traits.Frugality = UnityEngine.Random.Range(0.3f, 0.7f);
                    traits.Curiosity = UnityEngine.Random.Range(0.4f, 0.8f);
                    break;

                case VisitorType.Family:
                    traits.Extroversion = UnityEngine.Random.Range(0.3f, 0.7f);
                    traits.Adventurousness = UnityEngine.Random.Range(0.2f, 0.5f);
                    traits.Patience = UnityEngine.Random.Range(0.4f, 0.8f);
                    traits.Frugality = UnityEngine.Random.Range(0.4f, 0.8f);
                    traits.Curiosity = UnityEngine.Random.Range(0.3f, 0.6f);
                    break;

                case VisitorType.Couple:
                    traits.Extroversion = UnityEngine.Random.Range(0.3f, 0.7f);
                    traits.Adventurousness = UnityEngine.Random.Range(0.3f, 0.7f);
                    traits.Patience = UnityEngine.Random.Range(0.3f, 0.7f);
                    traits.Frugality = UnityEngine.Random.Range(0.2f, 0.6f);
                    traits.Curiosity = UnityEngine.Random.Range(0.4f, 0.8f);
                    break;

                case VisitorType.Senior:
                    traits.Extroversion = UnityEngine.Random.Range(0.2f, 0.6f);
                    traits.Adventurousness = UnityEngine.Random.Range(0.1f, 0.3f);
                    traits.Patience = UnityEngine.Random.Range(0.5f, 0.9f);
                    traits.Frugality = UnityEngine.Random.Range(0.5f, 0.9f);
                    traits.Curiosity = UnityEngine.Random.Range(0.2f, 0.5f);
                    break;

                case VisitorType.VIP:
                    traits.Extroversion = UnityEngine.Random.Range(0.4f, 0.8f);
                    traits.Adventurousness = UnityEngine.Random.Range(0.3f, 0.7f);
                    traits.Patience = UnityEngine.Random.Range(0.1f, 0.4f);
                    traits.Frugality = UnityEngine.Random.Range(0.0f, 0.2f);
                    traits.Curiosity = UnityEngine.Random.Range(0.5f, 0.9f);
                    break;

                case VisitorType.Influencer:
                    traits.Extroversion = UnityEngine.Random.Range(0.7f, 1.0f);
                    traits.Adventurousness = UnityEngine.Random.Range(0.5f, 0.9f);
                    traits.Patience = UnityEngine.Random.Range(0.2f, 0.5f);
                    traits.Frugality = UnityEngine.Random.Range(0.1f, 0.4f);
                    traits.Curiosity = UnityEngine.Random.Range(0.7f, 1.0f);
                    break;
            }

            return traits;
        }

        /// <summary>性格を自然言語で記述する（LLMプロンプト用）</summary>
        public string Describe()
        {
            var parts = new List<string>();

            if (Extroversion > 0.7f) parts.Add("outgoing and talkative");
            else if (Extroversion < 0.3f) parts.Add("shy and reserved");

            if (Adventurousness > 0.7f) parts.Add("thrill-seeking");
            else if (Adventurousness < 0.3f) parts.Add("cautious and safety-conscious");

            if (Patience > 0.7f) parts.Add("patient");
            else if (Patience < 0.3f) parts.Add("easily frustrated");

            if (Frugality > 0.7f) parts.Add("budget-conscious");
            else if (Frugality < 0.3f) parts.Add("generous spender");

            if (Curiosity > 0.7f) parts.Add("curious about everything");
            else if (Curiosity < 0.3f) parts.Add("sticks to what they know");

            return parts.Count > 0 ? string.Join(", ", parts) : "an average visitor";
        }
    }

    /// <summary>
    /// 来場者のグループ関係を定義する。
    /// 【ゲームデザイン】グループ単位で行動することで、パーク内の自然な人の流れを再現。
    /// 家族連れは子供の好みに引っ張られ、カップルは二人乗りアトラクションを好む。
    /// </summary>
    [Serializable]
    public enum RelationshipGroup
    {
        Solo,           // 一人客
        Couple,         // カップル
        Family,         // 家族連れ
        FriendGroup,    // 友人グループ
        SchoolTrip      // 修学旅行
    }

    /// <summary>
    /// アトラクション体験の記憶。訪問したアトラクションとその評価を記録。
    /// 【ゲームデザイン】記憶は同じアトラクションへの再訪判定やAI会話の話題に使う。
    /// 素晴らしい体験はSNS投稿イベントを発生させ、パークの知名度に貢献する。
    /// </summary>
    [Serializable]
    public struct AttractionMemory
    {
        /// <summary>アトラクションID</summary>
        public int AttractionId;

        /// <summary>アトラクション名</summary>
        public string AttractionName;

        /// <summary>体験時の満足度（-100 ~ +100）</summary>
        public float SatisfactionScore;

        /// <summary>体験時刻（ゲーム内時間）</summary>
        public float VisitTime;

        /// <summary>嘔吐したか</summary>
        public bool VomitedAfter;

        /// <summary>待ち時間（秒）</summary>
        public float WaitDuration;

        /// <summary>体験の感想を自然言語で返す（LLMプロンプト用）</summary>
        public string DescribeExperience()
        {
            string rating;
            if (SatisfactionScore >= 80f) rating = "absolutely loved";
            else if (SatisfactionScore >= 50f) rating = "enjoyed";
            else if (SatisfactionScore >= 20f) rating = "thought it was okay";
            else if (SatisfactionScore >= 0f) rating = "was not impressed by";
            else rating = "disliked";

            string result = $"{rating} '{AttractionName}'";

            if (VomitedAfter)
                result += " (got sick afterwards)";
            if (WaitDuration > 120f)
                result += $" (waited {WaitDuration:F0}s in line)";

            return result;
        }
    }

    /// <summary>
    /// 来場者個人のプロファイル。AI会話における「人格」を構成する。
    ///
    /// 【ゲームデザイン】
    /// 住人視点モード（ResidentView）では、プレイヤーが来場者に話しかけることができる。
    /// この時、VisitorProfileの情報がLLMプロンプトに埋め込まれ、
    /// 一人ひとりが固有の名前・性格・体験記憶を持つリアルなNPCとして応答する。
    /// これにより、「テーマパークにいる生きた人々」の感覚を演出する。
    /// </summary>
    [Serializable]
    public class VisitorProfile
    {
        // ---- 基本情報 ----

        [Header("基本情報")]
        [SerializeField] private string visitorName;
        [SerializeField] private int age;
        [SerializeField] private VisitorType visitorType;
        [SerializeField] private RelationshipGroup relationshipGroup;

        [Header("性格")]
        [SerializeField] private PersonalityTraits personality;

        [Header("訪問情報")]
        [SerializeField] private string visitPurpose;
        [SerializeField] private List<AttractionCategory> favoriteRideTypes = new List<AttractionCategory>();
        [SerializeField] private List<AttractionCategory> dislikedRideTypes = new List<AttractionCategory>();

        // 体験記憶
        private List<AttractionMemory> attractionMemories = new List<AttractionMemory>();

        // お土産購入回数
        private int souvenirPurchaseCount;

        // グループ内の他メンバーID
        private List<int> groupMemberIds = new List<int>();

        // ---- プロパティ ----

        public string VisitorName => visitorName;
        public int Age => age;
        public VisitorType Type => visitorType;
        public RelationshipGroup Group => relationshipGroup;
        public PersonalityTraits Personality => personality;
        public string VisitPurpose => visitPurpose;
        public IReadOnlyList<AttractionCategory> FavoriteRideTypes => favoriteRideTypes;
        public IReadOnlyList<AttractionCategory> DislikedRideTypes => dislikedRideTypes;
        public IReadOnlyList<AttractionMemory> AttractionMemories => attractionMemories;
        public IReadOnlyList<int> GroupMemberIds => groupMemberIds;

        /// <summary>体験したアトラクション数</summary>
        public int RidesExperienced => attractionMemories.Count;

        /// <summary>お気に入りアトラクション（最高満足度の体験）</summary>
        public AttractionMemory? FavoriteAttraction
        {
            get
            {
                if (attractionMemories.Count == 0) return null;

                AttractionMemory best = attractionMemories[0];
                for (int i = 1; i < attractionMemories.Count; i++)
                {
                    if (attractionMemories[i].SatisfactionScore > best.SatisfactionScore)
                        best = attractionMemories[i];
                }
                return best;
            }
        }

        /// <summary>最悪のアトラクション体験</summary>
        public AttractionMemory? WorstAttraction
        {
            get
            {
                if (attractionMemories.Count == 0) return null;

                AttractionMemory worst = attractionMemories[0];
                for (int i = 1; i < attractionMemories.Count; i++)
                {
                    if (attractionMemories[i].SatisfactionScore < worst.SatisfactionScore)
                        worst = attractionMemories[i];
                }
                return worst;
            }
        }

        // ---- 名前データベース（簡易） ----

        private static readonly string[] FirstNames = {
            "Alex", "Jordan", "Taylor", "Casey", "Morgan",
            "Riley", "Quinn", "Avery", "Harper", "Skyler",
            "Sakura", "Yuki", "Haru", "Sora", "Ren",
            "Emma", "Liam", "Oliver", "Ava", "Noah",
            "Mia", "Lucas", "Sophia", "Ethan", "Isabella",
            "Kai", "Luna", "Leo", "Chloe", "Max"
        };

        private static readonly string[] LastNames = {
            "Smith", "Johnson", "Williams", "Brown", "Jones",
            "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
            "Tanaka", "Suzuki", "Yamamoto", "Watanabe", "Sato",
            "Kim", "Lee", "Park", "Chen", "Wang",
            "Muller", "Schmidt", "Fischer", "Weber", "Meyer"
        };

        private static readonly string[] VisitPurposes = {
            "a fun day out",
            "celebrating a birthday",
            "their first visit ever",
            "a repeat visit to their favorite park",
            "a special occasion",
            "trying out the new attractions",
            "a holiday trip",
            "a weekend getaway",
            "a school trip reward",
            "their annual park visit"
        };

        // ---- 初期化 ----

        /// <summary>
        /// VisitorTypeに応じてランダムなプロファイルを生成する。
        /// 【ゲームデザイン】名前・年齢・性格・好みの組み合わせにより、
        /// 数千人規模の来場者でもそれぞれが個性を持つ。
        /// </summary>
        public void Initialize(VisitorType type)
        {
            visitorType = type;
            visitorName = GenerateName();
            age = GenerateAge(type);
            personality = PersonalityTraits.GenerateRandom(type);
            relationshipGroup = DetermineRelationshipGroup(type);
            visitPurpose = VisitPurposes[UnityEngine.Random.Range(0, VisitPurposes.Length)];
            GenerateFavoriteRideTypes(type);

            attractionMemories.Clear();
            groupMemberIds.Clear();
            souvenirPurchaseCount = 0;
        }

        /// <summary>グループメンバーIDを追加する</summary>
        public void AddGroupMember(int memberId)
        {
            if (!groupMemberIds.Contains(memberId))
            {
                groupMemberIds.Add(memberId);
            }
        }

        /// <summary>アトラクション体験を記録する</summary>
        public void RecordAttractionVisit(AttractionMemory memory)
        {
            attractionMemories.Add(memory);
        }

        /// <summary>お土産の購入回数を取得する</summary>
        public int GetSouvenirPurchaseCount() => souvenirPurchaseCount;

        /// <summary>お土産の購入を記録する</summary>
        public void RecordSouvenirPurchase()
        {
            souvenirPurchaseCount++;
        }

        /// <summary>
        /// 指定したアトラクションを既に体験済みかどうか
        /// </summary>
        public bool HasVisitedAttraction(int attractionId)
        {
            for (int i = 0; i < attractionMemories.Count; i++)
            {
                if (attractionMemories[i].AttractionId == attractionId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 指定したアトラクションカテゴリが好みかどうか。
        /// 【ゲームデザイン】好みのカテゴリのアトラクションは満足度にボーナスが付く。
        /// </summary>
        public bool LikesCategory(AttractionCategory category)
        {
            return favoriteRideTypes.Contains(category);
        }

        /// <summary>指定したアトラクションカテゴリが苦手かどうか</summary>
        public bool DislikesCategory(AttractionCategory category)
        {
            return dislikedRideTypes.Contains(category);
        }

        // ---- リピーターシステム用ヘルパー ----

        /// <summary>体験したアトラクション数</summary>
        public int VisitedAttractionCount => attractionMemories.Count;

        /// <summary>総支出額（来場者パラメータ側で追跡）</summary>
        public float TotalSpent { get; set; }

        /// <summary>
        /// 満足度の高い上位N件のアトラクション名を取得する。
        /// リピーターシステムのお気に入り記録に使用。
        /// </summary>
        public List<string> GetTopAttractionNames(int count)
        {
            var result = new List<string>();
            if (attractionMemories.Count == 0) return result;

            // 満足度でソート（降順）
            var sorted = new List<AttractionMemory>(attractionMemories);
            sorted.Sort((a, b) => b.SatisfactionScore.CompareTo(a.SatisfactionScore));

            for (int i = 0; i < Mathf.Min(count, sorted.Count); i++)
            {
                if (!string.IsNullOrEmpty(sorted[i].AttractionName))
                    result.Add(sorted[i].AttractionName);
            }
            return result;
        }

        /// <summary>最もつまらなかったアトラクション名を取得する</summary>
        public string GetWorstAttractionName()
        {
            var worst = WorstAttraction;
            return worst?.AttractionName ?? "";
        }

        // ---- LLMプロンプト生成 ----

        /// <summary>
        /// LLM向けのプロンプトコンテキストを生成する。
        /// 住人視点モードでプレイヤーが来場者に話しかけた際、
        /// この情報がシステムプロンプトに挿入される。
        ///
        /// 【ゲームデザイン】
        /// 来場者は自分の体験を基に会話する。楽しい体験が多ければポジティブに、
        /// 不満が溜まっていればネガティブに応答する。これにより、
        /// プレイヤーは自分のパーク経営の結果を「住人の声」として直接聞くことができる。
        /// </summary>
        /// <param name="currentParams">現在のパラメータ（気分の反映用）</param>
        /// <returns>LLMに渡すキャラクターコンテキスト文字列</returns>
        public string GenerateLLMPromptContext(VisitorParameters currentParams)
        {
            var sb = new StringBuilder();

            // 基本キャラクター情報
            sb.AppendLine("=== Visitor Character Profile ===");
            sb.AppendLine($"Name: {visitorName}");
            sb.AppendLine($"Age: {age}");
            sb.AppendLine($"Visitor Type: {visitorType}");
            sb.AppendLine($"Group: {DescribeGroup()}");
            sb.AppendLine($"Purpose of visit: {visitPurpose}");
            sb.AppendLine($"Personality: {personality.Describe()}");

            // 現在の気分
            sb.AppendLine();
            sb.AppendLine("=== Current Mood ===");
            sb.AppendLine($"Overall happiness: {DescribeMoodLevel(currentParams.Happiness)}");
            sb.AppendLine($"Excitement level: {DescribeMoodLevel(currentParams.Excitement)}");

            if (currentParams.IsHungry)
                sb.AppendLine("Currently feeling hungry.");
            if (currentParams.IsThirsty)
                sb.AppendLine("Currently feeling thirsty.");
            if (currentParams.Nausea > 40f)
                sb.AppendLine("Feeling a bit nauseous.");
            if (currentParams.NeedsToilet)
                sb.AppendLine("Really needs to find a restroom.");
            if (!currentParams.HasMoney)
                sb.AppendLine("Has run out of money and is frustrated about it.");

            // 体験記憶
            if (attractionMemories.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== Today's Experiences ===");

                // 最新5件の体験を報告
                int startIdx = Mathf.Max(0, attractionMemories.Count - 5);
                for (int i = startIdx; i < attractionMemories.Count; i++)
                {
                    sb.AppendLine($"- {attractionMemories[i].DescribeExperience()}");
                }

                if (FavoriteAttraction.HasValue && FavoriteAttraction.Value.SatisfactionScore >= 70f)
                {
                    sb.AppendLine($"Favorite experience so far: '{FavoriteAttraction.Value.AttractionName}'");
                }
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("Has not ridden any attractions yet today.");
            }

            // 好み情報
            sb.AppendLine();
            sb.AppendLine("=== Preferences ===");
            if (favoriteRideTypes.Count > 0)
            {
                sb.AppendLine($"Enjoys: {string.Join(", ", favoriteRideTypes)}");
            }
            if (dislikedRideTypes.Count > 0)
            {
                sb.AppendLine($"Avoids: {string.Join(", ", dislikedRideTypes)}");
            }

            // 会話の指示
            sb.AppendLine();
            sb.AppendLine("=== Conversation Guidelines ===");
            sb.AppendLine("Respond in character as this theme park visitor.");
            sb.AppendLine("Keep responses concise (2-3 sentences max).");
            sb.AppendLine("React based on current mood and experiences.");
            sb.AppendLine("If asked about the park, give honest feedback based on experiences.");

            return sb.ToString();
        }

        /// <summary>SNS投稿コンテンツを生成する（パーク知名度に影響）</summary>
        public string GenerateSNSPostContent(VisitorParameters currentParams)
        {
            var sb = new StringBuilder();

            if (currentParams.Happiness >= 80f && FavoriteAttraction.HasValue)
            {
                sb.Append($"Had an amazing time at the park! ");
                sb.Append($"'{FavoriteAttraction.Value.AttractionName}' was incredible! ");
                sb.Append($"Definitely coming back! #ThemePark #BestDayEver");
            }
            else if (currentParams.Happiness >= 50f)
            {
                sb.Append($"Spent the day at the theme park. ");
                sb.Append($"It was a decent experience overall. ");
                sb.Append($"Rode {attractionMemories.Count} attractions. #ThemePark");
            }
            else
            {
                sb.Append($"Not the best day at the park... ");
                if (currentParams.IsHungry || currentParams.IsThirsty)
                    sb.Append("Could use more food/drink options. ");
                if (currentParams.Nausea > 50f)
                    sb.Append("Some rides made me sick. ");
                sb.Append("#ThemePark #CouldBeBetter");
            }

            return sb.ToString();
        }

        // ---- 内部ヘルパー ----

        private string GenerateName()
        {
            string first = FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)];
            string last = LastNames[UnityEngine.Random.Range(0, LastNames.Length)];
            return $"{first} {last}";
        }

        private int GenerateAge(VisitorType type)
        {
            switch (type)
            {
                case VisitorType.Kids:   return UnityEngine.Random.Range(6, 14);
                case VisitorType.Young:  return UnityEngine.Random.Range(15, 25);
                case VisitorType.Family: return UnityEngine.Random.Range(28, 45);
                case VisitorType.Couple: return UnityEngine.Random.Range(18, 40);
                case VisitorType.Senior: return UnityEngine.Random.Range(55, 75);
                case VisitorType.VIP:    return UnityEngine.Random.Range(25, 60);
                case VisitorType.Influencer: return UnityEngine.Random.Range(20, 35);
                default: return 30;
            }
        }

        private RelationshipGroup DetermineRelationshipGroup(VisitorType type)
        {
            switch (type)
            {
                case VisitorType.Kids:
                    // キッズは修学旅行か家族連れ
                    return UnityEngine.Random.value > 0.5f
                        ? RelationshipGroup.SchoolTrip
                        : RelationshipGroup.Family;

                case VisitorType.Young:
                    float youngRoll = UnityEngine.Random.value;
                    if (youngRoll < 0.4f) return RelationshipGroup.FriendGroup;
                    if (youngRoll < 0.7f) return RelationshipGroup.Couple;
                    return RelationshipGroup.Solo;

                case VisitorType.Family:
                    return RelationshipGroup.Family;

                case VisitorType.Couple:
                    return RelationshipGroup.Couple;

                case VisitorType.Senior:
                    return UnityEngine.Random.value > 0.5f
                        ? RelationshipGroup.Couple
                        : RelationshipGroup.Solo;

                case VisitorType.VIP:
                    return RelationshipGroup.Solo;

                case VisitorType.Influencer:
                    return UnityEngine.Random.value > 0.7f
                        ? RelationshipGroup.FriendGroup
                        : RelationshipGroup.Solo;

                default:
                    return RelationshipGroup.Solo;
            }
        }

        /// <summary>
        /// VisitorTypeに応じた好みのアトラクションカテゴリを設定する。
        /// 【ゲームデザイン】タイプごとに好みのアトラクションが異なることで、
        /// プレイヤーは多様な来場者層に対応するパーク設計を求められる。
        /// Kids: おとなしめ、Young: スリル系、Family: ショー系、Senior: 展望系
        /// </summary>
        private void GenerateFavoriteRideTypes(VisitorType type)
        {
            favoriteRideTypes.Clear();
            dislikedRideTypes.Clear();

            switch (type)
            {
                case VisitorType.Kids:
                    // キッズ: 横回転（コーヒーカップ等）、乗り物系が好き。G系は苦手。
                    favoriteRideTypes.Add(AttractionCategory.HorizontalRotation);
                    favoriteRideTypes.Add(AttractionCategory.RideAttraction);
                    if (UnityEngine.Random.value > 0.5f)
                        favoriteRideTypes.Add(AttractionCategory.ShowAttraction);
                    dislikedRideTypes.Add(AttractionCategory.GForce);
                    break;

                case VisitorType.Young:
                    // ヤング: G系（ジェットコースター）、縦回転が好き。展望系は退屈。
                    favoriteRideTypes.Add(AttractionCategory.GForce);
                    favoriteRideTypes.Add(AttractionCategory.VerticalRotation);
                    if (UnityEngine.Random.value > 0.5f)
                        favoriteRideTypes.Add(AttractionCategory.RideAttraction);
                    dislikedRideTypes.Add(AttractionCategory.Observation);
                    break;

                case VisitorType.Family:
                    // ファミリー: ショー系、乗り物系、展望系が好き。
                    favoriteRideTypes.Add(AttractionCategory.ShowAttraction);
                    favoriteRideTypes.Add(AttractionCategory.RideAttraction);
                    favoriteRideTypes.Add(AttractionCategory.Observation);
                    break;

                case VisitorType.Couple:
                    // カップル: 展望系（観覧車）、ショー系が好き。
                    favoriteRideTypes.Add(AttractionCategory.Observation);
                    favoriteRideTypes.Add(AttractionCategory.ShowAttraction);
                    if (UnityEngine.Random.value > 0.5f)
                        favoriteRideTypes.Add(AttractionCategory.VerticalRotation);
                    break;

                case VisitorType.Senior:
                    // シニア: 展望系、ショー系が好き。激しい系は苦手。
                    favoriteRideTypes.Add(AttractionCategory.Observation);
                    favoriteRideTypes.Add(AttractionCategory.ShowAttraction);
                    dislikedRideTypes.Add(AttractionCategory.GForce);
                    dislikedRideTypes.Add(AttractionCategory.VerticalRotation);
                    break;

                case VisitorType.VIP:
                    // VIP: 全般的に楽しめるが、特にショー系とG系を好む。
                    favoriteRideTypes.Add(AttractionCategory.ShowAttraction);
                    favoriteRideTypes.Add(AttractionCategory.GForce);
                    favoriteRideTypes.Add(AttractionCategory.Observation);
                    break;

                case VisitorType.Influencer:
                    // インフルエンサー: 映える体験を求める。ショー系＆G系＆展望系
                    favoriteRideTypes.Add(AttractionCategory.ShowAttraction);
                    favoriteRideTypes.Add(AttractionCategory.GForce);
                    favoriteRideTypes.Add(AttractionCategory.Observation);
                    if (UnityEngine.Random.value > 0.3f)
                        favoriteRideTypes.Add(AttractionCategory.RideAttraction);
                    break;
            }
        }

        private string DescribeGroup()
        {
            switch (relationshipGroup)
            {
                case RelationshipGroup.Solo: return "visiting alone";
                case RelationshipGroup.Couple: return "visiting with a partner";
                case RelationshipGroup.Family: return $"visiting with family ({groupMemberIds.Count + 1} members)";
                case RelationshipGroup.FriendGroup: return $"visiting with friends ({groupMemberIds.Count + 1} people)";
                case RelationshipGroup.SchoolTrip: return "on a school trip";
                default: return "visiting";
            }
        }

        private string DescribeMoodLevel(float value)
        {
            if (value >= 90f) return "extremely high";
            if (value >= 70f) return "high";
            if (value >= 50f) return "moderate";
            if (value >= 30f) return "low";
            return "very low";
        }
    }
}
