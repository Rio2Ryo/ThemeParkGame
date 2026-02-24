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
        SpaceZone         // スペースゾーン
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
        VIP         // VIP
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
        TalkingToPlayer  // AI会話中
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
        Scientist      // サイエンティスト - 研究開発
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
        ResearchLab
    }

    /// <summary>天候</summary>
    public enum Weather
    {
        Sunny,
        Cloudy,
        Rainy,
        Snowy,
        Hot
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

    /// <summary>視点モード</summary>
    public enum ViewMode
    {
        GodView,      // 神視点（経営モード）
        ResidentView, // 住人視点（探索モード）
        FirstPerson   // 一人称視点（アトラクション搭乗）
    }

    /// <summary>研究開発状態</summary>
    public enum ResearchState
    {
        Locked,
        Available,
        InProgress,
        Completed
    }
}
