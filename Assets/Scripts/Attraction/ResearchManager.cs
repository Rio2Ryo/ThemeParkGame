// ============================================================
// ThemeParkGame - ResearchManager
// 研究開発システムの管理
// サイエンティストによる研究ツリーの進行を統括する
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;
using ThemeParkGame.Economy;

namespace ThemeParkGame.Attraction
{
    /// <summary>
    /// 研究開発システムの統括マネージャー。
    ///
    /// 研究の仕組み:
    /// - プレイヤーが研究予算を割り当てると、雇用中のサイエンティストが研究を進める
    /// - 研究速度 = サイエンティスト人数 * 平均スキルレベル * 予算係数
    /// - 同時に進行できる研究は1つのみ（リソース集中型）
    /// - 研究完了でアトラクション・ショップ・施設・アップグレードがアンロックされる
    /// - 研究ツリーは前提条件で構造化されており、基本→応用の順に進む
    ///
    /// サイエンティストの役割:
    /// - 研究ラボに配置されたサイエンティストの人数が研究速度に直結する
    /// - スキルレベルが高いほど効率的に研究が進む
    /// - サイエンティストが0人だと研究は進まない
    /// </summary>
    public class ResearchManager : MonoBehaviour
    {
        // ---- 研究データ ----

        /// <summary>全研究項目の辞書（ResearchId → ResearchItem）</summary>
        private Dictionary<string, ResearchItem> _allResearch = new Dictionary<string, ResearchItem>();
        public IReadOnlyDictionary<string, ResearchItem> AllResearch => _allResearch;

        /// <summary>現在進行中の研究項目（nullなら研究中でない）</summary>
        private ResearchItem _currentResearch;
        public ResearchItem CurrentResearch => _currentResearch;

        /// <summary>研究中かどうか</summary>
        public bool IsResearching => _currentResearch != null;

        // ---- 予算設定 ----

        /// <summary>
        /// 月間研究予算。
        /// 予算が高いほど研究速度にボーナスが付く（ただし上限あり）。
        /// 予算は毎月EconomyManagerから引かれる。
        /// </summary>
        [Header("Budget")]
        [SerializeField] private int monthlyResearchBudget = 1000;
        public int MonthlyResearchBudget
        {
            get => monthlyResearchBudget;
            set => monthlyResearchBudget = Mathf.Max(0, value);
        }

        /// <summary>
        /// 予算による研究速度ボーナス係数。
        /// 基準予算（BaseBudget）に対する現在予算の比率で計算。
        /// 最小0.5倍、最大2.0倍。
        /// </summary>
        private const int BaseBudget = 1000;
        private const float MinBudgetMultiplier = 0.5f;
        private const float MaxBudgetMultiplier = 2.0f;

        private float BudgetMultiplier
        {
            get
            {
                if (monthlyResearchBudget <= 0) return MinBudgetMultiplier;
                float ratio = (float)monthlyResearchBudget / BaseBudget;
                return Mathf.Clamp(ratio, MinBudgetMultiplier, MaxBudgetMultiplier);
            }
        }

        // ---- サイエンティスト参照 ----

        /// <summary>現在雇用中のサイエンティスト数（StaffManagerから取得想定）</summary>
        [Header("Scientists (Runtime)")]
        [SerializeField] private int scientistCount;
        public int ScientistCount
        {
            get => scientistCount;
            set => scientistCount = Mathf.Max(0, value);
        }

        /// <summary>サイエンティストの平均スキルレベル（0.0～1.0）</summary>
        [SerializeField] private float averageScientistSkill = 0.5f;
        public float AverageScientistSkill
        {
            get => averageScientistSkill;
            set => averageScientistSkill = Mathf.Clamp01(value);
        }

        // ---- 研究完了通知用キュー ----

        /// <summary>完了済みだが未通知の研究IDリスト（UI通知用）</summary>
        private readonly Queue<string> _pendingNotifications = new Queue<string>();
        public bool HasPendingNotification => _pendingNotifications.Count > 0;

        // ---- 統計 ----

        /// <summary>完了した研究の総数</summary>
        public int CompletedResearchCount { get; private set; }

        /// <summary>研究に投じた累計費用</summary>
        public float TotalResearchSpending { get; private set; }

        // ---- イベント購読 ----

