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
        ResearchLab
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
}
