// ============================================================
// ThemeParkGame - NPCDialogueSystem
// NPC台詞テンプレート管理・LLMフォールバック会話生成
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.AI
{
    /// <summary>
    /// NPCの会話テーマ（状況別カテゴリ）
    /// </summary>
    public enum DialogueCategory
    {
        Greeting,           // 挨拶
        Farewell,           // 別れの挨拶
        Compliment,         // パークへの称賛
        Complaint,          // 不満・苦情
        AttractionReview,   // アトラクションの感想
        FoodReview,         // 食事の感想
        WeatherComment,     // 天候についてのコメント
        Request,            // お願い・リクエスト
        Idle,               // 雑談・独り言
        Excited,            // 興奮・感動
        Tired,              // 疲れ
        Lost                // 迷子
    }

    /// <summary>
    /// テンプレートから選択された台詞データ
    /// </summary>
    public class DialogueLine
    {
        public string Text;
        public DialogueCategory Category;
        public ConversationStyle Style;
        public float HappinessImpact;
    }

    /// <summary>
    /// NPC台詞テンプレートの管理・選択システム。
    ///
    /// 【役割】
    /// ・LLM APIが利用不可の場合のフォールバック台詞を提供
    /// ・NPCの性格・状態・パーク状況に応じた台詞テンプレートを選択
    /// ・AIConversationManagerと連携し、オフライン時でも自然な会話を実現
    /// ・来場者の感情バブルに連動した台詞を生成
    ///
    /// 【設計方針】
    /// ・ConversationStyle × DialogueCategory の組合せで台詞プールを管理
    /// ・VisitorID をシードとした決定論的選択で再現性を確保
    /// ・パーク状態（天候・混雑度・時間帯）による台詞バリエーション
    /// </summary>
    public class NPCDialogueSystem : MonoBehaviour
    {
        public static NPCDialogueSystem Instance { get; private set; }

        // 台詞テンプレートプール: (Style, Category) → 台詞リスト
        private readonly Dictionary<(ConversationStyle, DialogueCategory), string[]> _templates
            = new Dictionary<(ConversationStyle, DialogueCategory), string[]>();

        // 汎用台詞プール（スタイル不問）
        private readonly Dictionary<DialogueCategory, string[]> _genericTemplates
            = new Dictionary<DialogueCategory, string[]>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            BuildTemplates();
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// NPC性格と状況に応じた台詞を取得する。
        /// AIConversationManager.GetFallbackGreeting() の拡張版。
        /// </summary>
        public DialogueLine GetDialogue(
            int visitorId,
            ConversationStyle style,
            DialogueCategory category,
            float happiness = 50f)
        {
            string text = PickTemplate(visitorId, style, category);

            // パーク状況で動的置換
            text = ApplyContextReplacements(text);

            return new DialogueLine
            {
                Text = text,
                Category = category,
                Style = style,
                HappinessImpact = GetHappinessImpact(category)
            };
        }

        /// <summary>
        /// NPCPersonalityから自動的にカテゴリを推定して台詞を返す。
        /// </summary>
        public DialogueLine GetContextualDialogue(int visitorId, NPCPersonality personality, VisitorParameters parameters)
        {
            var category = InferCategory(parameters);
            return GetDialogue(visitorId, personality.Style, category, parameters != null ? parameters.Happiness : 50f);
        }

        /// <summary>
        /// 感情バブルタイプから適切なNPC台詞を返す。
        /// EmotionBubble表示時に呼び出してテキスト吹き出しも同時表示可能。
        /// </summary>
        public DialogueLine GetDialogueForEmotion(int visitorId, ConversationStyle style, EmotionBubbleType emotion)
        {
            var category = MapEmotionToCategory(emotion);
            return GetDialogue(visitorId, style, category);
        }

        /// <summary>
        /// プレイヤーのメッセージに対するフォールバック応答を生成する。
        /// LLM APIが利用不可の場合にAIConversationManagerから呼ばれる。
        /// </summary>
        public string GetFallbackResponse(int visitorId, ConversationStyle style, string playerMessage, float happiness)
        {
            // プレイヤーメッセージのキーワードから応答カテゴリを推定
            var category = InferCategoryFromPlayerMessage(playerMessage, happiness);
            var line = GetDialogue(visitorId, style, category, happiness);
            return line.Text;
        }

        /// <summary>
        /// NPCが自発的に発する独り言を取得する。
        /// VisitorAI の Idle 状態などで使用。
        /// </summary>
        public string GetIdleComment(int visitorId, ConversationStyle style, float happiness)
        {
            var category = happiness >= 70f ? DialogueCategory.Compliment
                         : happiness <= 30f ? DialogueCategory.Complaint
                         : DialogueCategory.Idle;
            return GetDialogue(visitorId, style, category, happiness).Text;
        }

        // ================================================================
        // Category Inference
        // ================================================================

        private DialogueCategory InferCategory(VisitorParameters parameters)
        {
            if (parameters == null) return DialogueCategory.Greeting;

            if (parameters.Happiness <= 20f) return DialogueCategory.Complaint;
            if (parameters.Fatigue >= 80f) return DialogueCategory.Tired;
            if (parameters.Happiness >= 85f) return DialogueCategory.Excited;
            if (parameters.Hunger >= 70f) return DialogueCategory.FoodReview;

            // 天候コメント
            if (GameManager.Instance?.WeatherSystem != null)
            {
                var weather = GameManager.Instance.WeatherSystem.CurrentWeather;
                if (weather == WeatherType.Rain || weather == WeatherType.Snow || weather == WeatherType.Storm)
                    return DialogueCategory.WeatherComment;
            }

            return DialogueCategory.Idle;
        }

        private DialogueCategory InferCategoryFromPlayerMessage(string message, float happiness)
        {
            if (string.IsNullOrEmpty(message)) return DialogueCategory.Idle;

            string lower = message.ToLowerInvariant();

            // 挨拶系
            if (lower.Contains("こんにちは") || lower.Contains("やあ") || lower.Contains("はじめまして") || lower.Contains("hello"))
                return DialogueCategory.Greeting;

            // 食べ物系
            if (lower.Contains("食べ") || lower.Contains("おいしい") || lower.Contains("ご飯") || lower.Contains("料理"))
                return DialogueCategory.FoodReview;

            // アトラクション系
            if (lower.Contains("乗り物") || lower.Contains("アトラクション") || lower.Contains("楽し") || lower.Contains("怖"))
                return DialogueCategory.AttractionReview;

            // 天気系
            if (lower.Contains("天気") || lower.Contains("雨") || lower.Contains("暑") || lower.Contains("寒"))
                return DialogueCategory.WeatherComment;

            // 困っている系
            if (lower.Contains("困") || lower.Contains("迷") || lower.Contains("どこ") || lower.Contains("探"))
                return DialogueCategory.Lost;

            // 別れ系
            if (lower.Contains("バイバイ") || lower.Contains("さようなら") || lower.Contains("また") || lower.Contains("帰"))
                return DialogueCategory.Farewell;

            // 感情ベース
            return happiness >= 60f ? DialogueCategory.Compliment : DialogueCategory.Idle;
        }

        private static DialogueCategory MapEmotionToCategory(EmotionBubbleType emotion)
        {
            switch (emotion)
            {
                case EmotionBubbleType.LookingForExit:
                case EmotionBubbleType.Lost:
                    return DialogueCategory.Lost;

                case EmotionBubbleType.Hungry:
                case EmotionBubbleType.Thirsty:
                case EmotionBubbleType.LookingForFood:
                case EmotionBubbleType.CurrentlyEating:
                    return DialogueCategory.FoodReview;

                case EmotionBubbleType.FoodTastesBad:
                case EmotionBubbleType.TooExpensive:
                case EmotionBubbleType.TooDirty:
                case EmotionBubbleType.NotExcitingEnough:
                case EmotionBubbleType.LongWait:
                case EmotionBubbleType.Boring:
                    return DialogueCategory.Complaint;

                case EmotionBubbleType.BestRide:
                case EmotionBubbleType.LovingIt:
                    return DialogueCategory.Excited;

                case EmotionBubbleType.Interesting:
                case EmotionBubbleType.BestShop:
                    return DialogueCategory.Compliment;

                case EmotionBubbleType.Resting:
                    return DialogueCategory.Tired;

                case EmotionBubbleType.LookingForToilet:
                    return DialogueCategory.Request;

                case EmotionBubbleType.NoMoney:
                    return DialogueCategory.Complaint;

                default:
                    return DialogueCategory.Idle;
            }
        }

        // ================================================================
        // Template Selection
        // ================================================================

        private string PickTemplate(int visitorId, ConversationStyle style, DialogueCategory category)
        {
            // スタイル別テンプレートを優先、なければ汎用
            if (_templates.TryGetValue((style, category), out string[] pool) && pool.Length > 0)
            {
                return pool[SeededIndex(visitorId, pool.Length)];
            }

            if (_genericTemplates.TryGetValue(category, out string[] generic) && generic.Length > 0)
            {
                return generic[SeededIndex(visitorId, generic.Length)];
            }

            return "...";
        }

        private static int SeededIndex(int visitorId, int count)
        {
            // visitorId + 現在のフレーム数で少しランダム性を加える
            int seed = visitorId * 31 + (Time.frameCount / 60);
            return Mathf.Abs(seed) % count;
        }

        private string ApplyContextReplacements(string text)
        {
            // 天候
            if (GameManager.Instance?.WeatherSystem != null)
            {
                string weatherName = GameManager.Instance.WeatherSystem.CurrentWeather switch
                {
                    WeatherType.Sunny => "晴れ",
                    WeatherType.Cloudy => "曇り",
                    WeatherType.Rain => "雨",
                    WeatherType.Snow => "雪",
                    WeatherType.Storm => "嵐",
                    WeatherType.HeatWave => "猛暑",
                    _ => "晴れ"
                };
                text = text.Replace("{weather}", weatherName);
            }

            // 時間帯
            if (GameManager.Instance?.TimeManager != null)
            {
                int hour = GameManager.Instance.TimeManager.CurrentHour;
                string timeOfDay = hour < 12 ? "午前" : hour < 17 ? "午後" : "夕方";
                text = text.Replace("{time}", timeOfDay);
            }

            return text;
        }

        private static float GetHappinessImpact(DialogueCategory category)
        {
            return category switch
            {
                DialogueCategory.Greeting => 1f,
                DialogueCategory.Compliment => 0f,
                DialogueCategory.Excited => 0f,
                DialogueCategory.Complaint => 0f,
                DialogueCategory.Farewell => 0.5f,
                _ => 0f
            };
        }

        // ================================================================
        // Template Database
        // ================================================================

        private void BuildTemplates()
        {
            // ---- Formal (丁寧語) ----
            _templates[(ConversationStyle.Formal, DialogueCategory.Greeting)] = new[] {
                "こんにちは。今日はとても良い天気ですね。",
                "初めまして。素敵なパークですね。",
                "こんにちは。楽しませていただいています。",
                "お声がけいただきありがとうございます。"
            };
            _templates[(ConversationStyle.Formal, DialogueCategory.Farewell)] = new[] {
                "それでは、失礼いたします。良い一日を。",
                "楽しい時間をありがとうございました。",
                "また来園したいと思います。さようなら。"
            };
            _templates[(ConversationStyle.Formal, DialogueCategory.Compliment)] = new[] {
                "スタッフの方々がとても親切で感動しました。",
                "パーク全体の雰囲気がとても素晴らしいですね。",
                "アトラクションのクオリティが高くて驚きました。",
                "お手入れが行き届いていて気持ちがいいです。"
            };
            _templates[(ConversationStyle.Formal, DialogueCategory.Complaint)] = new[] {
                "少し待ち時間が長いように感じます。",
                "もう少し清潔だと嬉しいのですが…。",
                "価格が少し高めではないでしょうか。",
                "休憩できる場所がもっとあると助かります。"
            };
            _templates[(ConversationStyle.Formal, DialogueCategory.AttractionReview)] = new[] {
                "先ほど乗ったアトラクションはとても素晴らしかったです。",
                "想像以上にスリルがありました。良い体験でした。",
                "子供も大人も楽しめる良いアトラクションですね。"
            };
            _templates[(ConversationStyle.Formal, DialogueCategory.FoodReview)] = new[] {
                "こちらのフードは美味しいですね。",
                "種類が豊富で迷ってしまいます。",
                "もう少しリーズナブルだと嬉しいですね。"
            };
            _templates[(ConversationStyle.Formal, DialogueCategory.WeatherComment)] = new[] {
                "今日の{weather}は少し心配ですね。",
                "{weather}ですが、それでも楽しんでいます。",
                "この{weather}の中でも来てよかったです。"
            };
            _templates[(ConversationStyle.Formal, DialogueCategory.Tired)] = new[] {
                "少し疲れてきました。ベンチで休ませていただきます。",
                "歩き回ったので少し休憩が必要ですね。"
            };
            _templates[(ConversationStyle.Formal, DialogueCategory.Idle)] = new[] {
                "次はどこに行こうか考えています。",
                "素敵な{time}ですね。",
                "パークを散歩するのも気持ちがいいです。"
            };

            // ---- Casual (カジュアル) ----
            _templates[(ConversationStyle.Casual, DialogueCategory.Greeting)] = new[] {
                "あ、こんにちは！楽しんでる？",
                "やっほー！今日めっちゃいい天気だね！",
                "お、声かけてくれたんだ。ありがとう！",
                "こんにちは〜！ここ初めて来たんだよね。"
            };
            _templates[(ConversationStyle.Casual, DialogueCategory.Farewell)] = new[] {
                "じゃあね〜！また来るよ！",
                "バイバイ！今日は楽しかった〜！",
                "そろそろ帰るね。ありがとう！"
            };
            _templates[(ConversationStyle.Casual, DialogueCategory.Compliment)] = new[] {
                "ここマジで楽しい！来てよかった〜！",
                "めっちゃいいパークだね！友達にもおすすめする！",
                "スタッフさんみんな優しくて最高！",
                "写真映えするスポットがいっぱいあるね！"
            };
            _templates[(ConversationStyle.Casual, DialogueCategory.Complaint)] = new[] {
                "うーん、ちょっと並びすぎじゃない？",
                "お腹減ったけどどこも混んでるなぁ…。",
                "ちょっと高くない？もう少し安いといいのに。",
                "ゴミ箱もっと増やしてほしいな〜。"
            };
            _templates[(ConversationStyle.Casual, DialogueCategory.AttractionReview)] = new[] {
                "さっきの乗り物やばかった！最高！",
                "めっちゃ怖かったけど楽しかった〜！",
                "あれもう一回乗りたい！",
                "思ったより普通だったかな…。"
            };
            _templates[(ConversationStyle.Casual, DialogueCategory.FoodReview)] = new[] {
                "ここのフード美味しいね！おすすめある？",
                "さっき食べたやつめっちゃ美味しかった！",
                "ちょっと量少なくない？お腹まだ減ってる。"
            };
            _templates[(ConversationStyle.Casual, DialogueCategory.WeatherComment)] = new[] {
                "{weather}かぁ…まあ楽しいからいいけど！",
                "うわ、{weather}だ！屋内のアトラクション行こっかな。",
                "この{weather}もなんか雰囲気あっていいかも！"
            };
            _templates[(ConversationStyle.Casual, DialogueCategory.Excited)] = new[] {
                "うわあああ！すっごい！！",
                "やばい！テンション上がる〜！！",
                "最高最高最高！来てよかった！！",
                "これ今日一番楽しい！！"
            };
            _templates[(ConversationStyle.Casual, DialogueCategory.Tired)] = new[] {
                "ちょっと疲れちゃった〜。休憩しよ。",
                "足パンパンだわ…ベンチどこ？"
            };
            _templates[(ConversationStyle.Casual, DialogueCategory.Idle)] = new[] {
                "次どこ行こっかな〜。",
                "いい{time}だね〜。",
                "写真撮ろっかな。映えスポットどこだろ。"
            };

            // ---- Childlike (子供) ----
            _templates[(ConversationStyle.Childlike, DialogueCategory.Greeting)] = new[] {
                "ねーねー！ここ楽しいね！",
                "こんにちは！おにいちゃん（おねえちゃん）！",
                "あのね、今日ね、遊園地に来たの！",
                "わーい！お話しよう！"
            };
            _templates[(ConversationStyle.Childlike, DialogueCategory.Farewell)] = new[] {
                "バイバーイ！またねー！",
                "帰りたくないよー！もっと遊びたい！",
                "また来ようねー！楽しかった！"
            };
            _templates[(ConversationStyle.Childlike, DialogueCategory.Compliment)] = new[] {
                "ここ大好き！！毎日来たい！",
                "すっごーい！キラキラしてる！",
                "パパ（ママ）またここ来ようね！"
            };
            _templates[(ConversationStyle.Childlike, DialogueCategory.Complaint)] = new[] {
                "もう疲れたよー…。",
                "お腹すいたー！何か食べたい！",
                "まだ乗れないの？いつ？",
                "つまんなーい。早く次行こうよー。"
            };
            _templates[(ConversationStyle.Childlike, DialogueCategory.AttractionReview)] = new[] {
                "わー！すっごく楽しかった！もう一回！",
                "ちょっと怖かったけど…楽しかった！",
                "ぐるぐるってなったよ！面白かった！"
            };
            _templates[(ConversationStyle.Childlike, DialogueCategory.FoodReview)] = new[] {
                "アイス食べたい！アイス！",
                "これ美味しい！おかわりー！",
                "んー、あんまり好きじゃないかも…。"
            };
            _templates[(ConversationStyle.Childlike, DialogueCategory.Excited)] = new[] {
                "わーーーい！！すごいすごい！！",
                "キャーーー！たのしーーー！！",
                "見て見て！あれすごいよ！！"
            };
            _templates[(ConversationStyle.Childlike, DialogueCategory.Tired)] = new[] {
                "もう歩けないよー。抱っこ…。",
                "ねむい…。でもまだ遊びたい…。"
            };
            _templates[(ConversationStyle.Childlike, DialogueCategory.Lost)] = new[] {
                "パパ（ママ）どこー？",
                "ここどこ？出口わかんない…。",
                "トイレ行きたい…どこにあるの？"
            };
            _templates[(ConversationStyle.Childlike, DialogueCategory.Idle)] = new[] {
                "あ！蝶々だ！",
                "次はあれ乗りたいなー！",
                "お花きれいだねー。"
            };

            // ---- PoliteElderly (丁寧な年配者) ----
            _templates[(ConversationStyle.PoliteElderly, DialogueCategory.Greeting)] = new[] {
                "あら、こんにちは。素敵なパークですわね。",
                "やあ、こんにちは。久しぶりの遊園地じゃのう。",
                "こんにちは。孫と一緒に来たんですよ。",
                "おお、声をかけてくれてありがとう。"
            };
            _templates[(ConversationStyle.PoliteElderly, DialogueCategory.Farewell)] = new[] {
                "では、お先に失礼しますわ。ありがとう。",
                "そろそろ帰りますかのう。楽しかったですよ。",
                "また孫を連れて来たいものですね。さようなら。"
            };
            _templates[(ConversationStyle.PoliteElderly, DialogueCategory.Compliment)] = new[] {
                "昔の遊園地とは違って、随分立派になりましたねえ。",
                "若い人も年寄りも楽しめる、良いパークですわね。",
                "お花がきれいに手入れされていて感心しますよ。",
                "スタッフの方が親切で、安心して過ごせますね。"
            };
            _templates[(ConversationStyle.PoliteElderly, DialogueCategory.Complaint)] = new[] {
                "もう少し座れる場所があるとありがたいのですが。",
                "ちょっと音が大きくて、耳にこたえますなあ。",
                "最近のものは値段が高くなりましたねえ…。"
            };
            _templates[(ConversationStyle.PoliteElderly, DialogueCategory.Tired)] = new[] {
                "少し足が疲れてきましたのう。一休みしますかね。",
                "年を取ると長く歩けなくなってのう…。"
            };
            _templates[(ConversationStyle.PoliteElderly, DialogueCategory.WeatherComment)] = new[] {
                "今日は{weather}ですねえ。体に気をつけないと。",
                "{weather}ですが、それもまた一興ですかのう。"
            };
            _templates[(ConversationStyle.PoliteElderly, DialogueCategory.Idle)] = new[] {
                "孫が楽しそうで何よりですよ。",
                "昔はよくこういう場所に来たものですがねえ。",
                "いい{time}ですねえ。のんびりしますかのう。"
            };

            // ---- Enthusiastic (ハイテンション) ----
            _templates[(ConversationStyle.Enthusiastic, DialogueCategory.Greeting)] = new[] {
                "うわー！こんにちは！今日めっちゃ楽しい！！",
                "ハロー！！テンション上がりまくり！！",
                "やっほーーー！最高の一日だね！！"
            };
            _templates[(ConversationStyle.Enthusiastic, DialogueCategory.Farewell)] = new[] {
                "ありがとう！！最高だった！！また絶対来る！！",
                "帰りたくなーーーい！！でもまた来る！！",
                "今日は人生最高の日！！バイバイ！！"
            };
            _templates[(ConversationStyle.Enthusiastic, DialogueCategory.Compliment)] = new[] {
                "ここ本当に最っ高！！全部すごい！！",
                "もう感動しっぱなし！！涙出そう！！",
                "世界一のパーク！！間違いない！！"
            };
            _templates[(ConversationStyle.Enthusiastic, DialogueCategory.Complaint)] = new[] {
                "えーーー！もっと早く乗りたいよー！！",
                "お腹減りすぎてテンション下がっちゃう！！"
            };
            _templates[(ConversationStyle.Enthusiastic, DialogueCategory.AttractionReview)] = new[] {
                "やばいやばいやばい！！最高すぎ！！",
                "叫びすぎて声枯れた！！でもまた乗る！！",
                "人生で一番楽しいアトラクション！！！"
            };
            _templates[(ConversationStyle.Enthusiastic, DialogueCategory.Excited)] = new[] {
                "うおおおおお！！すごすぎる！！！",
                "最高最高最高最高！！！！",
                "テンションぶち上がり！！！！",
                "これはヤバい！！みんなに自慢する！！"
            };
            _templates[(ConversationStyle.Enthusiastic, DialogueCategory.Idle)] = new[] {
                "次は何に乗ろう！！全部制覇するぞ！！",
                "ワクワクが止まらない！！",
                "写真100枚くらい撮った！！まだ撮る！！"
            };

            // ---- Generic (スタイル不問) ----
            _genericTemplates[DialogueCategory.Greeting] = new[] {
                "こんにちは！",
                "やあ！",
                "お、こんにちは。"
            };
            _genericTemplates[DialogueCategory.Farewell] = new[] {
                "さようなら！",
                "またね！",
                "バイバイ！"
            };
            _genericTemplates[DialogueCategory.Request] = new[] {
                "トイレはどこですか？",
                "おすすめの乗り物を教えてください。",
                "出口はどちらですか？",
                "写真を撮ってもらえますか？"
            };
            _genericTemplates[DialogueCategory.Lost] = new[] {
                "ちょっと迷っちゃいました…。",
                "出口がわからなくて…。",
                "トイレを探しているんですが…。"
            };
            _genericTemplates[DialogueCategory.WeatherComment] = new[] {
                "今日は{weather}ですね。",
                "{weather}だけど楽しいです！"
            };
            _genericTemplates[DialogueCategory.Idle] = new[] {
                "...",
                "ふむふむ。",
                "いい天気だなぁ。",
                "次は何しようかな。"
            };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