        private void OnEnable()
        {
            GameEvents.OnStaffHired += OnStaffChanged;
            GameEvents.OnStaffFired += OnStaffChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnStaffHired -= OnStaffChanged;
            GameEvents.OnStaffFired -= OnStaffChanged;
        }

        private void OnStaffChanged(int staffId, StaffType type)
        {
            if (type == StaffType.Scientist)
            {
                RefreshScientistInfo();
            }
        }

        /// <summary>StaffManagerからサイエンティスト情報を取得して更新する</summary>
        private void RefreshScientistInfo()
        {
            if (GameManager.Instance?.StaffManager == null) return;
            var sm = GameManager.Instance.StaffManager;
            int count = sm.GetStaffCountByType(StaffType.Scientist);
            float avgSkill = 0.5f;
            var scientists = sm.GetStaffByType(StaffType.Scientist);
            if (scientists.Count > 0)
            {
                float totalSkill = 0f;
                foreach (var s in scientists)
                    totalSkill += (float)(s.SkillLevel - ThemeParkGame.Staff.StaffMember.MinSkillLevel)
                                  / (ThemeParkGame.Staff.StaffMember.MaxSkillLevel - ThemeParkGame.Staff.StaffMember.MinSkillLevel);
                avgSkill = totalSkill / scientists.Count;
            }
            UpdateScientistInfo(count, avgSkill);
        }

        // ---- 初期化 ----

        /// <summary>
        /// 研究システムを初期化する。
        /// GameManager.StartNewGame()から呼ばれる。
        /// </summary>
        public void Initialize()
        {
            _allResearch.Clear();
            _currentResearch = null;
            CompletedResearchCount = 0;
            TotalResearchSpending = 0f;

            RegisterAllResearchItems();
            UpdateResearchAvailability();
            RefreshScientistInfo();

            WebGLOptimizer.LogVerbose($"[ResearchManager] 初期化完了: {_allResearch.Count}件の研究項目を登録");
        }

        // ---- 研究項目の登録 ----

        /// <summary>
        /// 全研究項目を登録する。
        /// 研究ツリーの構造はここで定義される。
        /// 基本アトラクションは前提条件なし、上位アトラクションは下位の研究完了が必要。
        /// </summary>
        private void RegisterAllResearchItems()
        {
            // ==== ロストキングダム ====

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_LK_STONE_COASTER",
                NameJP = "ストーンコースター研究",
                NameEN = "Stone Coaster Research",
                Description = "古代遺跡をモチーフにしたジェットコースターの設計図を開発する",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.LostKingdom,
                ResearchCost = 2000,
                BaseResearchTime = 120f,
                UnlockedAttractionId = "LK_STONE_COASTER",
                UnlockedFacilityType = FacilityType.Attraction
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_LK_ANCIENT_WHEEL",
                NameJP = "エンシェントホイール研究",
                NameEN = "Ancient Wheel Research",
                Description = "古代文明の巨大車輪を再現した観覧車型アトラクション",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.LostKingdom,
                ResearchCost = 1500,
                BaseResearchTime = 90f,
                UnlockedAttractionId = "LK_ANCIENT_WHEEL",
                UnlockedFacilityType = FacilityType.Attraction
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_LK_DRAGON_BOAT",
                NameJP = "ドラゴンボート研究",
                NameEN = "Dragon Boat Research",
                Description = "地底湖を巡る神秘的なボートライドの開発",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.LostKingdom,
                RequiredPrerequisites = new List<string> { "RES_LK_STONE_COASTER" },
                ResearchCost = 2500,
                BaseResearchTime = 150f,
                UnlockedAttractionId = "LK_DRAGON_BOAT",
                UnlockedFacilityType = FacilityType.Attraction
            });

