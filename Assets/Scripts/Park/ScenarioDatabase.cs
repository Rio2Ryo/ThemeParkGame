// ============================================================
// ThemeParkGame - ScenarioDatabase
// 全10ヶ国のシナリオ静的データベース
// ============================================================

using System.Collections.Generic;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    /// <summary>
    /// 全シナリオの静的データを管理する。
    /// GameManager.StartScenario()から参照される。
    ///
    /// 【ゲームデザイン: 難易度バランス】
    /// Easy (4ヶ国): France, Egypt, India, China
    ///   - 初期資金: 80,000～120,000
    ///   - 目標: 低め（来場者500人、月間利益5,000など）
    ///   - 初期施設あり、制限時間なし
    ///
    /// Normal (2ヶ国): Japan, UnitedKingdom
    ///   - 初期資金: 50,000～60,000
    ///   - 目標: 中程度（来場者1,000人、評価70以上など）
    ///   - 初期施設少なめ、緩い制限時間
    ///
    /// Hard (4ヶ国): UnitedStates, Brazil, Australia, Russia
    ///   - 初期資金: 20,000～40,000
    ///   - 目標: 高め（来場者2,000人、全認定証取得など）
    ///   - 初期施設なし、厳しい制限時間
    /// </summary>
    public static class ScenarioDatabase
    {
        private static Dictionary<ScenarioCountry, ScenarioData> _scenarios;

        /// <summary>
        /// 指定国のシナリオデータを取得する。
        /// 初回アクセス時にデータベースを初期化する。
        /// </summary>
        public static ScenarioData GetScenario(ScenarioCountry country)
        {
            if (_scenarios == null)
            {
                InitializeDatabase();
            }

            if (_scenarios.TryGetValue(country, out ScenarioData data))
            {
                return data;
            }
            return null;
        }

        /// <summary>全シナリオデータを取得する</summary>
        public static IReadOnlyDictionary<ScenarioCountry, ScenarioData> GetAllScenarios()
        {
            if (_scenarios == null)
            {
                InitializeDatabase();
            }
            return _scenarios;
        }

        /// <summary>指定難易度のシナリオ一覧を取得する</summary>
        public static List<ScenarioData> GetScenariosByDifficulty(ScenarioDifficulty difficulty)
        {
            if (_scenarios == null)
            {
                InitializeDatabase();
            }

            var result = new List<ScenarioData>();
            foreach (var kvp in _scenarios)
            {
                if (kvp.Value.Difficulty == difficulty)
                {
                    result.Add(kvp.Value);
                }
            }
            return result;
        }

        private static void InitializeDatabase()
        {
            _scenarios = new Dictionary<ScenarioCountry, ScenarioData>();

            // ======== Easy シナリオ ========
            CreateFranceScenario();
            CreateEgyptScenario();
            CreateIndiaScenario();
            CreateChinaScenario();

            // ======== Normal シナリオ ========
            CreateJapanScenario();
            CreateUKScenario();

            // ======== Hard シナリオ ========
            CreateUSAScenario();
            CreateBrazilScenario();
            CreateAustraliaScenario();
            CreateRussiaScenario();
        }

        // ================================================================
        // Easy シナリオ
        // 初心者向け。資金に余裕があり、目標も緩やか。
        // ================================================================

        /// <summary>
        /// フランス: 最初のシナリオ。チュートリアル的な位置づけ。
        /// ワンダーランドゾーンで夢の遊園地を作る。
        /// </summary>
        private static void CreateFranceScenario()
        {
            var scenario = new ScenarioData
            {
                Country = ScenarioCountry.France,
                Difficulty = ScenarioDifficulty.Easy,
                StartingMoney = 120000,
                InitialZone = ThemeZone.Wonderland,
                NameKey = "scenario_france_name",
                DescriptionKey = "scenario_france_desc",
                TimeLimitYears = 0
            };

            // 初期施設: 小さなコースターと飲食店
            scenario.PrebuiltFacilities.Add(
                new PrebuiltFacilityData(FacilityType.Attraction, "small_coaster", 5, 5, ThemeZone.Wonderland));
            scenario.PrebuiltFacilities.Add(
                new PrebuiltFacilityData(FacilityType.FoodShop, "burger_stand", 8, 5, ThemeZone.Wonderland));
            scenario.PrebuiltFacilities.Add(
                new PrebuiltFacilityData(FacilityType.Toilet, "basic_toilet", 10, 5, ThemeZone.Wonderland));

            // 目標: 来場者500人 + 月間利益5,000
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.VisitorTarget, 500f, "objective_visitor_500"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.MonthlyProfitTarget, 5000f, "objective_profit_5000"));

            scenario.CompletionReward = new ScenarioReward
            {
                GoldenTickets = 1,
                BonusMoney = 50000
            };

            _scenarios[ScenarioCountry.France] = scenario;
        }

        /// <summary>
        /// エジプト: ロストキングダムゾーンで古代遺跡テーマパークを運営。
        /// 暑い気候により飲み物の需要が高い。
        /// </summary>
        private static void CreateEgyptScenario()
        {
            var scenario = new ScenarioData
            {
                Country = ScenarioCountry.Egypt,
                Difficulty = ScenarioDifficulty.Easy,
                StartingMoney = 100000,
                InitialZone = ThemeZone.LostKingdom,
                NameKey = "scenario_egypt_name",
                DescriptionKey = "scenario_egypt_desc",
                TimeLimitYears = 0
            };

            scenario.PrebuiltFacilities.Add(
                new PrebuiltFacilityData(FacilityType.Attraction, "tomb_explorer", 4, 4, ThemeZone.LostKingdom));
            scenario.PrebuiltFacilities.Add(
                new PrebuiltFacilityData(FacilityType.DrinkShop, "oasis_drinks", 7, 4, ThemeZone.LostKingdom));

            // 目標: 来場者400人 + ショップ売上重視
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.VisitorTarget, 400f, "objective_visitor_400"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.TotalRevenueTarget, 80000f, "objective_revenue_80000"));

            scenario.CompletionReward = new ScenarioReward
            {
                GoldenTickets = 1,
                BonusMoney = 40000
            };

            _scenarios[ScenarioCountry.Egypt] = scenario;
        }

        /// <summary>
        /// インド: ワンダーランドゾーンで色彩豊かなテーマパークを構築。
        /// 大家族向けの来場者が多い。
        /// </summary>
        private static void CreateIndiaScenario()
        {
            var scenario = new ScenarioData
            {
                Country = ScenarioCountry.India,
                Difficulty = ScenarioDifficulty.Easy,
                StartingMoney = 90000,
                InitialZone = ThemeZone.Wonderland,
                NameKey = "scenario_india_name",
                DescriptionKey = "scenario_india_desc",
                TimeLimitYears = 0
            };

            scenario.PrebuiltFacilities.Add(
                new PrebuiltFacilityData(FacilityType.Attraction, "magic_carpet", 6, 6, ThemeZone.Wonderland));
            scenario.PrebuiltFacilities.Add(
                new PrebuiltFacilityData(FacilityType.FoodShop, "spice_kitchen", 9, 6, ThemeZone.Wonderland));

            // 目標: 幸福度重視 + 来場者数
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.VisitorTarget, 600f, "objective_visitor_600"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.HappinessTarget, 70f, "objective_happiness_70"));

            scenario.CompletionReward = new ScenarioReward
            {
                GoldenTickets = 1,
                BonusMoney = 45000
            };

            _scenarios[ScenarioCountry.India] = scenario;
        }

        /// <summary>
        /// 中国: スペースゾーンで未来型テーマパークを建設。
        /// アトラクション数を増やすことが重要。
        /// </summary>
        private static void CreateChinaScenario()
        {
            var scenario = new ScenarioData
            {
                Country = ScenarioCountry.China,
                Difficulty = ScenarioDifficulty.Easy,
                StartingMoney = 80000,
                InitialZone = ThemeZone.SpaceZone,
                NameKey = "scenario_china_name",
                DescriptionKey = "scenario_china_desc",
                TimeLimitYears = 0
            };

            scenario.PrebuiltFacilities.Add(
                new PrebuiltFacilityData(FacilityType.Attraction, "rocket_launch", 5, 5, ThemeZone.SpaceZone));

            // 目標: アトラクション5個 + 来場者300人
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.AttractionCountTarget, 5f, "objective_attraction_5"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.VisitorTarget, 300f, "objective_visitor_300"));

            scenario.CompletionReward = new ScenarioReward
            {
                GoldenTickets = 1,
                BonusMoney = 35000
            };

            _scenarios[ScenarioCountry.China] = scenario;
        }

        // ================================================================
        // Normal シナリオ
        // 中級者向け。バランスの取れた経営が求められる。
        // ================================================================

        /// <summary>
        /// 日本: 全カテゴリ評価を満遍なく上げる総合経営シナリオ。
        /// 快適性と安全性に特に厳しい来場者が多い。
        /// </summary>
        private static void CreateJapanScenario()
        {
            var scenario = new ScenarioData
            {
                Country = ScenarioCountry.Japan,
                Difficulty = ScenarioDifficulty.Normal,
                StartingMoney = 60000,
                InitialZone = ThemeZone.Wonderland,
                NameKey = "scenario_japan_name",
                DescriptionKey = "scenario_japan_desc",
                TimeLimitYears = 8
            };

            scenario.PrebuiltFacilities.Add(
                new PrebuiltFacilityData(FacilityType.Attraction, "tea_cups", 5, 5, ThemeZone.Wonderland));

            // 目標: パーク評価70以上 + 来場者1,000人 + 認定証1つ
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ParkRatingTarget, 70f, "objective_rating_70"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.VisitorTarget, 1000f, "objective_visitor_1000"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ObtainCertificate, 1f, "objective_cert_comfort",
                (int)CertificateCategory.Comfort));

            scenario.CompletionReward = new ScenarioReward
            {
                GoldenTickets = 2,
                BonusMoney = 80000
            };

            _scenarios[ScenarioCountry.Japan] = scenario;
        }

        /// <summary>
        /// イギリス: 雨が多い環境で利益を上げる経営シナリオ。
        /// 天候対策と屋内施設の充実が鍵。
        /// </summary>
        private static void CreateUKScenario()
        {
            var scenario = new ScenarioData
            {
                Country = ScenarioCountry.UnitedKingdom,
                Difficulty = ScenarioDifficulty.Normal,
                StartingMoney = 50000,
                InitialZone = ThemeZone.LostKingdom,
                NameKey = "scenario_uk_name",
                DescriptionKey = "scenario_uk_desc",
                TimeLimitYears = 8
            };

            // 初期施設なし（やや厳しめのNormal）
            // 目標: 月間利益15,000 + パーク評価60以上
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.MonthlyProfitTarget, 15000f, "objective_profit_15000"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ParkRatingTarget, 60f, "objective_rating_60"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.VisitorTarget, 800f, "objective_visitor_800"));

            scenario.CompletionReward = new ScenarioReward
            {
                GoldenTickets = 2,
                BonusMoney = 70000
            };

            _scenarios[ScenarioCountry.UnitedKingdom] = scenario;
        }

        // ================================================================
        // Hard シナリオ
        // 上級者向け。初期資金が少なく、厳しい目標と制限時間がある。
        // ================================================================

        /// <summary>
        /// アメリカ: 大規模テーマパーク経営。高い来場者数と収益目標。
        /// 競合が多く、価格設定が非常に重要。
        /// </summary>
        private static void CreateUSAScenario()
        {
            var scenario = new ScenarioData
            {
                Country = ScenarioCountry.UnitedStates,
                Difficulty = ScenarioDifficulty.Hard,
                StartingMoney = 40000,
                InitialZone = ThemeZone.SpaceZone,
                NameKey = "scenario_usa_name",
                DescriptionKey = "scenario_usa_desc",
                TimeLimitYears = 6
            };

            // 目標: 来場者2,000人 + 総収入500,000 + ゾーンアンロック
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.VisitorTarget, 2000f, "objective_visitor_2000"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.TotalRevenueTarget, 500000f, "objective_revenue_500000"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.UnlockZone, 1f, "objective_unlock_halloween",
                (int)ThemeZone.HalloweenWorld));

            scenario.CompletionReward = new ScenarioReward
            {
                GoldenTickets = 3,
                BonusMoney = 100000
            };

            _scenarios[ScenarioCountry.UnitedStates] = scenario;
        }

        /// <summary>
        /// ブラジル: エンターテイメント重視のシナリオ。
        /// ムード評価と興奮度の両立が求められる。
        /// </summary>
        private static void CreateBrazilScenario()
        {
            var scenario = new ScenarioData
            {
                Country = ScenarioCountry.Brazil,
                Difficulty = ScenarioDifficulty.Hard,
                StartingMoney = 35000,
                InitialZone = ThemeZone.Wonderland,
                NameKey = "scenario_brazil_name",
                DescriptionKey = "scenario_brazil_desc",
                TimeLimitYears = 6
            };

            // 目標: ムード認定証 + 興奮度認定証 + 来場者1,500人
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ObtainCertificate, 1f, "objective_cert_mood",
                (int)CertificateCategory.Mood));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ObtainCertificate, 1f, "objective_cert_excitement",
                (int)CertificateCategory.Excitement));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.VisitorTarget, 1500f, "objective_visitor_1500"));

            scenario.CompletionReward = new ScenarioReward
            {
                GoldenTickets = 3,
                BonusMoney = 90000
            };

            _scenarios[ScenarioCountry.Brazil] = scenario;
        }

        /// <summary>
        /// オーストラリア: 安全性最優先のシナリオ。
        /// 事故ゼロを維持しつつ利益を確保する。
        /// </summary>
        private static void CreateAustraliaScenario()
        {
            var scenario = new ScenarioData
            {
                Country = ScenarioCountry.Australia,
                Difficulty = ScenarioDifficulty.Hard,
                StartingMoney = 30000,
                InitialZone = ThemeZone.LostKingdom,
                NameKey = "scenario_australia_name",
                DescriptionKey = "scenario_australia_desc",
                TimeLimitYears = 5
            };

            // 目標: 安全認定証 + パーク評価80以上 + 月間利益20,000
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ObtainCertificate, 1f, "objective_cert_safety",
                (int)CertificateCategory.Safety));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ParkRatingTarget, 80f, "objective_rating_80"));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.MonthlyProfitTarget, 20000f, "objective_profit_20000"));

            scenario.CompletionReward = new ScenarioReward
            {
                GoldenTickets = 3,
                BonusMoney = 100000
            };

            _scenarios[ScenarioCountry.Australia] = scenario;
        }

        /// <summary>
        /// ロシア: 最難関シナリオ。厳しい気候と低予算で全認定証を目指す。
        /// 冬は来場者が激減し、経営判断が試される。
        /// </summary>
        private static void CreateRussiaScenario()
        {
            var scenario = new ScenarioData
            {
                Country = ScenarioCountry.Russia,
                Difficulty = ScenarioDifficulty.Hard,
                StartingMoney = 20000,
                InitialZone = ThemeZone.HalloweenWorld,
                NameKey = "scenario_russia_name",
                DescriptionKey = "scenario_russia_desc",
                TimeLimitYears = 5
            };

            // 目標: 全5カテゴリ認定証取得 + ゴールデンチケット3枚
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ObtainCertificate, 1f, "objective_cert_fame",
                (int)CertificateCategory.Fame));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ObtainCertificate, 1f, "objective_cert_safety",
                (int)CertificateCategory.Safety));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ObtainCertificate, 1f, "objective_cert_comfort",
                (int)CertificateCategory.Comfort));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ObtainCertificate, 1f, "objective_cert_excitement",
                (int)CertificateCategory.Excitement));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.ObtainCertificate, 1f, "objective_cert_mood",
                (int)CertificateCategory.Mood));
            scenario.Objectives.Add(new ScenarioObjective(
                ObjectiveType.GoldenTicketTarget, 3f, "objective_golden_3"));

            scenario.CompletionReward = new ScenarioReward
            {
                GoldenTickets = 5,
                BonusMoney = 200000
            };
            scenario.CompletionReward.UnlockedAttractions.Add("legendary_coaster");

            _scenarios[ScenarioCountry.Russia] = scenario;
        }
    }
}
