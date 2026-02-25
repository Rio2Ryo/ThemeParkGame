// ============================================================
// ThemeParkGame - ParkManager
// パーク全体の状態管理（ゾーン・施設配置・統計・認定証）
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Park
{
    /// <summary>
    /// テーマゾーンの状態データ。
    /// </summary>
    [Serializable]
    public class ThemeZoneState
    {
        public ThemeZone Zone;
        public bool IsUnlocked;

        /// <summary>アンロックに必要なゴールデンチケット枚数</summary>
        public int UnlockCost;

        /// <summary>ゾーン内の施設IDリスト</summary>
        public List<int> FacilityIds;

        /// <summary>ゾーン内のアトラクション数</summary>
        public int AttractionCount;

        /// <summary>ゾーン内のショップ数</summary>
        public int ShopCount;

        public ThemeZoneState()
        {
            FacilityIds = new List<int>();
        }
    }

    /// <summary>
    /// グリッド上に配置された施設の基本情報。
    /// </summary>
    [Serializable]
    public class PlacedFacility
    {
        public int FacilityId;
        public FacilityType Type;
        public string FacilityDataId;
        public int GridX;
        public int GridY;
        public int Width;
        public int Height;
        public ThemeZone Zone;
    }

    /// <summary>
    /// パーク全体の統計情報。
    /// </summary>
    [Serializable]
    public class ParkStats
    {
        /// <summary>現在のパーク内来場者数</summary>
        public int CurrentVisitorCount;

        /// <summary>累計来場者数</summary>
        public int TotalVisitorsEver;

        /// <summary>来場者の平均幸福度（0～1）</summary>
        public float AverageHappiness;

        /// <summary>パーク全体の清潔度（0～1）</summary>
        public float Cleanliness;

        /// <summary>安全性評価（0～100）</summary>
        public float SafetyRating;

        /// <summary>アトラクション総数</summary>
        public int TotalAttractions;

        /// <summary>ショップ総数</summary>
        public int TotalShops;

        /// <summary>スタッフ総数</summary>
        public int TotalStaff;

        /// <summary>累計事故件数</summary>
        public int TotalAccidents;
    }

    /// <summary>
    /// パーク全体の状態とゾーン管理を担当するマネージャー。
    ///
    /// 【ゲームデザイン: ゾーンシステム】
    /// 4つのテーマゾーンはそれぞれ独自の世界観を持つ:
    /// - LostKingdom: 古代遺跡テーマ。探検系アトラクションが多い。
    /// - HalloweenWorld: ホラーテーマ。スリル系が充実。夜間人気が高い。
    /// - Wonderland: ファンタジーテーマ。子供・家族向け。最も汎用的。
    /// - SpaceZone: 近未来テーマ。絶叫系・最先端アトラクション。
    ///
    /// 最初は1ゾーンのみアンロック。ゴールデンチケットを消費して拡張。
    /// ゾーンごとに専用アトラクション・装飾があり、テーマの統一感が評価に影響。
    ///
    /// 【グリッドシステム】
    /// 各ゾーンは独立したグリッドを持ち、施設はグリッド単位で配置される。
    /// 通路で施設間を接続し、来場者が移動できるようにする必要がある。
    /// </summary>
    public class ParkManager : MonoBehaviour
    {
        [Header("Park Settings")]
        [SerializeField] private int gridWidth = 64;
        [SerializeField] private int gridHeight = 64;

        /// <summary>パーク総合評価システム</summary>
        public ParkRating Rating { get; private set; }

        /// <summary>パーク統計</summary>
        public ParkStats Stats { get; private set; }

        /// <summary>現在のシナリオデータ（シナリオモード時のみ）</summary>
        public ScenarioData CurrentScenario { get; private set; }

        /// <summary>シナリオモードかどうか</summary>
        public bool IsScenarioMode => CurrentScenario != null;

        // ---- ゾーン管理 ----
        private readonly Dictionary<ThemeZone, ThemeZoneState> _zones
            = new Dictionary<ThemeZone, ThemeZoneState>();

        // ---- 施設管理 ----
        private readonly Dictionary<int, PlacedFacility> _placedFacilities
            = new Dictionary<int, PlacedFacility>();

        /// <summary>
        /// グリッドマップ。各ゾーンのグリッドを管理する。
        /// grid[x, y] = 配置されている施設のID（0 = 空き）
        /// </summary>
        private readonly Dictionary<ThemeZone, int[,]> _grids
            = new Dictionary<ThemeZone, int[,]>();

        private int _nextFacilityId = 1;

        // ================================================================
        // 初期化
        // ================================================================

        /// <summary>
        /// フリーモードでパークを初期化する。
        /// 指定ゾーンのみアンロック状態で開始。
        /// </summary>
        public void Initialize(ThemeZone startingZone)
        {
            CurrentScenario = null;
            InitializeZones(startingZone);
            InitializeGrids();
            InitializeRatingAndStats();
            SubscribeToEvents();

            WebGLOptimizer.LogVerbose($"[ParkManager] 初期化完了。開始ゾーン: {startingZone}");
        }

        /// <summary>
        /// シナリオモードでパークを初期化する。
        /// シナリオデータに基づき初期施設を配置する。
        /// </summary>
        public void InitializeFromScenario(ScenarioData scenario)
        {
            CurrentScenario = scenario;
            InitializeZones(scenario.InitialZone);
            InitializeGrids();
            InitializeRatingAndStats();
            SubscribeToEvents();

            // 初期施設を配置
            foreach (var facility in scenario.PrebuiltFacilities)
            {
                PlaceFacility(facility.FacilityType, facility.FacilityId,
                    facility.GridX, facility.GridY, 2, 2, facility.Zone);
            }

            WebGLOptimizer.LogVerbose($"[ParkManager] シナリオ初期化完了: {scenario.Country} " +
                      $"(初期施設: {scenario.PrebuiltFacilities.Count}件)");
        }

        private void InitializeZones(ThemeZone startingZone)
        {
            _zones.Clear();

            // 各ゾーンのアンロックコスト設定
            var unlockCosts = new Dictionary<ThemeZone, int>
            {
                { ThemeZone.LostKingdom, 2 },
                { ThemeZone.HalloweenWorld, 3 },
                { ThemeZone.Wonderland, 2 },
                { ThemeZone.SpaceZone, 3 }
            };

            foreach (ThemeZone zone in Enum.GetValues(typeof(ThemeZone)))
            {
                _zones[zone] = new ThemeZoneState
                {
                    Zone = zone,
                    IsUnlocked = (zone == startingZone),
                    UnlockCost = unlockCosts.ContainsKey(zone) ? unlockCosts[zone] : 2
                };
            }
        }

        private void InitializeGrids()
        {
            _grids.Clear();
            _placedFacilities.Clear();
            _nextFacilityId = 1;

            foreach (ThemeZone zone in Enum.GetValues(typeof(ThemeZone)))
            {
                _grids[zone] = new int[gridWidth, gridHeight];
            }
        }

        private void InitializeRatingAndStats()
        {
            Rating = new ParkRating();
            Stats = new ParkStats();
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnAttractionAccident += OnAttractionAccidentHandler;
            GameEvents.OnVisitorEnterPark += OnVisitorEnterHandler;
            GameEvents.OnVisitorLeavePark += OnVisitorLeaveHandler;

            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnMonthChanged += OnMonthEnd;
                GameManager.Instance.TimeManager.OnYearChanged += OnYearEnd;
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnAttractionAccident -= OnAttractionAccidentHandler;
            GameEvents.OnVisitorEnterPark -= OnVisitorEnterHandler;
            GameEvents.OnVisitorLeavePark -= OnVisitorLeaveHandler;

            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnMonthChanged -= OnMonthEnd;
                GameManager.Instance.TimeManager.OnYearChanged -= OnYearEnd;
            }
        }

        // ================================================================
        // ゾーン管理
        // ================================================================

        /// <summary>
        /// ゴールデンチケットを消費してテーマゾーンをアンロックする。
        /// </summary>
        /// <returns>アンロック成功か</returns>
        public bool UnlockZone(ThemeZone zone)
        {
            if (!_zones.TryGetValue(zone, out ThemeZoneState state))
            {
                return false;
            }

            if (state.IsUnlocked)
            {
                WebGLOptimizer.LogVerbose($"[ParkManager] {zone} は既にアンロック済み");
                return false;
            }

            if (GameManager.Instance == null || !GameManager.Instance.SpendGoldenTicket(state.UnlockCost))
            {
                WebGLOptimizer.LogVerbose($"[ParkManager] ゴールデンチケット不足。必要: {state.UnlockCost}枚");
                return false;
            }

            state.IsUnlocked = true;
            GameEvents.FireThemeZoneUnlocked(zone);
            WebGLOptimizer.LogVerbose($"[ParkManager] {zone} をアンロック! (チケット消費: {state.UnlockCost}枚)");
            return true;
        }

        /// <summary>ゾーンがアンロック済みかを確認する</summary>
        public bool IsZoneUnlocked(ThemeZone zone)
        {
            return _zones.TryGetValue(zone, out ThemeZoneState state) && state.IsUnlocked;
        }

        /// <summary>ゾーンのアンロックコストを取得する</summary>
        public int GetZoneUnlockCost(ThemeZone zone)
        {
            return _zones.TryGetValue(zone, out ThemeZoneState state) ? state.UnlockCost : 0;
        }

        /// <summary>ゾーン状態を取得する</summary>
        public ThemeZoneState GetZoneState(ThemeZone zone)
        {
            _zones.TryGetValue(zone, out ThemeZoneState state);
            return state;
        }

        /// <summary>アンロック済みゾーン一覧を取得する</summary>
        public List<ThemeZone> GetUnlockedZones()
        {
            return _zones.Where(kvp => kvp.Value.IsUnlocked).Select(kvp => kvp.Key).ToList();
        }

        // ================================================================
        // グリッド配置システム
        // ================================================================

        /// <summary>
        /// 施設をグリッドに配置する。
        /// </summary>
        /// <returns>配置された施設のID（失敗時は-1）</returns>
        public int PlaceFacility(FacilityType type, string facilityDataId,
            int gridX, int gridY, int width, int height, ThemeZone zone)
        {
            // ゾーンアンロックチェック
            if (!IsZoneUnlocked(zone))
            {
                Debug.LogWarning($"[ParkManager] ゾーン {zone} はアンロックされていません");
                return -1;
            }

            // グリッド範囲チェック
            if (!IsGridAreaAvailable(zone, gridX, gridY, width, height))
            {
                Debug.LogWarning($"[ParkManager] グリッド({gridX},{gridY}) size({width}x{height}) は配置不可");
                return -1;
            }

            int facilityId = _nextFacilityId++;

            var facility = new PlacedFacility
            {
                FacilityId = facilityId,
                Type = type,
                FacilityDataId = facilityDataId,
                GridX = gridX,
                GridY = gridY,
                Width = width,
                Height = height,
                Zone = zone
            };

            _placedFacilities[facilityId] = facility;

            // グリッドにマーキング
            int[,] grid = _grids[zone];
            for (int x = gridX; x < gridX + width; x++)
            {
                for (int y = gridY; y < gridY + height; y++)
                {
                    grid[x, y] = facilityId;
                }
            }

            // ゾーン統計を更新
            ThemeZoneState zoneState = _zones[zone];
            zoneState.FacilityIds.Add(facilityId);

            if (type == FacilityType.Attraction)
            {
                zoneState.AttractionCount++;
                Stats.TotalAttractions++;
            }
            else if (type == FacilityType.FoodShop || type == FacilityType.DrinkShop ||
                     type == FacilityType.SouvenirShop)
            {
                zoneState.ShopCount++;
                Stats.TotalShops++;
            }

            GameEvents.FireAttractionBuilt(facilityId);
            WebGLOptimizer.LogVerbose($"[ParkManager] 施設配置: {facilityDataId} (ID:{facilityId}) at ({gridX},{gridY}) in {zone}");
            return facilityId;
        }

        /// <summary>施設をグリッドから撤去する</summary>
        public bool RemoveFacility(int facilityId)
        {
            if (!_placedFacilities.TryGetValue(facilityId, out PlacedFacility facility))
            {
                return false;
            }

            // グリッドからクリア
            int[,] grid = _grids[facility.Zone];
            for (int x = facility.GridX; x < facility.GridX + facility.Width; x++)
            {
                for (int y = facility.GridY; y < facility.GridY + facility.Height; y++)
                {
                    if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                    {
                        grid[x, y] = 0;
                    }
                }
            }

            // ゾーン統計を更新
            ThemeZoneState zoneState = _zones[facility.Zone];
            zoneState.FacilityIds.Remove(facilityId);

            if (facility.Type == FacilityType.Attraction)
            {
                zoneState.AttractionCount--;
                Stats.TotalAttractions--;
            }
            else if (facility.Type == FacilityType.FoodShop || facility.Type == FacilityType.DrinkShop ||
                     facility.Type == FacilityType.SouvenirShop)
            {
                zoneState.ShopCount--;
                Stats.TotalShops--;
            }

            _placedFacilities.Remove(facilityId);
            WebGLOptimizer.LogVerbose($"[ParkManager] 施設撤去: ID={facilityId}");
            return true;
        }

        /// <summary>グリッドの指定領域が配置可能かチェックする</summary>
        public bool IsGridAreaAvailable(ThemeZone zone, int gridX, int gridY, int width, int height)
        {
            if (!_grids.TryGetValue(zone, out int[,] grid))
            {
                return false;
            }

            for (int x = gridX; x < gridX + width; x++)
            {
                for (int y = gridY; y < gridY + height; y++)
                {
                    if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                    {
                        return false;
                    }
                    if (grid[x, y] != 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>指定グリッド座標の施設IDを取得する</summary>
        public int GetFacilityAtGrid(ThemeZone zone, int gridX, int gridY)
        {
            if (!_grids.TryGetValue(zone, out int[,] grid))
            {
                return 0;
            }
            if (gridX < 0 || gridX >= gridWidth || gridY < 0 || gridY >= gridHeight)
            {
                return 0;
            }
            return grid[gridX, gridY];
        }

        /// <summary>施設情報を取得する</summary>
        public PlacedFacility GetPlacedFacility(int facilityId)
        {
            _placedFacilities.TryGetValue(facilityId, out PlacedFacility facility);
            return facility;
        }

        /// <summary>全配置済み施設を取得する</summary>
        public IReadOnlyDictionary<int, PlacedFacility> GetAllFacilities()
        {
            return _placedFacilities;
        }

        /// <summary>指定タイプの施設一覧を取得する</summary>
        public List<PlacedFacility> GetFacilitiesByType(FacilityType type)
        {
            return _placedFacilities.Values.Where(f => f.Type == type).ToList();
        }

        /// <summary>指定ゾーン内の施設一覧を取得する</summary>
        public List<PlacedFacility> GetFacilitiesInZone(ThemeZone zone)
        {
            return _placedFacilities.Values.Where(f => f.Zone == zone).ToList();
        }

        // ================================================================
        // パーク統計・評価
        // ================================================================

        /// <summary>
        /// パーク統計を外部から更新する。
        /// VisitorManager等の他システムから毎フレームまたは定期的に呼ばれる。
        /// </summary>
        public void UpdateStats(int currentVisitors, float averageHappiness,
            float cleanliness, int totalStaff)
        {
            Stats.CurrentVisitorCount = currentVisitors;
            Stats.AverageHappiness = averageHappiness;
            Stats.Cleanliness = cleanliness;
            Stats.TotalStaff = totalStaff;
        }

        /// <summary>
        /// パーク評価を全カテゴリ再計算する。
        /// 月末処理で呼ばれる。
        /// </summary>
        /// <param name="mechanicCoverageRatio">メカニック充足率（0～1）</param>
        /// <param name="toiletCoverage">トイレ充足率（0～1）</param>
        /// <param name="benchCoverage">ベンチ充足率（0～1）</param>
        /// <param name="foodDrinkAvailability">飲食施設充足率（0～1）</param>
        /// <param name="uniqueAttractionCategories">異なるアトラクションカテゴリ数</param>
        /// <param name="averageAttractionQuality">平均アトラクション品質（0～1）</param>
        /// <param name="entertainerCoverage">エンターテイナー充足率（0～1）</param>
        /// <param name="recentAccidents">直近1年の事故件数</param>
        public void RecalculateAllRatings(
            float mechanicCoverageRatio,
            float toiletCoverage,
            float benchCoverage,
            float foodDrinkAvailability,
            int uniqueAttractionCategories,
            float averageAttractionQuality,
            float entertainerCoverage,
            int recentAccidents)
        {
            int parkAge = 1;
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                parkAge = GameManager.Instance.TimeManager.CurrentYear;
            }

            Rating.UpdateFameRating(Stats.TotalVisitorsEver, parkAge);
            Rating.UpdateSafetyRating(recentAccidents, mechanicCoverageRatio, Stats.TotalAttractions);
            Rating.UpdateComfortRating(Stats.Cleanliness, toiletCoverage, benchCoverage, foodDrinkAvailability);
            Rating.UpdateExcitementRating(Stats.TotalAttractions, uniqueAttractionCategories, averageAttractionQuality);
            Rating.UpdateMoodRating(Stats.AverageHappiness, entertainerCoverage);
            Rating.RecalculateOverallRating();

            Stats.SafetyRating = Rating.GetCategoryScore(CertificateCategory.Safety);
        }

        /// <summary>パーク総合評価を取得する（0～100）</summary>
        public float GetOverallRating()
        {
            return Rating.OverallRating;
        }

        // ================================================================
        // 認定証システム
        // ================================================================

        /// <summary>認定証の条件をチェックし、新たに授与された認定証を返す</summary>
        public List<CertificateCategory> CheckCertificates()
        {
            return Rating.CheckAndAwardCertificates();
        }

        /// <summary>指定カテゴリの認定証が取得済みか確認する</summary>
        public bool HasCertificate(CertificateCategory category)
        {
            return Rating.IsCertificateAwarded(category);
        }

        // ================================================================
        // シナリオ目標チェック
        // ================================================================

        /// <summary>
        /// シナリオ目標の達成状況をチェックし、達成した目標を更新する。
        /// </summary>
        public void CheckScenarioObjectives()
        {
            if (CurrentScenario == null) return;

            foreach (var objective in CurrentScenario.Objectives)
            {
                if (objective.IsCompleted) continue;

                bool completed = EvaluateObjective(objective);
                if (completed)
                {
                    objective.IsCompleted = true;
                    WebGLOptimizer.LogVerbose($"[ParkManager] シナリオ目標達成: {objective.DescriptionKey}");
                }
            }

            // 全目標達成チェック
            if (CurrentScenario.AreAllObjectivesCompleted())
            {
                OnScenarioCompleted();
            }
        }

        private bool EvaluateObjective(ScenarioObjective objective)
        {
            switch (objective.Type)
            {
                case ObjectiveType.VisitorTarget:
                    return Stats.TotalVisitorsEver >= objective.TargetValue;

                case ObjectiveType.MonthlyProfitTarget:
                    if (GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
                    {
                        return GameManager.Instance.EconomyManager.GetMonthlyProfit() >= objective.TargetValue;
                    }
                    return false;

                case ObjectiveType.ParkRatingTarget:
                    return Rating.OverallRating >= objective.TargetValue;

                case ObjectiveType.ObtainCertificate:
                    var category = (CertificateCategory)objective.SubParameter;
                    return Rating.IsCertificateAwarded(category);

                case ObjectiveType.AttractionCountTarget:
                    return Stats.TotalAttractions >= objective.TargetValue;

                case ObjectiveType.UnlockZone:
                    var zone = (ThemeZone)objective.SubParameter;
                    return IsZoneUnlocked(zone);

                case ObjectiveType.TotalRevenueTarget:
                    if (GameManager.Instance != null && GameManager.Instance.EconomyManager != null)
                    {
                        return GameManager.Instance.EconomyManager.TotalRevenueEarned >= objective.TargetValue;
                    }
                    return false;

                case ObjectiveType.HappinessTarget:
                    return Stats.AverageHappiness * 100f >= objective.TargetValue;

                case ObjectiveType.GoldenTicketTarget:
                    if (GameManager.Instance != null)
                    {
                        return GameManager.Instance.GoldenTickets >= objective.TargetValue;
                    }
                    return false;

                default:
                    return false;
            }
        }

        private void OnScenarioCompleted()
        {
            WebGLOptimizer.LogVerbose($"[ParkManager] シナリオクリア! {CurrentScenario.Country}");

            // 報酬を付与
            ScenarioReward reward = CurrentScenario.CompletionReward;

            if (GameManager.Instance != null)
            {
                for (int i = 0; i < reward.GoldenTickets; i++)
                {
                    GameManager.Instance.AwardGoldenTicket();
                }

                if (reward.BonusMoney > 0 && GameManager.Instance.EconomyManager != null)
                {
                    GameManager.Instance.EconomyManager.AddRevenue(
                        reward.BonusMoney, Economy.RevenueCategory.Other);
                }

                if (reward.UnlockedZone.HasValue)
                {
                    // 報酬ゾーンは無料でアンロック
                    ThemeZoneState zoneState = _zones[reward.UnlockedZone.Value];
                    if (!zoneState.IsUnlocked)
                    {
                        zoneState.IsUnlocked = true;
                        GameEvents.FireThemeZoneUnlocked(reward.UnlockedZone.Value);
                    }
                }
            }
        }

        // ================================================================
        // イベントハンドラ
        // ================================================================

        private void OnAttractionAccidentHandler(int attractionId)
        {
            Stats.TotalAccidents++;

            int currentYear = 1;
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                currentYear = GameManager.Instance.TimeManager.CurrentYear;
            }
            Rating.RecordAccident(currentYear);

            WebGLOptimizer.LogVerbose($"[ParkManager] 事故発生! アトラクションID: {attractionId}, 累計事故: {Stats.TotalAccidents}");
        }

        private void OnVisitorEnterHandler(int visitorId)
        {
            Stats.CurrentVisitorCount++;
            Stats.TotalVisitorsEver++;
        }

        private void OnVisitorLeaveHandler(int visitorId)
        {
            Stats.CurrentVisitorCount = Mathf.Max(0, Stats.CurrentVisitorCount - 1);
        }

        private void OnMonthEnd()
        {
            // 認定証チェック
            var newCertificates = CheckCertificates();
            foreach (var cert in newCertificates)
            {
                WebGLOptimizer.LogVerbose($"[ParkManager] 新認定証: {cert}");
            }

            // シナリオ目標チェック
            CheckScenarioObjectives();
        }

        private void OnYearEnd(int newYear)
        {
            Rating.OnYearAdvanced(newYear);
            WebGLOptimizer.LogVerbose($"[ParkManager] Year {newYear} 開始。総合評価: {Rating.OverallRating:F1}");
        }

        // ================================================================
        // ユーティリティ
        // ================================================================

        /// <summary>
        /// 充足率を計算する汎用ヘルパー。
        /// 施設タイプの数を来場者数で割って充足率を算出。
        /// </summary>
        /// <param name="facilityType">対象施設タイプ</param>
        /// <param name="facilityCapacity">施設1つあたりのカバー人数</param>
        /// <returns>充足率（0～1、1以上は1にクランプ）</returns>
        public float CalculateFacilityCoverage(FacilityType facilityType, int facilityCapacity)
        {
            int facilityCount = _placedFacilities.Values.Count(f => f.Type == facilityType);
            int totalCapacity = facilityCount * facilityCapacity;
            int visitors = Stats.CurrentVisitorCount;

            if (visitors <= 0) return 1.0f;
            return Mathf.Clamp01((float)totalCapacity / visitors);
        }

        /// <summary>パーク内の全ゾーン状態を取得する</summary>
        public IReadOnlyDictionary<ThemeZone, ThemeZoneState> GetAllZoneStates()
        {
            return _zones;
        }
    }
}