            // ==== ハロウィーンワールド ====

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_HW_GHOST_TRAIN",
                NameJP = "ゴーストトレイン研究",
                NameEN = "Ghost Train Research",
                Description = "恐怖の幽霊列車の設計。多数のギミックを搭載した見せ物系アトラクション",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.HalloweenWorld,
                ResearchCost = 2000,
                BaseResearchTime = 120f,
                UnlockedAttractionId = "HW_GHOST_TRAIN",
                UnlockedFacilityType = FacilityType.Attraction
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_HW_WITCH_SPIN",
                NameJP = "ウィッチスピン研究",
                NameEN = "Witch's Spin Research",
                Description = "魔女の大釜をモチーフにした高速回転アトラクション",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.HalloweenWorld,
                ResearchCost = 1800,
                BaseResearchTime = 100f,
                UnlockedAttractionId = "HW_WITCH_SPIN",
                UnlockedFacilityType = FacilityType.Attraction
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_HW_SCREAM_TOWER",
                NameJP = "スクリームタワー研究",
                NameEN = "Scream Tower Research",
                Description = "絶叫の塔。フリーフォール型の高G系アトラクション",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.HalloweenWorld,
                RequiredPrerequisites = new List<string> { "RES_HW_GHOST_TRAIN" },
                ResearchCost = 3000,
                BaseResearchTime = 180f,
                UnlockedAttractionId = "HW_SCREAM_TOWER",
                UnlockedFacilityType = FacilityType.Attraction
            });

            // ==== ワンダーランド ====

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_WL_FAIRY_CAROUSEL",
                NameJP = "フェアリーカルーセル研究",
                NameEN = "Fairy Carousel Research",
                Description = "妖精たちが飾る幻想的なメリーゴーラウンド",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.Wonderland,
                ResearchCost = 1200,
                BaseResearchTime = 80f,
                UnlockedAttractionId = "WL_FAIRY_CAROUSEL",
                UnlockedFacilityType = FacilityType.Attraction
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_WL_MAGIC_CARPET",
                NameJP = "マジックカーペット研究",
                NameEN = "Magic Carpet Research",
                Description = "空飛ぶ魔法の絨毯に乗ってワンダーランドを巡るライド",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.Wonderland,
                ResearchCost = 1800,
                BaseResearchTime = 110f,
                UnlockedAttractionId = "WL_MAGIC_CARPET",
                UnlockedFacilityType = FacilityType.Attraction
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_WL_GIANT_BEANSTALK",
                NameJP = "ジャイアントビーンストーク研究",
                NameEN = "Giant Beanstalk Research",
                Description = "ジャックと豆の木をモチーフにした展望タワー",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.Wonderland,
                RequiredPrerequisites = new List<string> { "RES_WL_FAIRY_CAROUSEL" },
                ResearchCost = 2200,
                BaseResearchTime = 140f,
                UnlockedAttractionId = "WL_GIANT_BEANSTALK",
                UnlockedFacilityType = FacilityType.Attraction
            });

            // ==== スペースゾーン ====

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_SZ_ROCKET_COASTER",
                NameJP = "ロケットコースター研究",
                NameEN = "Rocket Coaster Research",
                Description = "宇宙空間を駆け抜ける超高速ジェットコースター",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.SpaceZone,
                ResearchCost = 3000,
                BaseResearchTime = 160f,
                UnlockedAttractionId = "SZ_ROCKET_COASTER",
                UnlockedFacilityType = FacilityType.Attraction
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_SZ_UFO_SPINNER",
                NameJP = "UFOスピナー研究",
                NameEN = "UFO Spinner Research",
                Description = "UFO型の高速回転アトラクション。遠心力で宇宙酔い体験",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.SpaceZone,
                ResearchCost = 2000,
                BaseResearchTime = 120f,
                UnlockedAttractionId = "SZ_UFO_SPINNER",
                UnlockedFacilityType = FacilityType.Attraction
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_SZ_STAR_OBSERVATORY",
                NameJP = "スターオブザーバトリー研究",
                NameEN = "Star Observatory Research",
                Description = "宇宙の星々を望む巨大展望施設",
                Category = ResearchCategory.Attractions,
                RelatedZone = ThemeZone.SpaceZone,
                RequiredPrerequisites = new List<string> { "RES_SZ_ROCKET_COASTER" },
                ResearchCost = 2500,
                BaseResearchTime = 150f,
                UnlockedAttractionId = "SZ_STAR_OBSERVATORY",
                UnlockedFacilityType = FacilityType.Attraction
            });

            // ==== ショップ研究 ====

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_SHOP_GOURMET",
                NameJP = "グルメショップ研究",
                NameEN = "Gourmet Shop Research",
                Description = "高品質な食事を提供するグルメショップの開発",
                Category = ResearchCategory.Shops,
                IsZoneSpecific = false,
                ResearchCost = 1500,
                BaseResearchTime = 90f,
                UnlockedFacilityType = FacilityType.FoodShop
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_SHOP_PREMIUM_DRINK",
                NameJP = "プレミアムドリンク研究",
                NameEN = "Premium Drink Research",
                Description = "特別なドリンクメニューの開発",
                Category = ResearchCategory.Shops,
                IsZoneSpecific = false,
                ResearchCost = 1200,
                BaseResearchTime = 80f,
                UnlockedFacilityType = FacilityType.DrinkShop
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_SHOP_DELUXE_SOUVENIR",
                NameJP = "デラックスお土産研究",
                NameEN = "Deluxe Souvenir Research",
                Description = "高級お土産ラインナップの開発",
                Category = ResearchCategory.Shops,
                IsZoneSpecific = false,
                RequiredPrerequisites = new List<string> { "RES_SHOP_GOURMET" },
                ResearchCost = 2000,
                BaseResearchTime = 120f,
                UnlockedFacilityType = FacilityType.SouvenirShop
            });

            // ==== 施設研究 ====

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_FACILITY_STAFF_ROOM",
                NameJP = "スタッフルーム研究",
                NameEN = "Staff Room Research",
                Description = "スタッフの休憩施設。配置するとスタッフの満足度が向上し、ストライキ率が低下する",
                Category = ResearchCategory.Facilities,
                IsZoneSpecific = false,
                ResearchCost = 800,
                BaseResearchTime = 60f,
                UnlockedFacilityType = FacilityType.StaffRoom
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_FACILITY_INFO_BOARD",
                NameJP = "インフォメーションボード研究",
                NameEN = "Information Board Research",
                Description = "パーク内の案内板。来場者が迷子になりにくくなる",
                Category = ResearchCategory.Facilities,
                IsZoneSpecific = false,
                ResearchCost = 500,
                BaseResearchTime = 45f,
                UnlockedFacilityType = FacilityType.InfoBoard
            });

            // ==== アップグレード研究 ====

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_UPG_LK_COASTER_LV1",
                NameJP = "ストーンコースター強化Lv1",
                NameEN = "Stone Coaster Upgrade Lv1",
                Description = "ストーンコースターの軌道を延長し、興奮度を高める",
                Category = ResearchCategory.Upgrades,
                RelatedZone = ThemeZone.LostKingdom,
                RequiredPrerequisites = new List<string> { "RES_LK_STONE_COASTER" },
                ResearchCost = 1500,
                BaseResearchTime = 100f,
                UpgradeTargetAttractionId = "LK_STONE_COASTER",
                UpgradeLevel = 1
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_UPG_HW_SCREAM_LV1",
                NameJP = "スクリームタワー強化Lv1",
                NameEN = "Scream Tower Upgrade Lv1",
                Description = "スクリームタワーの高さを増し、より強烈なGを実現",
                Category = ResearchCategory.Upgrades,
                RelatedZone = ThemeZone.HalloweenWorld,
                RequiredPrerequisites = new List<string> { "RES_HW_SCREAM_TOWER" },
                ResearchCost = 2000,
                BaseResearchTime = 130f,
                UpgradeTargetAttractionId = "HW_SCREAM_TOWER",
                UpgradeLevel = 1
            });

            RegisterItem(new ResearchItem
            {
                ResearchId = "RES_UPG_SZ_ROCKET_LV1",
                NameJP = "ロケットコースター強化Lv1",
                NameEN = "Rocket Coaster Upgrade Lv1",
                Description = "ロケットコースターにブースターを追加し、最高速度を向上",
                Category = ResearchCategory.Upgrades,
                RelatedZone = ThemeZone.SpaceZone,
                RequiredPrerequisites = new List<string> { "RES_SZ_ROCKET_COASTER" },
                ResearchCost = 2500,
                BaseResearchTime = 150f,
                UpgradeTargetAttractionId = "SZ_ROCKET_COASTER",
                UpgradeLevel = 1
            });
        }

        /// <summary>研究項目を辞書に登録する</summary>
        private void RegisterItem(ResearchItem item)
        {
            if (_allResearch.ContainsKey(item.ResearchId))
            {
                WebGLOptimizer.LogWarning($"[ResearchManager] 重複した研究ID: {item.ResearchId}");
                return;
            }
            _allResearch[item.ResearchId] = item;
        }

        // ---- 研究の開始・進行・完了 ----

        /// <summary>
        /// 指定の研究を開始する。
        /// 前提条件を満たし、費用を支払える場合のみ開始される。
        /// 既に別の研究が進行中の場合は失敗する。
        /// </summary>
        /// <param name="researchId">開始する研究のID</param>
        /// <returns>開始成功ならtrue</returns>
        public bool StartResearch(string researchId)
        {
            if (_currentResearch != null)
            {
                WebGLOptimizer.LogWarning($"[ResearchManager] 既に研究進行中: {_currentResearch.ResearchId}");
                return false;
            }

            if (!_allResearch.TryGetValue(researchId, out var item))
            {
                WebGLOptimizer.LogError($"[ResearchManager] 研究項目が見つかりません: {researchId}");
                return false;
            }

            if (!item.CanStart(_allResearch))
            {
                WebGLOptimizer.LogWarning($"[ResearchManager] 前提条件未達成: {researchId}");
                return false;
            }

            // 研究費用の支払い
            TotalResearchSpending += item.ResearchCost;
            if (GameManager.Instance?.EconomyManager != null)
            {
                if (!GameManager.Instance.EconomyManager.CanAfford(item.ResearchCost))
                {
                    WebGLOptimizer.LogWarning($"[ResearchManager] 研究資金不足: {item.ResearchCost}");
                    TotalResearchSpending -= item.ResearchCost;
                    return false;
                }
                GameManager.Instance.EconomyManager.PayExpense(
                    item.ResearchCost, Economy.ExpenseCategory.Research);
            }
            else
            {
                GameEvents.FireExpensePaid(item.ResearchCost);
            }

            item.StartResearch();
            _currentResearch = item;

            GameEvents.FireResearchStarted(researchId);
            WebGLOptimizer.LogVerbose($"[ResearchManager] 研究開始: {item.NameJP} (コスト: {item.ResearchCost})");
            return true;
        }

        /// <summary>
        /// 進行中の研究をキャンセルする。
        /// 投入済みの費用は返還されない。進捗はリセットされる。
        /// </summary>
        public void CancelCurrentResearch()
        {
            if (_currentResearch == null) return;

            string cancelledId = _currentResearch.ResearchId;
            _currentResearch.CurrentState = ResearchState.Available;
            _currentResearch.CurrentProgress = 0f;
            _currentResearch = null;

            WebGLOptimizer.LogVerbose($"[ResearchManager] 研究キャンセル: {cancelledId}");
        }

        /// <summary>
        /// 毎フレームの研究進捗更新。
        /// サイエンティストの人数・スキル・予算に基づいて進捗を加算する。
        /// </summary>
        private void Update()
        {
            if (_currentResearch == null) return;
            if (scientistCount <= 0) return;

            // 研究速度の計算
            // 基本速度 * サイエンティスト人数補正 * スキル補正 * 予算補正
            float baseSpeed = 1f;
            float scientistMultiplier = 1f + (scientistCount - 1) * 0.4f; // 1人=1.0x, 2人=1.4x, 3人=1.8x...
            float skillMultiplier = 0.5f + averageScientistSkill; // スキル0=0.5x, スキル1.0=1.5x
            float budgetMult = BudgetMultiplier;

            float totalSpeed = baseSpeed * scientistMultiplier * skillMultiplier * budgetMult;
            float progressDelta = totalSpeed * Time.deltaTime;

            bool completed = _currentResearch.AddProgress(progressDelta);
            if (completed)
            {
                OnResearchCompleted(_currentResearch);
            }
        }

        /// <summary>
        /// 研究完了時の処理。
        /// アンロック通知を発行し、後続研究の利用可能状態を更新する。
        /// </summary>
        private void OnResearchCompleted(ResearchItem completedItem)
        {
            CompletedResearchCount++;
            _pendingNotifications.Enqueue(completedItem.ResearchId);

            string completedId = completedItem.ResearchId;
            _currentResearch = null;

            // 後続研究の利用可能状態を更新
            UpdateResearchAvailability();

            GameEvents.FireResearchCompleted(completedId);
            WebGLOptimizer.LogVerbose($"[ResearchManager] 研究完了: {completedItem.NameJP}");
        }

        /// <summary>
        /// 全研究項目の利用可能状態を更新する。
        /// Locked状態の項目について、前提条件が満たされていればAvailableに変更する。
        /// </summary>
        private void UpdateResearchAvailability()
        {
            foreach (var kvp in _allResearch)
            {
                var item = kvp.Value;
                if (item.CurrentState != ResearchState.Locked) continue;

                bool allPrereqsMet = true;
                foreach (string prereqId in item.RequiredPrerequisites)
                {
                    if (!_allResearch.TryGetValue(prereqId, out var prereq) ||
                        prereq.CurrentState != ResearchState.Completed)
                    {
                        allPrereqsMet = false;
                        break;
                    }
                }

                // 前提条件なし、または全前提条件クリア
                if (item.RequiredPrerequisites.Count == 0 || allPrereqsMet)
                {
                    item.CurrentState = ResearchState.Available;
                }
            }
        }

        // ---- クエリメソッド ----

        /// <summary>指定カテゴリの研究項目一覧を取得する</summary>
        public List<ResearchItem> GetResearchByCategory(ResearchCategory category)
        {
            return _allResearch.Values
                .Where(r => r.Category == category)
                .ToList();
        }

        /// <summary>指定テーマゾーンの研究項目一覧を取得する</summary>
        public List<ResearchItem> GetResearchByZone(ThemeZone zone)
        {
            return _allResearch.Values
                .Where(r => r.IsZoneSpecific && r.RelatedZone == zone)
                .ToList();
        }

        /// <summary>利用可能（開始可能）な研究項目一覧を取得する</summary>
        public List<ResearchItem> GetAvailableResearch()
        {
            return _allResearch.Values
                .Where(r => r.CurrentState == ResearchState.Available)
                .ToList();
        }

        /// <summary>完了済みの研究項目一覧を取得する</summary>
        public List<ResearchItem> GetCompletedResearch()
        {
            return _allResearch.Values
                .Where(r => r.CurrentState == ResearchState.Completed)
                .ToList();
        }

        /// <summary>指定IDの研究が完了しているかどうかを返す</summary>
        public bool IsResearchCompleted(string researchId)
        {
            return _allResearch.TryGetValue(researchId, out var item) &&
                   item.CurrentState == ResearchState.Completed;
        }

        /// <summary>指定アトラクションIDに関連する研究が完了しているかを返す</summary>
        public bool IsAttractionUnlocked(string attractionId)
        {
            return _allResearch.Values.Any(r =>
                r.UnlockedAttractionId == attractionId &&
                r.CurrentState == ResearchState.Completed);
        }

        /// <summary>未読の研究完了通知を1つ取得する（UIポップアップ用）</summary>
        public string DequeueNotification()
        {
            return _pendingNotifications.Count > 0 ? _pendingNotifications.Dequeue() : null;
        }

        /// <summary>指定IDの研究項目を取得する</summary>
        public ResearchItem GetResearchItem(string researchId)
        {
            _allResearch.TryGetValue(researchId, out var item);
            return item;
        }

        // ---- サイエンティスト管理の連携 ----

        /// <summary>
        /// サイエンティスト情報を更新する。
        /// StaffManagerからの通知で呼ばれることを想定。
        /// </summary>
        /// <param name="count">サイエンティスト人数</param>
        /// <param name="avgSkill">平均スキルレベル（0.0～1.0）</param>
        public void UpdateScientistInfo(int count, float avgSkill)
        {
            scientistCount = count;
            averageScientistSkill = Mathf.Clamp01(avgSkill);
        }

        // ---- デバッグ ----

        /// <summary>
        /// 指定研究を即座に完了させる。
        /// セーブデータ復元時にも使用されるため、ランタイムでも動作する。
        /// </summary>
        public void DebugCompleteResearch(string researchId)
        {
            if (!_allResearch.TryGetValue(researchId, out var item)) return;

            item.CurrentState = ResearchState.Completed;
            item.CurrentProgress = item.BaseResearchTime;

            if (_currentResearch == item)
                _currentResearch = null;

            UpdateResearchAvailability();
            GameEvents.FireResearchCompleted(researchId);
            WebGLOptimizer.LogVerbose($"[ResearchManager] 研究完了: {item.NameJP}");
        }

        /// <summary>全研究を即座に完了させる（デバッグ用）</summary>
        public void DebugCompleteAllResearch()
        {
            foreach (var kvp in _allResearch)
            {
                kvp.Value.CurrentState = ResearchState.Completed;
                kvp.Value.CurrentProgress = kvp.Value.BaseResearchTime;
            }
            _currentResearch = null;
            WebGLOptimizer.LogVerbose("[ResearchManager] 全研究完了");
        }
    }
}
