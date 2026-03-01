// ============================================================
// ThemeParkGame - Game Enumerations
// 全ゲームシステムで共有される列挙型定義
// ============================================================

namespace ThemeParkGame.Core
{
    /// <summary>テーマゾーン</summary>
    public enum ThemeZone
    {
        LostKingdom,      // ロストキングダム
        HalloweenWorld,   // ハロウィーンワールド
        Wonderland,       // ワンダーランド
        SpaceZone,        // スペースゾーン
        FutureCity        // 未来都市
    }

    /// <summary>ゲーム状態</summary>
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        ScenarioSelect,
        BuildMode,
        FirstPersonMode,  // 一人称視点モード
        ResidentMode,     // 住人視点モード
        GameOver          // ゲーム終了・結果表示
    }

    /// <summary>来場者タイプ</summary>
    public enum VisitorType
    {
        Kids,       // キッズ
        Young,      // ヤング
        Family,     // ファミリー
        Couple,     // カップル
        Senior,     // シニア
        VIP,        // VIP
        Influencer  // インフルエンサー（SNS拡散型）
    }

    /// <summary>来場者の行動状態</summary>
    public enum VisitorBehaviorState
    {
        Idle,
        WalkingToAttraction,
        WaitingInQueue,
        RidingAttraction,
        WalkingToShop,
        Eating,
        Drinking,
        WalkingToToilet,
        UsingToilet,
        Resting,
        WatchingEntertainment,
        LookingAtMap,
        Vomiting,
        LeavingPark,
        TalkingToPlayer,  // AI会話中
        WatchingParade,   // パレード鑑賞中
        TakingPhoto       // フォトスポットで撮影中
    }

    /// <summary>感情バブルの色</summary>
    public enum EmotionBubbleColor
    {
        Green,    // 迷子・探索
        Yellow,   // 食欲・渇き
        LightBlue,// 現在の状態
        Gray,     // 不満
        White,    // 評価
        Blue      // 最高評価
    }

    /// <summary>感情バブルタイプ</summary>
    public enum EmotionBubbleType
    {
        // 緑 - 迷子・探索
        LookingForExit,
        LookingForToilet,
        LookingForFood,
        Lost,

        // 黄 - 食欲・渇き
        Hungry,
        Thirsty,

        // 水色 - 現在の状態
        CurrentlyEating,
        RodeAllRides,
        NoMoney,
        Resting,

        // 灰 - 不満
        FoodTastesBad,
        TooExpensive,
        TooDirty,
        NotExcitingEnough,
        LongWait,

        // 白 - 評価
        Interesting,
        Average,
        Boring,

        // 青 - 最高評価
        BestRide,
        BestShop,
        LovingIt
    }

    /// <summary>スタッフ種別</summary>
    public enum StaffType
    {
        Mechanic,      // メカニック - 修理・点検
        Cleaner,       // スイーパー - 清掃
        Entertainer,   // エンターテイナー - 楽しませる
        Guard,         // ガードマン - 治安維持
        Scientist,     // サイエンティスト - 研究開発
        Doctor,        // ドクター - 体調不良の来場者を治療
        Vendor,        // 移動販売員 - 園内を巡回して飲食を販売
        Gardener       // 園芸師 - パークの美観・ムード評価を向上
    }

    /// <summary>スタッフの行動状態</summary>
    public enum StaffBehaviorState
    {
        Idle,
        Working,
        MovingToTask,
        Resting,
        OnStrike
    }

    /// <summary>
    /// 勤務シフト。スタッフの稼働時間帯を定義する。
    /// 【ゲームデザイン】
    /// ・Morning (6:00-14:00): 通常給与。開園直後の来場者対応。
    /// ・Day (14:00-22:00): 通常給与。ピークタイム対応。
    /// ・Night (22:00-6:00): 夜間割増25%。夜間パレードやメンテナンスに対応。
    /// ・AllDay: 終日勤務。連続勤務による疲労蓄積ペナルティあり。
    /// </summary>
    public enum ShiftType
    {
        Morning,    // 朝シフト (6:00-14:00)
        Day,        // 昼シフト (14:00-22:00)
        Night,      // 夜シフト (22:00-6:00) ※夜間割増25%
        AllDay      // 終日勤務 ※疲労蓄積×1.5
    }

    /// <summary>アトラクションカテゴリ</summary>
    public enum AttractionCategory
    {
        GForce,             // G系 - ジェットコースター等
        VerticalRotation,   // 縦回転 - 観覧車等
        HorizontalRotation, // 横回転 - コーヒーカップ等
        Observation,        // 展望系 - 展望台等
        ShowAttraction,     // 見せ物系 - お化け屋敷等
        RideAttraction      // 乗り物系 - ゴーカート等
    }

    /// <summary>施設タイプ</summary>
    public enum FacilityType
    {
        Attraction,
        FoodShop,
        DrinkShop,
        SouvenirShop,
        Toilet,
        Bench,
        TrashCan,
        InfoBoard,
        Pathway,
        Decoration,
        StaffRoom,
        ResearchLab,
        PhotoSpot       // フォトスポット
    }

    /// <summary>天候</summary>
    public enum Weather
    {
        Sunny,
        Cloudy,
        Rainy,
        Snowy,
        Hot,
        Typhoon,       // 台風
        Thunderstorm   // 雷雨
    }

    /// <summary>パーク認定証カテゴリ</summary>
    public enum CertificateCategory
    {
        Fame,        // 知名度
        Safety,      // 安全性
        Comfort,     // 快適性
        Excitement,  // 興奮度
        Mood         // ムード
    }

    /// <summary>シナリオ国</summary>
    public enum ScenarioCountry
    {
        France,
        Egypt,
        India,
        China,
        Japan,
        UnitedKingdom,
        UnitedStates,
        Brazil,
        Australia,
        Russia
    }

    /// <summary>シナリオ難易度</summary>
    public enum ScenarioDifficulty
    {
        Easy,    // やさしい
        Normal,  // ふつう
        Hard     // むずかしい
    }

    /// <summary>ゲーム難易度（サンドボックスモード用）</summary>
    public enum GameDifficulty
    {
        Easy,    // 簡単: スポーン遅め・初期資金多め
        Normal,  // 普通: 標準設定
        Hard     // 難しい: スポーン速い・初期資金少ない
    }

    /// <summary>視点モード</summary>
    public enum ViewMode
    {
        GodView,      // 神視点（経営モード）
        ResidentView, // 住人視点（探索モード）
        FirstPerson   // 一人称視点（アトラクション搭乗）
    }

    /// <summary>来場者ライフサイクルフェーズ（マクロ状態）</summary>
    public enum VisitorLifecyclePhase
    {
        Waiting,   // 入園後～体験開始前（移動・行列待ち・散策）
        Enjoying,  // 体験中（搭乗・食事・ショー鑑賞・休憩など）
        Leaving    // 退園中
    }

    /// <summary>研究開発状態</summary>
    public enum ResearchState
    {
        Locked,
        Available,
        InProgress,
        Completed
    }

    /// <summary>
    /// スタッフスキルID。職種別スキルツリーの個別スキルを識別する。
    /// 各職種に分岐A/分岐Bの2系統、各3段階のスキルを持つ。
    /// </summary>
    public enum StaffSkillId
    {
        // メカニック 分岐A: 高速修理系
        Mechanic_QuickRepair,       // 修理速度+30%
        Mechanic_DiagnosticEye,     // 故障予兆を検知（Condition50%で通知）
        Mechanic_MasterEngineer,    // 修理で追加Condition+10回復

        // メカニック 分岐B: 予防保全系
        Mechanic_PreventiveCare,    // 定期点検のCondition回復+10
        Mechanic_DurabilityExpert,  // 担当アトラクションの劣化速度-20%
        Mechanic_OverhaulMaster,    // オーバーホール時間-40%

        // エンターテイナー 分岐A: 群衆魅了系
        Entertainer_CrowdPleaser,   // パフォーマンス効果範囲+50%
        Entertainer_ShowStopper,    // 観客幸福度ボーナス+5
        Entertainer_Superstar,      // パフォーマンス中SNS投稿発生

        // エンターテイナー 分岐B: VIP接待系
        Entertainer_VIPHost,        // VIP幸福度回復速度+30%
        Entertainer_PersonalGuide,  // VIPリクエスト達成時ボーナス×1.5
        Entertainer_CelebrityCharm, // VIP出現率+5%

        // クリーナー 分岐A: 高速清掃系
        Cleaner_SwiftSweep,         // 清掃速度+30%
        Cleaner_DeepClean,          // 清掃効果持続時間2倍
        Cleaner_SpotlessZone,       // 担当ゾーンの汚れ発生率-25%

        // クリーナー 分岐B: 衛生管理系
        Cleaner_HygieneInspector,   // 汚れ検知範囲+50%
        Cleaner_WasteExpert,        // ゴミ箱容量2倍効果
        Cleaner_SanitationMaster,   // ゾーン衛生評価+10

        // ガードマン 分岐A: 追跡系
        Guard_EagleEye,             // フーリガン検知範囲+50%
        Guard_SwiftPursuit,         // 追跡速度+30%
        Guard_IronGrip,             // フーリガン捕獲時間-50%

        // ガードマン 分岐B: 抑止系
        Guard_Deterrence,           // 存在だけでフーリガン発生率-20%
        Guard_CrowdControl,         // 混雑エリアの来場者幸福度ペナルティ-30%
        Guard_PeaceKeeper,          // ゾーン治安評価+15

        // サイエンティスト 分岐A: 研究加速系
        Scientist_RapidResearch,    // 研究速度+25%
        Scientist_Eureka,           // 研究完了時に次の研究コスト-20%
        Scientist_Visionary,        // 同時研究スロット+1

        // サイエンティスト 分岐B: 応用研究系
        Scientist_AppliedScience,   // 研究完了アトラクションの興奮度+0.5
        Scientist_CostOptimizer,    // 研究完了アトラクションの維持費-15%
        Scientist_InnovationLeader, // 全アトラクションのアップグレードコスト-10%

        // ドクター 分岐A: 治療系
        Doctor_QuickHeal,           // 治療速度+30%
        Doctor_WideRange,           // 治療効果範囲+50%
        Doctor_Miracle,             // 嘔吐回復時に幸福度+10回復

        // ドクター 分岐B: 予防系
        Doctor_Prevention,          // 周囲の来場者の嘔吐率-30%
        Doctor_HealthAdvisor,       // 高嘔吐率アトラクション警告表示
        Doctor_WellnessZone,        // 担当ゾーンの嘔吐率-20%

        // 販売員 分岐A: セールス系
        Vendor_Salesman,            // ショップ売上+15%
        Vendor_Upseller,            // お土産追加購入率+20%
        Vendor_GoldenTouch,         // ショップ周辺の来場者の購買意欲UP

        // 販売員 分岐B: サービス系
        Vendor_SpeedyService,       // 販売速度+30%
        Vendor_FriendlyFace,        // 購入後の幸福度ボーナス+5
        Vendor_CustomerExpert,      // 在庫切れ予測＆自動補充

        // 園芸師 分岐A: 美観系
        Gardener_BloomMaster,       // 植栽美観効果+30%
        Gardener_SeasonalExpert,    // 季節デコレーション効果+25%
        Gardener_GardenDesigner,    // ゾーンムード評価+15

        // 園芸師 分岐B: 効率系
        Gardener_QuickGreen,        // 園芸作業速度+30%
        Gardener_LowMaintenance,    // 植栽の維持費-25%
        Gardener_EcoFriendly        // パーク全体の来場者幸福度微増+2
    }

    /// <summary>マーケティングチャネル種別</summary>
    public enum MarketingChannel
    {
        TvCommercial,       // TV CM（全VisitorType集客ブースト）
        WebAdvertising,     // Web広告（Young/Couple特化）
        FlyerDistribution,  // チラシ配布（Family/Kids特化）
        InfluencerInvite    // インフルエンサー招待（SNS拡散倍増）
    }
}
