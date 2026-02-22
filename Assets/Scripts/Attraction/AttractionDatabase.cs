// ============================================================
// ThemeParkGame - AttractionDatabase
// 全アトラクションの静的データベース
// テーマゾーン別・カテゴリ別にアトラクション定義を管理する
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Attraction
{
    /// <summary>
    /// コード上で定義するアトラクションの基本パラメータ。
    /// ScriptableObject(AttractionData)を動的に生成する際の初期値として使用する。
    /// エディタ上でAttractionDataアセットを手作業で作成する場合は参考値となる。
    /// </summary>
    [Serializable]
    public class AttractionDefinition
    {
        public string AttractionId;
        public string NameJP;
        public string NameEN;
        public string Description;
        public AttractionCategory Category;
        public ThemeZone PrimaryThemeZone;
        public float ExcitementRating;
        public float NauseaFactor;
        public int Capacity;
        public float RideDuration;
        public int BuildCost;
        public int MaintenanceCost;
        public int SuggestedTicketPrice;
        public Vector2Int Size;
        public string RequiredResearchId;
        public float BaseBreakdownRate;
        public List<AttractionUpgradeLevel> UpgradePath;
    }

    /// <summary>
    /// 全アトラクションの静的データベース。
    /// テーマゾーン別・カテゴリ別にアトラクション定義を管理し、
    /// ゲーム内のアトラクション建設メニューや研究ツリーで参照される。
    ///
    /// 各テーマゾーンに3種類以上のアトラクションを定義:
    ///
    /// ロストキングダム:
    ///   - ストーンコースター（G系） - 古代遺跡を駆け抜けるコースター
    ///   - エンシェントホイール（縦回転） - 古代文明の巨大車輪
    ///   - ドラゴンボート（乗り物系） - 地底湖を巡る神秘のボートライド
    ///
    /// ハロウィーンワールド:
    ///   - ゴーストトレイン（見せ物系） - 恐怖の幽霊列車
    ///   - ウィッチスピン（横回転） - 魔女の大釜回転
    ///   - スクリームタワー（G系） - 絶叫のフリーフォール
    ///
    /// ワンダーランド:
    ///   - フェアリーカルーセル（横回転） - 妖精のメリーゴーラウンド
    ///   - マジックカーペット（乗り物系） - 空飛ぶ魔法の絨毯
    ///   - ジャイアントビーンストーク（展望系） - 巨大な豆の木展望台
    ///
    /// スペースゾーン:
    ///   - ロケットコースター（G系） - 宇宙空間の超高速コースター
    ///   - UFOスピナー（横回転） - UFO型高速回転
    ///   - スターオブザーバトリー（展望系） - 宇宙展望施設
    /// </summary>
    public static class AttractionDatabase
    {
        /// <summary>全アトラクション定義（AttractionId → AttractionDefinition）</summary>
        private static Dictionary<string, AttractionDefinition> _definitions;

        /// <summary>テーマゾーン別インデックス</summary>
        private static Dictionary<ThemeZone, List<AttractionDefinition>> _byZone;

        /// <summary>カテゴリ別インデックス</summary>
        private static Dictionary<AttractionCategory, List<AttractionDefinition>> _byCategory;

        /// <summary>初期化済みフラグ</summary>
        private static bool _initialized;

        // ---- 初期化 ----

        /// <summary>
        /// データベースを初期化する。
        /// 初回アクセス時に自動的に呼ばれる（遅延初期化）。
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _definitions = new Dictionary<string, AttractionDefinition>();
            _byZone = new Dictionary<ThemeZone, List<AttractionDefinition>>();
            _byCategory = new Dictionary<AttractionCategory, List<AttractionDefinition>>();

            // 各テーマゾーンのリストを初期化
            foreach (ThemeZone zone in Enum.GetValues(typeof(ThemeZone)))
            {
                _byZone[zone] = new List<AttractionDefinition>();
            }
            foreach (AttractionCategory cat in Enum.GetValues(typeof(AttractionCategory)))
            {
                _byCategory[cat] = new List<AttractionDefinition>();
            }

            RegisterAllAttractions();
        }

        /// <summary>全アトラクションを登録する</summary>
        private static void RegisterAllAttractions()
        {
            // ===================================================================
            // ロストキングダム
            // 古代遺跡・失われた文明をテーマにしたゾーン
            // ===================================================================

            // ストーンコースター（G系）
            // 古代遺跡の石造りの軌道を駆け抜けるジェットコースター。
            // 興奮度が高く、嘔吐率もそこそこ。パークの目玉アトラクション候補。
            Register(new AttractionDefinition
            {
                AttractionId = "LK_STONE_COASTER",
                NameJP = "ストーンコースター",
                NameEN = "Stone Coaster",
                Description = "古代遺跡の石造り軌道を駆け抜けるジェットコースター。崩壊しかけた神殿の間を縫うスリル満点のコース。",
                Category = AttractionCategory.GForce,
                PrimaryThemeZone = ThemeZone.LostKingdom,
                ExcitementRating = 7.5f,
                NauseaFactor = 0.25f,
                Capacity = 16,
                RideDuration = 90f,
                BuildCost = 8000,
                MaintenanceCost = 350,
                SuggestedTicketPrice = 400,
                Size = new Vector2Int(5, 4),
                RequiredResearchId = "RES_LK_STONE_COASTER",
                BaseBreakdownRate = 0.03f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "軌道延長",
                        Description = "コースを延長し、ループを追加。興奮度が向上する。",
                        UpgradeCost = 3000,
                        ExcitementMultiplier = 1.2f,
                        CapacityMultiplier = 1.0f,
                        NauseaMultiplier = 1.1f,
                        BreakdownRateMultiplier = 0.9f,
                        RequiredResearchId = "RES_UPG_LK_COASTER_LV1"
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "黄金の軌道",
                        Description = "軌道を黄金装飾にアップグレード。見た目と興奮度が大幅向上。",
                        UpgradeCost = 5000,
                        ExcitementMultiplier = 1.15f,
                        CapacityMultiplier = 1.1f,
                        NauseaMultiplier = 0.95f,
                        BreakdownRateMultiplier = 0.85f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 3,
                        UpgradeName = "伝説の遺跡コース",
                        Description = "伝説の遺跡を巡る究極コースに改装。パーク随一の名物に。",
                        UpgradeCost = 8000,
                        ExcitementMultiplier = 1.25f,
                        CapacityMultiplier = 1.2f,
                        NauseaMultiplier = 0.9f,
                        BreakdownRateMultiplier = 0.8f
                    }
                }
            });

            // エンシェントホイール（縦回転）
            // 古代文明の巨大な石の車輪を再現した観覧車。
            // 穏やかだが眺望が良く、幅広い年齢層に人気。
            Register(new AttractionDefinition
            {
                AttractionId = "LK_ANCIENT_WHEEL",
                NameJP = "エンシェントホイール",
                NameEN = "Ancient Wheel",
                Description = "古代文明の巨大車輪を再現した観覧車。頂上からはロストキングダム全体を見渡せる。",
                Category = AttractionCategory.VerticalRotation,
                PrimaryThemeZone = ThemeZone.LostKingdom,
                ExcitementRating = 4.0f,
                NauseaFactor = 0.05f,
                Capacity = 24,
                RideDuration = 120f,
                BuildCost = 5000,
                MaintenanceCost = 200,
                SuggestedTicketPrice = 250,
                Size = new Vector2Int(4, 4),
                RequiredResearchId = "RES_LK_ANCIENT_WHEEL",
                BaseBreakdownRate = 0.015f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "ゴンドラ増設",
                        Description = "ゴンドラを増やして定員アップ。待ち時間を短縮できる。",
                        UpgradeCost = 2000,
                        ExcitementMultiplier = 1.05f,
                        CapacityMultiplier = 1.3f,
                        NauseaMultiplier = 1.0f,
                        BreakdownRateMultiplier = 0.95f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "夜間ライトアップ",
                        Description = "神秘的なライトアップで夜の興奮度がアップ。",
                        UpgradeCost = 3500,
                        ExcitementMultiplier = 1.15f,
                        CapacityMultiplier = 1.0f,
                        NauseaMultiplier = 1.0f,
                        BreakdownRateMultiplier = 0.9f
                    }
                }
            });

            // ドラゴンボート（乗り物系）
            // 地底湖を巡る神秘的なボートライド。
            // ファミリー向けだが、最後にスプラッシュがある。
            Register(new AttractionDefinition
            {
                AttractionId = "LK_DRAGON_BOAT",
                NameJP = "ドラゴンボート",
                NameEN = "Dragon Boat",
                Description = "龍の形をしたボートで地底湖を巡る。最後の大滝ダイブがハイライト。",
                Category = AttractionCategory.RideAttraction,
                PrimaryThemeZone = ThemeZone.LostKingdom,
                ExcitementRating = 6.0f,
                NauseaFactor = 0.1f,
                Capacity = 8,
                RideDuration = 100f,
                BuildCost = 7000,
                MaintenanceCost = 300,
                SuggestedTicketPrice = 350,
                Size = new Vector2Int(4, 5),
                RequiredResearchId = "RES_LK_DRAGON_BOAT",
                BaseBreakdownRate = 0.02f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "大滝コース延長",
                        Description = "コースを延長し、大滝ダイブをよりダイナミックに。",
                        UpgradeCost = 2500,
                        ExcitementMultiplier = 1.2f,
                        CapacityMultiplier = 1.0f,
                        NauseaMultiplier = 1.05f,
                        BreakdownRateMultiplier = 0.95f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "龍の息吹演出",
                        Description = "火炎と水しぶきの演出を追加。迫力満点の体験に。",
                        UpgradeCost = 4000,
                        ExcitementMultiplier = 1.15f,
                        CapacityMultiplier = 1.15f,
                        NauseaMultiplier = 1.0f,
                        BreakdownRateMultiplier = 0.9f
                    }
                }
            });

            // ===================================================================
            // ハロウィーンワールド
            // ホラー・ダークファンタジーをテーマにしたゾーン
            // ===================================================================

            // ゴーストトレイン（見せ物系）
            // 多数のギミックを搭載した恐怖の幽霊列車。
            // 興奮度は中程度だが嘔吐率が低く、回転率が高い。
            Register(new AttractionDefinition
            {
                AttractionId = "HW_GHOST_TRAIN",
                NameJP = "ゴーストトレイン",
                NameEN = "Ghost Train",
                Description = "暗闘の中を進む恐怖の幽霊列車。至る所にホラーギミックが仕掛けられている。",
                Category = AttractionCategory.ShowAttraction,
                PrimaryThemeZone = ThemeZone.HalloweenWorld,
                ExcitementRating = 5.5f,
                NauseaFactor = 0.05f,
                Capacity = 12,
                RideDuration = 80f,
                BuildCost = 6000,
                MaintenanceCost = 250,
                SuggestedTicketPrice = 300,
                Size = new Vector2Int(4, 3),
                RequiredResearchId = "RES_HW_GHOST_TRAIN",
                BaseBreakdownRate = 0.02f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "ホラーギミック追加",
                        Description = "新しい恐怖演出を追加。リピーターも驚かせる。",
                        UpgradeCost = 2000,
                        ExcitementMultiplier = 1.2f,
                        CapacityMultiplier = 1.0f,
                        NauseaMultiplier = 1.0f,
                        BreakdownRateMultiplier = 1.05f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "3Dプロジェクション",
                        Description = "最新3D映像技術で恐怖演出を大幅強化。",
                        UpgradeCost = 4000,
                        ExcitementMultiplier = 1.25f,
                        CapacityMultiplier = 1.1f,
                        NauseaMultiplier = 1.0f,
                        BreakdownRateMultiplier = 0.9f
                    }
                }
            });

            // ウィッチスピン（横回転）
            // 魔女の大釜をモチーフにした高速回転アトラクション。
            // 嘔吐率が高めだが、スリルを求める若者に人気。
            Register(new AttractionDefinition
            {
                AttractionId = "HW_WITCH_SPIN",
                NameJP = "ウィッチスピン",
                NameEN = "Witch's Spin",
                Description = "巨大な魔女の大釜の上で高速回転。ハロウィーンの魔法に酔いしれる。",
                Category = AttractionCategory.HorizontalRotation,
                PrimaryThemeZone = ThemeZone.HalloweenWorld,
                ExcitementRating = 6.5f,
                NauseaFactor = 0.3f,
                Capacity = 20,
                RideDuration = 60f,
                BuildCost = 5500,
                MaintenanceCost = 220,
                SuggestedTicketPrice = 300,
                Size = new Vector2Int(3, 3),
                RequiredResearchId = "RES_HW_WITCH_SPIN",
                BaseBreakdownRate = 0.025f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "高速モード",
                        Description = "回転速度を上げて興奮度アップ。ただし嘔吐率も上昇。",
                        UpgradeCost = 2000,
                        ExcitementMultiplier = 1.2f,
                        CapacityMultiplier = 1.0f,
                        NauseaMultiplier = 1.2f,
                        BreakdownRateMultiplier = 1.1f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "魔法陣エフェクト",
                        Description = "光る魔法陣の演出を追加。見た目と興奮度が向上。",
                        UpgradeCost = 3500,
                        ExcitementMultiplier = 1.15f,
                        CapacityMultiplier = 1.15f,
                        NauseaMultiplier = 0.95f,
                        BreakdownRateMultiplier = 0.9f
                    }
                }
            });

            // スクリームタワー（G系）
            // フリーフォール型の絶叫タワー。
            // 最高クラスの興奮度と嘔吐率を誇るハイリスク・ハイリターンなアトラクション。
            Register(new AttractionDefinition
            {
                AttractionId = "HW_SCREAM_TOWER",
                NameJP = "スクリームタワー",
                NameEN = "Scream Tower",
                Description = "地上50mから一気に落下するフリーフォール。絶叫が止まらない恐怖の塔。",
                Category = AttractionCategory.GForce,
                PrimaryThemeZone = ThemeZone.HalloweenWorld,
                ExcitementRating = 8.5f,
                NauseaFactor = 0.35f,
                Capacity = 12,
                RideDuration = 45f,
                BuildCost = 10000,
                MaintenanceCost = 400,
                SuggestedTicketPrice = 500,
                Size = new Vector2Int(3, 3),
                RequiredResearchId = "RES_HW_SCREAM_TOWER",
                BaseBreakdownRate = 0.035f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "タワー増築",
                        Description = "タワーの高さを20m追加。落下距離と興奮度が大幅アップ。",
                        UpgradeCost = 4000,
                        ExcitementMultiplier = 1.2f,
                        CapacityMultiplier = 1.0f,
                        NauseaMultiplier = 1.15f,
                        BreakdownRateMultiplier = 0.9f,
                        RequiredResearchId = "RES_UPG_HW_SCREAM_LV1"
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "逆バンジー機構",
                        Description = "落下後に跳ね上がる逆バンジー機構を追加。",
                        UpgradeCost = 6000,
                        ExcitementMultiplier = 1.2f,
                        CapacityMultiplier = 1.1f,
                        NauseaMultiplier = 1.1f,
                        BreakdownRateMultiplier = 0.85f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 3,
                        UpgradeName = "ダブルタワー",
                        Description = "2基のタワーを連動させた究極のフリーフォール体験。",
                        UpgradeCost = 10000,
                        ExcitementMultiplier = 1.3f,
                        CapacityMultiplier = 1.5f,
                        NauseaMultiplier = 1.05f,
                        BreakdownRateMultiplier = 0.8f
                    }
                }
            });

            // ===================================================================
            // ワンダーランド
            // ファンタジー・童話をテーマにしたゾーン
            // ===================================================================

            // フェアリーカルーセル（横回転）
            // 妖精たちが飾る幻想的なメリーゴーラウンド。
            // 低興奮度・低嘔吐率でファミリー・キッズ向け。定員が多く安定収益源。
            Register(new AttractionDefinition
            {
                AttractionId = "WL_FAIRY_CAROUSEL",
                NameJP = "フェアリーカルーセル",
                NameEN = "Fairy Carousel",
                Description = "妖精たちが飛び交う幻想的なメリーゴーラウンド。子供たちの一番人気。",
                Category = AttractionCategory.HorizontalRotation,
                PrimaryThemeZone = ThemeZone.Wonderland,
                ExcitementRating = 3.5f,
                NauseaFactor = 0.03f,
                Capacity = 28,
                RideDuration = 75f,
                BuildCost = 3500,
                MaintenanceCost = 150,
                SuggestedTicketPrice = 200,
                Size = new Vector2Int(3, 3),
                RequiredResearchId = "RES_WL_FAIRY_CAROUSEL",
                BaseBreakdownRate = 0.01f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "ユニコーン追加",
                        Description = "ユニコーンの乗り物を追加し、定員と人気度をアップ。",
                        UpgradeCost = 1500,
                        ExcitementMultiplier = 1.1f,
                        CapacityMultiplier = 1.2f,
                        NauseaMultiplier = 1.0f,
                        BreakdownRateMultiplier = 0.95f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "オルゴール演出",
                        Description = "美しいオルゴールの音楽演出で雰囲気を大幅向上。",
                        UpgradeCost = 2500,
                        ExcitementMultiplier = 1.15f,
                        CapacityMultiplier = 1.1f,
                        NauseaMultiplier = 1.0f,
                        BreakdownRateMultiplier = 0.9f
                    }
                }
            });

            // マジックカーペット（乗り物系）
            // 空飛ぶ魔法の絨毯に乗ってワンダーランドを巡るライド。
            // 中程度の興奮度で幅広い層に受ける。
            Register(new AttractionDefinition
            {
                AttractionId = "WL_MAGIC_CARPET",
                NameJP = "マジックカーペット",
                NameEN = "Magic Carpet",
                Description = "魔法の絨毯に乗ってワンダーランドの空を飛ぶ。眼下に広がるファンタジーの世界。",
                Category = AttractionCategory.RideAttraction,
                PrimaryThemeZone = ThemeZone.Wonderland,
                ExcitementRating = 5.5f,
                NauseaFactor = 0.08f,
                Capacity = 10,
                RideDuration = 85f,
                BuildCost = 6000,
                MaintenanceCost = 250,
                SuggestedTicketPrice = 300,
                Size = new Vector2Int(3, 4),
                RequiredResearchId = "RES_WL_MAGIC_CARPET",
                BaseBreakdownRate = 0.018f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "風のエフェクト",
                        Description = "風の演出を追加し、空を飛ぶ臨場感をアップ。",
                        UpgradeCost = 2000,
                        ExcitementMultiplier = 1.15f,
                        CapacityMultiplier = 1.0f,
                        NauseaMultiplier = 1.05f,
                        BreakdownRateMultiplier = 0.95f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "夜間飛行コース",
                        Description = "星空の中を飛ぶ夜間専用コースを追加。",
                        UpgradeCost = 3500,
                        ExcitementMultiplier = 1.2f,
                        CapacityMultiplier = 1.15f,
                        NauseaMultiplier = 1.0f,
                        BreakdownRateMultiplier = 0.9f
                    }
                }
            });

            // ジャイアントビーンストーク（展望系）
            // ジャックと豆の木をモチーフにした展望タワー。
            // 穏やかで嘔吐率が極めて低い。全年齢向け。
            Register(new AttractionDefinition
            {
                AttractionId = "WL_GIANT_BEANSTALK",
                NameJP = "ジャイアントビーンストーク",
                NameEN = "Giant Beanstalk",
                Description = "巨大な豆の木を登って雲の上の世界へ。パーク全体を一望できる展望台。",
                Category = AttractionCategory.Observation,
                PrimaryThemeZone = ThemeZone.Wonderland,
                ExcitementRating = 4.5f,
                NauseaFactor = 0.02f,
                Capacity = 20,
                RideDuration = 100f,
                BuildCost = 5500,
                MaintenanceCost = 200,
                SuggestedTicketPrice = 250,
                Size = new Vector2Int(2, 2),
                RequiredResearchId = "RES_WL_GIANT_BEANSTALK",
                BaseBreakdownRate = 0.012f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "雲の上の庭園",
                        Description = "頂上に雲の上の庭園を追加。滞在時間と満足度アップ。",
                        UpgradeCost = 2000,
                        ExcitementMultiplier = 1.15f,
                        CapacityMultiplier = 1.2f,
                        NauseaMultiplier = 1.0f,
                        BreakdownRateMultiplier = 0.9f
                    }
                }
            });

            // ===================================================================
            // スペースゾーン
            // 宇宙・SF・未来テクノロジーをテーマにしたゾーン
            // ===================================================================

            // ロケットコースター（G系）
            // 宇宙空間を駆け抜ける超高速ジェットコースター。
            // ゲーム中最高クラスの興奮度。上級プレイヤー向け。
            Register(new AttractionDefinition
            {
                AttractionId = "SZ_ROCKET_COASTER",
                NameJP = "ロケットコースター",
                NameEN = "Rocket Coaster",
                Description = "宇宙空間をロケットで駆け抜ける超高速コースター。重力を超越した極限のGを体感。",
                Category = AttractionCategory.GForce,
                PrimaryThemeZone = ThemeZone.SpaceZone,
                ExcitementRating = 9.0f,
                NauseaFactor = 0.4f,
                Capacity = 12,
                RideDuration = 75f,
                BuildCost = 12000,
                MaintenanceCost = 500,
                SuggestedTicketPrice = 600,
                Size = new Vector2Int(6, 5),
                RequiredResearchId = "RES_SZ_ROCKET_COASTER",
                BaseBreakdownRate = 0.04f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "ブースター追加",
                        Description = "ロケットブースターで加速区間を追加。最高速度が大幅アップ。",
                        UpgradeCost = 5000,
                        ExcitementMultiplier = 1.15f,
                        CapacityMultiplier = 1.0f,
                        NauseaMultiplier = 1.15f,
                        BreakdownRateMultiplier = 0.9f,
                        RequiredResearchId = "RES_UPG_SZ_ROCKET_LV1"
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "ワープゾーン",
                        Description = "光のトンネルを通過するワープ演出を追加。",
                        UpgradeCost = 7000,
                        ExcitementMultiplier = 1.2f,
                        CapacityMultiplier = 1.1f,
                        NauseaMultiplier = 1.05f,
                        BreakdownRateMultiplier = 0.85f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 3,
                        UpgradeName = "銀河コース",
                        Description = "銀河を巡る究極コースに拡張。ゲーム内最高の興奮度を実現。",
                        UpgradeCost = 12000,
                        ExcitementMultiplier = 1.25f,
                        CapacityMultiplier = 1.2f,
                        NauseaMultiplier = 1.1f,
                        BreakdownRateMultiplier = 0.8f
                    }
                }
            });

            // UFOスピナー（横回転）
            // UFO型の高速回転アトラクション。
            // 高い嘔吐率で若者に人気。スペースゾーンらしい近未来的デザイン。
            Register(new AttractionDefinition
            {
                AttractionId = "SZ_UFO_SPINNER",
                NameJP = "UFOスピナー",
                NameEN = "UFO Spinner",
                Description = "UFO型ライドで超高速回転。遠心力で身体が浮き上がる宇宙酔い体験。",
                Category = AttractionCategory.HorizontalRotation,
                PrimaryThemeZone = ThemeZone.SpaceZone,
                ExcitementRating = 7.0f,
                NauseaFactor = 0.35f,
                Capacity = 18,
                RideDuration = 55f,
                BuildCost = 7000,
                MaintenanceCost = 280,
                SuggestedTicketPrice = 350,
                Size = new Vector2Int(3, 3),
                RequiredResearchId = "RES_SZ_UFO_SPINNER",
                BaseBreakdownRate = 0.03f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "反重力モード",
                        Description = "回転中に座席が傾く反重力モードを追加。",
                        UpgradeCost = 2500,
                        ExcitementMultiplier = 1.2f,
                        CapacityMultiplier = 1.0f,
                        NauseaMultiplier = 1.2f,
                        BreakdownRateMultiplier = 1.05f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "LED光線演出",
                        Description = "回転と同期したLED演出で宇宙空間を再現。",
                        UpgradeCost = 4000,
                        ExcitementMultiplier = 1.15f,
                        CapacityMultiplier = 1.15f,
                        NauseaMultiplier = 0.95f,
                        BreakdownRateMultiplier = 0.9f
                    }
                }
            });

            // スターオブザーバトリー（展望系）
            // 宇宙の星々を望む巨大展望施設。
            // 高い定員と低い嘔吐率で安定した収益を生む。
            Register(new AttractionDefinition
            {
                AttractionId = "SZ_STAR_OBSERVATORY",
                NameJP = "スターオブザーバトリー",
                NameEN = "Star Observatory",
                Description = "宇宙ステーション型の展望施設。プラネタリウムと望遠鏡で星空を楽しむ。",
                Category = AttractionCategory.Observation,
                PrimaryThemeZone = ThemeZone.SpaceZone,
                ExcitementRating = 5.0f,
                NauseaFactor = 0.01f,
                Capacity = 30,
                RideDuration = 110f,
                BuildCost = 8000,
                MaintenanceCost = 300,
                SuggestedTicketPrice = 350,
                Size = new Vector2Int(4, 4),
                RequiredResearchId = "RES_SZ_STAR_OBSERVATORY",
                BaseBreakdownRate = 0.015f,
                UpgradePath = new List<AttractionUpgradeLevel>
                {
                    new AttractionUpgradeLevel
                    {
                        Level = 1,
                        UpgradeName = "4Dシアター",
                        Description = "宇宙旅行を体験できる4Dシアターを併設。",
                        UpgradeCost = 3000,
                        ExcitementMultiplier = 1.25f,
                        CapacityMultiplier = 1.15f,
                        NauseaMultiplier = 1.05f,
                        BreakdownRateMultiplier = 0.9f
                    },
                    new AttractionUpgradeLevel
                    {
                        Level = 2,
                        UpgradeName = "回転展望台",
                        Description = "360度回転する展望台に改装。パーク全体と星空を一望。",
                        UpgradeCost = 5000,
                        ExcitementMultiplier = 1.2f,
                        CapacityMultiplier = 1.2f,
                        NauseaMultiplier = 1.0f,
                        BreakdownRateMultiplier = 0.85f
                    }
                }
            });
        }

        // ---- 登録ヘルパー ----

        /// <summary>アトラクション定義を登録し、各インデックスにも追加する</summary>
        private static void Register(AttractionDefinition def)
        {
            if (_definitions.ContainsKey(def.AttractionId))
            {
                Debug.LogWarning($"[AttractionDatabase] 重複ID: {def.AttractionId}");
                return;
            }

            _definitions[def.AttractionId] = def;
            _byZone[def.PrimaryThemeZone].Add(def);
            _byCategory[def.Category].Add(def);
        }

        // ---- クエリメソッド ----

        /// <summary>IDでアトラクション定義を取得する</summary>
        /// <param name="attractionId">アトラクションID</param>
        /// <returns>アトラクション定義。見つからない場合はnull</returns>
        public static AttractionDefinition GetById(string attractionId)
        {
            EnsureInitialized();
            _definitions.TryGetValue(attractionId, out var def);
            return def;
        }

        /// <summary>指定テーマゾーンの全アトラクション定義を取得する</summary>
        /// <param name="zone">テーマゾーン</param>
        /// <returns>該当ゾーンのアトラクション定義リスト</returns>
        public static IReadOnlyList<AttractionDefinition> GetByZone(ThemeZone zone)
        {
            EnsureInitialized();
            return _byZone.TryGetValue(zone, out var list) ? list : new List<AttractionDefinition>();
        }

        /// <summary>指定カテゴリの全アトラクション定義を取得する</summary>
        /// <param name="category">アトラクションカテゴリ</param>
        /// <returns>該当カテゴリのアトラクション定義リスト</returns>
        public static IReadOnlyList<AttractionDefinition> GetByCategory(AttractionCategory category)
        {
            EnsureInitialized();
            return _byCategory.TryGetValue(category, out var list) ? list : new List<AttractionDefinition>();
        }

        /// <summary>全アトラクション定義を取得する</summary>
        public static IReadOnlyCollection<AttractionDefinition> GetAll()
        {
            EnsureInitialized();
            return _definitions.Values;
        }

        /// <summary>指定テーマゾーン・カテゴリの両方に合致するアトラクション定義を取得する</summary>
        public static List<AttractionDefinition> GetByZoneAndCategory(ThemeZone zone, AttractionCategory category)
        {
            EnsureInitialized();
            return _definitions.Values
                .Where(d => d.PrimaryThemeZone == zone && d.Category == category)
                .ToList();
        }

        /// <summary>
        /// アトラクション定義からAttractionDataのScriptableObjectを動的に生成する。
        /// ランタイムでScriptableObjectアセットが存在しない場合の代替手段。
        /// </summary>
        /// <param name="attractionId">アトラクションID</param>
        /// <returns>生成されたAttractionData。定義が見つからない場合はnull</returns>
        public static AttractionData CreateAttractionData(string attractionId)
        {
            var def = GetById(attractionId);
            if (def == null) return null;

            var data = ScriptableObject.CreateInstance<AttractionData>();
            data.AttractionId = def.AttractionId;
            data.NameJP = def.NameJP;
            data.NameEN = def.NameEN;
            data.Description = def.Description;
            data.Category = def.Category;
            data.PrimaryThemeZone = def.PrimaryThemeZone;
            data.ExcitementRating = def.ExcitementRating;
            data.NauseaFactor = def.NauseaFactor;
            data.Capacity = def.Capacity;
            data.RideDuration = def.RideDuration;
            data.BuildCost = def.BuildCost;
            data.MaintenanceCost = def.MaintenanceCost;
            data.SuggestedTicketPrice = def.SuggestedTicketPrice;
            data.Size = def.Size;
            data.RequiredResearchId = def.RequiredResearchId;
            data.BaseBreakdownRate = def.BaseBreakdownRate;

            if (def.UpgradePath != null)
            {
                data.UpgradePath = new List<AttractionUpgradeLevel>(def.UpgradePath);
            }

            return data;
        }

        /// <summary>データベースの登録数を返す</summary>
        public static int Count
        {
            get
            {
                EnsureInitialized();
                return _definitions.Count;
            }
        }

        /// <summary>データベースを強制的に再初期化する（テスト用）</summary>
        public static void Reinitialize()
        {
            _initialized = false;
            EnsureInitialized();
        }
    }
}
