// ============================================================
// ThemeParkGame - LocalizationData
// 全UI文字列の日本語化データ
// ============================================================

namespace ThemeParkGame.Core
{
    /// <summary>
    /// ゲーム内の全テキストを日本語で提供する静的クラス。
    /// UIコンポーネントはこのクラスから文字列を取得する。
    /// </summary>
    public static class LocalizationData
    {
        // ============================================================
        // メインメニュー / スタート画面
        // ============================================================
        public const string GameTitle = "テーマパークワールド";
        public const string GameSubtitle = "〜夢のテーマパークを作ろう〜";
        public const string BtnNewGame = "ニューゲーム";
        public const string BtnContinue = "つづきから";
        public const string BtnSettings = "設定";
        public const string BtnCredits = "クレジット";
        public const string Version = "v3.0";

        // ============================================================
        // HUD - スコアボード
        // ============================================================
        public const string LabelVisitors = "来場者";
        public const string LabelRevenue = "総収益";
        public const string LabelSatisfaction = "満足度";
        public const string LabelMoney = "所持金";
        public const string LabelDay = "日目";
        public const string LabelMonth = "月";
        public const string LabelYear = "年";

        // ============================================================
        // HUD - 天候
        // ============================================================
        public const string WeatherSunny = "☀ 晴れ";
        public const string WeatherCloudy = "☁ 曇り";
        public const string WeatherRainy = "☂ 雨";
        public const string WeatherSnowy = "❄ 雪";
        public const string WeatherHeatWave = "🌡 猛暑";
        public const string WeatherStormy = "⚡ 嵐";

        // ============================================================
        // HUD - ボタン
        // ============================================================
        public const string BtnBuild = "建設";
        public const string BtnStaff = "スタッフ";
        public const string BtnResearch = "研究";
        public const string BtnLoan = "ローン";
        public const string BtnMenu = "メニュー";
        public const string BtnPause = "一時停止";

        // ============================================================
        // ポーズメニュー
        // ============================================================
        public const string PauseTitle = "一時停止";
        public const string BtnResume = "ゲームに戻る";
        public const string BtnSave = "セーブ";
        public const string BtnLoad = "ロード";
        public const string BtnAchievements = "実績";
        public const string BtnChallenges = "チャレンジ";
        public const string BtnAccidents = "事故管理";
        public const string BtnReviews = "口コミ";
        public const string BtnRivals = "ライバル";
        public const string BtnSales = "セール";
        public const string BtnSoundSettings = "サウンド設定";
        public const string BtnEventLog = "イベントログ";
        public const string BtnQuit = "ゲーム終了";

        // ============================================================
        // 建設パネル
        // ============================================================
        public const string BuildTitle = "建設メニュー";
        public const string TabAttractions = "アトラクション";
        public const string TabShops = "ショップ";
        public const string TabFacilities = "施設";
        public const string TabDecorations = "装飾";
        public const string LabelCost = "建設コスト";
        public const string LabelLocked = "研究未完了";
        public const string BtnPlace = "配置";
        public const string BtnCancel = "キャンセル";

        // ============================================================
        // スタッフパネル
        // ============================================================
        public const string StaffTitle = "スタッフ管理";
        public const string BtnHire = "雇用";
        public const string BtnFire = "解雇";
        public const string BtnTrain = "訓練";
        public const string BtnPatrol = "パトロール設定";
        public const string BtnRest = "休憩";
        public const string LabelSalary = "月給";
        public const string LabelSkill = "スキル";
        public const string LabelFatigue = "疲労";
        public const string LabelOnStrike = "ストライキ中！";

        // ============================================================
        // スタッフ種別
        // ============================================================
        public const string StaffMechanic = "メカニック";
        public const string StaffCleaner = "クリーナー";
        public const string StaffEntertainer = "エンターテイナー";
        public const string StaffGuard = "ガードマン";
        public const string StaffScientist = "サイエンティスト";
        public const string StaffDoctor = "ドクター";
        public const string StaffVendor = "販売員";
        public const string StaffGardener = "庭師";

        // ============================================================
        // 来場者情報パネル
        // ============================================================
        public const string VisitorInfoTitle = "来場者情報";
        public const string LabelHappiness = "幸福度";
        public const string LabelExcitement = "興奮度";
        public const string LabelNausea = "吐き気";
        public const string LabelHunger = "空腹度";
        public const string LabelThirst = "喉の渇き";
        public const string LabelToilet = "トイレ";
        public const string LabelCash = "所持金";
        public const string LabelRides = "搭乗回数";
        public const string BtnFirstPerson = "一人称視点";
        public const string BtnTalk = "話しかける";

        // ============================================================
        // 来場者タイプ
        // ============================================================
        public const string VisitorKids = "こども";
        public const string VisitorYoung = "若者";
        public const string VisitorFamily = "ファミリー";
        public const string VisitorCouple = "カップル";
        public const string VisitorSenior = "シニア";
        public const string VisitorVIP = "VIP";

        // ============================================================
        // 来場者状態
        // ============================================================
        public const string StateWalking = "移動中";
        public const string StateQueueing = "待ち行列";
        public const string StateRiding = "搭乗中";
        public const string StateShopping = "買い物中";
        public const string StateIdle = "散策中";
        public const string StateLeaving = "退園中";
        public const string StateResting = "休憩中";
        public const string StateVomiting = "気分不良";

        // ============================================================
        // アトラクション
        // ============================================================
        public const string AttractionStatusActive = "運営中";
        public const string AttractionStatusBroken = "故障中";
        public const string AttractionStatusClosed = "休止中";
        public const string AttractionStatusRepairing = "修理中";
        public const string LabelQueue = "待ち行列";
        public const string LabelCapacity = "定員";
        public const string LabelTicketPrice = "料金";
        public const string LabelExcitementRating = "興奮度";
        public const string LabelUpgrade = "アップグレード";
        public const string LabelDemolish = "撤去";
        public const string LabelMaxLevel = "最大レベル";

        // ============================================================
        // アトラクション名
        // ============================================================
        public const string AttrDragonCoaster = "ドラゴンコースター";
        public const string AttrMagicFerris = "マジカル観覧車";
        public const string AttrSpinCup = "スピンカップ";
        public const string AttrHauntedHouse = "お化け屋敷ダーク";
        public const string AttrMerryGoRound = "メリーゴーランド";
        public const string AttrFreeFall = "フリーフォール";
        public const string AttrPirateShip = "パイレーツシップ";
        public const string AttrSpaceCoaster = "スペースコースター";
        public const string AttrJungleCruise = "ジャングルクルーズ";
        public const string AttrSkyTower = "スカイタワー";

        // ============================================================
        // ショップ名
        // ============================================================
        public const string ShopDragonBurger = "ドラゴンバーガー";
        public const string ShopMagicJuice = "マジカルジュース";
        public const string ShopSouvenirCastle = "おみやげ城";
        public const string ShopIceCream = "アイスクリーム屋";
        public const string ShopPopcorn = "ポップコーン屋";

        // ============================================================
        // 施設名
        // ============================================================
        public const string FacilityToilet = "トイレ";
        public const string FacilityBench = "ベンチ";
        public const string FacilityStaffRoom = "スタッフルーム";
        public const string FacilityInfoBoard = "案内板";
        public const string FacilityTrashCan = "ゴミ箱";
        public const string FacilityResearchLab = "研究所";

        // ============================================================
        // 研究
        // ============================================================
        public const string ResearchTitle = "研究開発";
        public const string LabelResearchProgress = "研究進捗";
        public const string LabelScientists = "配属サイエンティスト";
        public const string LabelResearchCost = "研究コスト";
        public const string LabelPrerequisite = "前提条件";
        public const string BtnStartResearch = "研究開始";
        public const string ResearchComplete = "研究完了！";

        // ============================================================
        // ローン
        // ============================================================
        public const string LoanTitle = "ローン管理";
        public const string LoanBalance = "借入残高";
        public const string LoanCount = "借入件数";
        public const string LoanBorrow = "借り入れ";
        public const string LoanRepay = "繰り上げ返済";
        public const string LoanMonthlyPayment = "月々の返済額";
        public const string LoanInterestRate = "年利";

        // ============================================================
        // セーブ/ロード
        // ============================================================
        public const string SaveTitle = "セーブ";
        public const string LoadTitle = "ロード";
        public const string SlotEmpty = "空きスロット";
        public const string SaveSuccess = "セーブ完了！";
        public const string LoadSuccess = "ロード完了！";
        public const string SaveFailed = "セーブ失敗";
        public const string LoadFailed = "ロード失敗";

        // ============================================================
        // サウンド設定
        // ============================================================
        public const string SoundTitle = "サウンド設定";
        public const string LabelMasterVolume = "マスター音量";
        public const string LabelBGMVolume = "BGM音量";
        public const string LabelSEVolume = "効果音量";
        public const string LabelAmbientVolume = "環境音量";

        // ============================================================
        // ゲーム速度
        // ============================================================
        public const string SpeedPause = "||";
        public const string SpeedHalf = "x0.5";
        public const string SpeedNormal = "x1";
        public const string SpeedDouble = "x2";
        public const string SpeedMax = "x5";

        // ============================================================
        // 月次レポート
        // ============================================================
        public const string MonthlyReportTitle = "月次レポート";
        public const string LabelIncome = "収入";
        public const string LabelExpenses = "支出";
        public const string LabelProfit = "利益";
        public const string LabelVisitorCount = "来場者数";
        public const string LabelAttractionRevenue = "アトラクション収益";
        public const string LabelShopRevenue = "ショップ収益";
        public const string LabelEntranceFee = "入場料収入";
        public const string LabelStaffCost = "人件費";
        public const string LabelMaintenanceCost = "維持費";

        // ============================================================
        // ゲームオーバー / リザルト
        // ============================================================
        public const string ResultTitle = "パーク運営結果";
        public const string ResultFinalScore = "最終スコア";
        public const string BtnRestart = "リスタート";
        public const string BtnBackToTitle = "タイトルに戻る";

        // ============================================================
        // チュートリアル
        // ============================================================
        public const string TutorialWelcome = "テーマパークワールドへようこそ！";
        public const string TutorialBuild = "まずはアトラクションを建設しましょう。画面下の「建設」ボタンをタップしてください。";
        public const string TutorialStaff = "次にスタッフを雇いましょう。「スタッフ」ボタンからメカニックを雇用してください。";
        public const string TutorialPrice = "入場料を設定しましょう。パーク評価に見合った価格が大切です。";
        public const string TutorialVisitor = "来場者がやってきました！タップすると詳細情報を確認できます。";
        public const string TutorialResearch = "研究を進めて新しいアトラクションをアンロックしましょう。";
        public const string TutorialComplete = "チュートリアル完了！自由にパークを発展させましょう！";
        public const string BtnNext = "次へ";
        public const string BtnSkip = "スキップ";

        // ============================================================
        // 実績
        // ============================================================
        public const string AchievementTitle = "実績一覧";
        public const string AchievementUnlocked = "実績解除！";
        public const string AchievementLocked = "未解除";
        public const string AchievementProgress = "達成率";

        // ============================================================
        // 通知
        // ============================================================
        public const string NotifAttractionBroken = "アトラクションが故障しました！";
        public const string NotifStaffStrike = "スタッフがストライキを開始しました！";
        public const string NotifVIPArrival = "VIPが来園しました！";
        public const string NotifResearchDone = "研究が完了しました！";
        public const string NotifMonthEnd = "月末レポートが届きました";
        public const string NotifLowFunds = "資金が不足しています！";
        public const string NotifAccident = "事故が発生しました！";
        public const string NotifNewRecord = "新記録達成！";

        // ============================================================
        // SNS
        // ============================================================
        public const string SNSTitle = "パーク口コミ";
        public const string SNSReputation = "評判スコア";
        public const string SNSTrending = "トレンド";
        public const string SNSPositive = "好意的";
        public const string SNSNegative = "否定的";
        public const string SNSNeutral = "中立";

        // ============================================================
        // パーク評価
        // ============================================================
        public const string RatingTitle = "パーク評価";
        public const string RatingThrill = "スリル";
        public const string RatingBeauty = "美しさ";
        public const string RatingVariety = "多様性";
        public const string RatingValue = "コスパ";
        public const string RatingCleanliness = "清潔度";

        // ============================================================
        // テーマゾーン
        // ============================================================
        public const string ZoneLostKingdom = "ロストキングダム";
        public const string ZoneHalloweenWorld = "ハロウィーンワールド";
        public const string ZoneWonderland = "ワンダーランド";
        public const string ZoneSpaceZone = "スペースゾーン";
        public const string ZoneFutureCity = "フューチャーシティ";

        // ============================================================
        // その他UI
        // ============================================================
        public const string BtnClose = "閉じる";
        public const string BtnConfirm = "確定";
        public const string BtnBack = "戻る";
        public const string LabelLevel = "Lv.";
        public const string LabelTotal = "合計";
        public const string LabelAverage = "平均";
        public const string LabelNone = "なし";
        public const string LabelPeople = "人";
        public const string LabelYen = "¥";
        public const string LabelPercent = "%";

        // ============================================================
        // エラー/警告
        // ============================================================
        public const string ErrInsufficientFunds = "資金が不足しています";
        public const string ErrMaxCapacity = "上限に達しています";
        public const string ErrResearchRequired = "先に研究を完了してください";
        public const string ErrNoSpace = "配置スペースがありません";
        public const string WarnBankrupt = "破産の危機！ローンを検討してください";
        public const string WarnStaffFatigue = "スタッフが疲弊しています！";

        // ============================================================
        // ヘルパー
        // ============================================================

        /// <summary>スタッフタイプ名を日本語で取得</summary>
        public static string GetStaffTypeName(StaffType type)
        {
            return type switch
            {
                StaffType.Mechanic => StaffMechanic,
                StaffType.Cleaner => StaffCleaner,
                StaffType.Entertainer => StaffEntertainer,
                StaffType.Guard => StaffGuard,
                StaffType.Scientist => StaffScientist,
                StaffType.Doctor => StaffDoctor,
                StaffType.Vendor => StaffVendor,
                StaffType.Gardener => StaffGardener,
                _ => type.ToString()
            };
        }

        /// <summary>来場者タイプ名を日本語で取得</summary>
        public static string GetVisitorTypeName(VisitorType type)
        {
            return type switch
            {
                VisitorType.Kids => VisitorKids,
                VisitorType.Young => VisitorYoung,
                VisitorType.Family => VisitorFamily,
                VisitorType.Couple => VisitorCouple,
                VisitorType.Senior => VisitorSenior,
                VisitorType.VIP => VisitorVIP,
                _ => type.ToString()
            };
        }

        /// <summary>天候名を日本語で取得</summary>
        public static string GetWeatherName(Weather weather)
        {
            return weather switch
            {
                Weather.Sunny => WeatherSunny,
                Weather.Cloudy => WeatherCloudy,
                Weather.Rainy => WeatherRainy,
                Weather.Snowy => WeatherSnowy,
                Weather.Hot => WeatherHeatWave,
                _ => weather.ToString()
            };
        }

        /// <summary>テーマゾーン名を日本語で取得</summary>
        public static string GetZoneName(ThemeZone zone)
        {
            return zone switch
            {
                ThemeZone.LostKingdom => ZoneLostKingdom,
                ThemeZone.HalloweenWorld => ZoneHalloweenWorld,
                ThemeZone.Wonderland => ZoneWonderland,
                ThemeZone.SpaceZone => ZoneSpaceZone,
                _ => zone.ToString()
            };
        }

        /// <summary>金額を日本語フォーマットで取得</summary>
        public static string FormatMoney(float amount)
        {
            if (amount >= 10000f)
                return $"¥{amount / 10000f:F1}万";
            return $"¥{amount:F0}";
        }

        /// <summary>数値を日本語フォーマットで取得</summary>
        public static string FormatCount(int count)
        {
            if (count >= 10000)
                return $"{count / 10000f:F1}万{LabelPeople}";
            return $"{count}{LabelPeople}";
        }
    }
}
