// ============================================================
// ThemeParkGame - SaveSystem
// セーブ/ロードシステム（JSON形式でIndexedDB+localStorageに保存）
// WebGL環境ではIndexedDBを使用し、非WebGLではPlayerPrefsにフォールバック
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.AI;
using ThemeParkGame.Attraction;
using ThemeParkGame.Park;
using ThemeParkGame.Staff;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// セーブデータの構造体。ゲーム全体の状態をシリアライズ可能な形式で保持する。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public string SaveVersion = "4.0";
        public string SaveDate;

        // ゲーム時間
        public int CurrentYear;
        public int CurrentMonth;
        public int CurrentDay;
        public float CurrentHour;

        // 経済
        public float CurrentBalance;
        public float TotalRevenueEarned;
        public float TotalExpensesPaid;

        // パーク
        public int GoldenTickets;
        public List<string> UnlockedZones = new List<string>();
        public int TotalAttractions;
        public int TotalShops;
        public int TotalVisitorsEver;

        // 難易度
        public string Difficulty;

        // 来場者統計
        public int TotalVisitorsToday;
        public float AverageHappiness;

        // 研究
        public List<string> CompletedResearchIds = new List<string>();
        public string CurrentResearchId;
        public float CurrentResearchProgress;

        // スタッフ
        public int TotalStaff;

        // 天候
        public string CurrentWeather;
        public string CurrentSeason;

        // シナリオ
        public bool IsScenarioMode;
        public string ScenarioCountry;

        // パーク評価
        public float RatingOverall;
        public float RatingFame;
        public float RatingSafety;
        public float RatingComfort;
        public float RatingExcitement;
        public float RatingMood;

        // 認定証
        public List<string> AwardedCertificates = new List<string>();

        // ライフサイクル統計（累積）
        public float LifecycleWaitingTime;
        public float LifecycleEnjoyingTime;
        public float LifecycleLeavingTime;
        public int LifecycleExperienceStarts;
        public int LifecycleExperienceCompletions;
        public float LifecycleEnjoymentRatio;

        // SNSレピュテーション
        public float SNSReputation;
        public int SNSTotalPosts;

        // 入場料
        public float EntranceFee = 15f;

        // ローン
        public List<SavedLoan> ActiveLoans = new List<SavedLoan>();

        // 配置済み建物
        public List<SavedBuilding> PlacedBuildings = new List<SavedBuilding>();

        // スタッフ
        public List<SavedStaff> StaffMembers = new List<SavedStaff>();

        // パーク拡張（購入済み区画ID）
        public List<string> PurchasedLandPlots = new List<string>();

        // Phase 8-10 data
        // アクシデント
        public int AccidentResolvedCount;
        public int AccidentTotalCount;
        public bool AccidentHasResolvedCritical;

        // 口コミスコア
        public float WordOfMouthScore;

        // ライバルパーク
        public int RivalParkCount;

        // セールキャンペーン使用種類数
        public int SaleTypesUsed;

        // チャレンジ
        public int ChallengesCompleted;

        // 実績用トラッカー
        public bool HadLoan;
        public bool HasFiredStaff;
    }

    /// <summary>配置済み建物のセーブ用データ構造</summary>
    [Serializable]
    public class SavedBuilding
    {
        public string Type;           // FacilityType名 (Attraction, FoodShop, etc.)
        public string DataId;         // 施設データID (FacilityDataId or name)
        public float PosX;
        public float PosY;
        public float PosZ;
        public int GridX;
        public int GridY;
        public int Width;
        public int Height;
        public string Zone;           // ThemeZone名
        public int UpgradeLevel;
        public int TicketPrice;
        // Attraction固有
        public string Category;       // AttractionCategory名
        public float Excitement;
        public float NauseaFactor;
        public int Capacity;
        public float RideDuration;
        public int BuildCost;
        // Shop固有
        public int WholesalePrice;
        public int SellingPrice;
        public int Stock;
    }

    /// <summary>スタッフのセーブ用データ構造</summary>
    [Serializable]
    public class SavedStaff
    {
        public string Type;          // StaffType名
        public string Name;
        public int SkillLevel;
        public float Fatigue;
        public float Salary;
        public float WorkExperience;
        public float PosX, PosY, PosZ;
        // パトロールエリア
        public float PatrolCenterX, PatrolCenterY, PatrolCenterZ;
        public float PatrolSizeX, PatrolSizeY, PatrolSizeZ;
    }

    /// <summary>ローンのセーブ用データ構造</summary>
    [Serializable]
    public class SavedLoan
    {
        public int LoanId;
        public float Principal;
        public float AnnualInterestRate;
        public float RemainingBalance;
        public float MonthlyPayment;
        public int TermMonths;
        public int ElapsedMonths;
    }

    /// <summary>
    /// セーブ/ロードを管理するシステム。
    /// WebGLStorageHelper経由でJSONシリアライズされたデータを保存する。
    /// WebGL環境ではIndexedDB+localStorage、非WebGLではPlayerPrefsを使用。
    /// 最大3つのセーブスロットをサポート。
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        private const string SAVE_KEY_PREFIX = "ThemeParkGame_Save_";
        private const string AUTOSAVE_KEY = "ThemeParkGame_AutoSave";
        private const int MAX_SAVE_SLOTS = 3;
        private static bool _autoSaveSubscribed;

        /// <summary>セーブスロットにデータが存在するか確認する</summary>
        public static bool HasSaveData(int slot)
        {
            if (slot < 0 || slot >= MAX_SAVE_SLOTS) return false;
            return WebGLStorageHelper.HasKey(SAVE_KEY_PREFIX + slot);
        }

        /// <summary>いずれかのスロットにセーブが存在するか</summary>
        public static bool HasAnySaveData()
        {
            for (int i = 0; i < MAX_SAVE_SLOTS; i++)
            {
                if (HasSaveData(i)) return true;
            }
            return false;
        }

        /// <summary>
        /// 現在のゲーム状態をセーブする。
        /// </summary>
        /// <param name="slot">セーブスロット番号（0～2）</param>
        /// <returns>セーブ成功ならtrue</returns>
        public static bool Save(int slot)
        {
            if (slot < 0 || slot >= MAX_SAVE_SLOTS)
            {
                Debug.LogError($"[SaveSystem] Invalid slot: {slot}");
                return false;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogError("[SaveSystem] GameManager not found");
                return false;
            }

            try
            {
                SaveData data = CollectSaveData();
                string json = JsonUtility.ToJson(data, true);
                WebGLStorageHelper.SetItem(SAVE_KEY_PREFIX + slot, json);

                GameEvents.FireGameSaved();
                WebGLOptimizer.LogVerbose($"[SaveSystem] Saved to slot {slot} ({json.Length} bytes)");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// セーブデータをロードしてゲーム状態を復元する。
        /// </summary>
        /// <param name="slot">セーブスロット番号（0～2）</param>
        /// <returns>ロード成功ならtrue</returns>
        public static bool Load(int slot)
        {
            if (!HasSaveData(slot))
            {
                Debug.LogWarning($"[SaveSystem] No save data in slot {slot}");
                return false;
            }

            try
            {
                string json = WebGLStorageHelper.GetItem(SAVE_KEY_PREFIX + slot);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                data = MigrateSaveData(data);

                if (!ValidateSaveData(data))
                {
                    Debug.LogError($"[SaveSystem] Save data in slot {slot} is corrupted");
                    return false;
                }

                ApplySaveData(data);

                GameEvents.FireGameLoaded();
                WebGLOptimizer.LogVerbose($"[SaveSystem] Loaded from slot {slot} " +
                          $"(Year {data.CurrentYear}, Balance: {data.CurrentBalance:F0})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Load failed: {e.Message}");
                return false;
            }
        }

        /// <summary>セーブデータを削除する</summary>
        public static void DeleteSave(int slot)
        {
            if (slot < 0 || slot >= MAX_SAVE_SLOTS) return;
            WebGLStorageHelper.DeleteItem(SAVE_KEY_PREFIX + slot);
            WebGLOptimizer.LogVerbose($"[SaveSystem] Deleted slot {slot}");
        }

        /// <summary>セーブスロットのメタ情報を取得する</summary>
        public static SaveData GetSaveInfo(int slot)
        {
            if (!HasSaveData(slot)) return null;

            try
            {
                string json = WebGLStorageHelper.GetItem(SAVE_KEY_PREFIX + slot);
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>最大スロット数</summary>
        public static int MaxSlots => MAX_SAVE_SLOTS;

        // ================================================================
        // データ収集
        // ================================================================

        private static SaveData CollectSaveData()
        {
            var gm = GameManager.Instance;
            var data = new SaveData
            {
                SaveDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
                GoldenTickets = gm.GoldenTickets,
                Difficulty = gm.CurrentDifficulty.ToString()
            };

            // 時間
            if (gm.TimeManager != null)
            {
                data.CurrentYear = gm.TimeManager.CurrentYear;
                data.CurrentMonth = gm.TimeManager.CurrentMonth;
                data.CurrentDay = gm.TimeManager.CurrentDay;
                data.CurrentHour = gm.TimeManager.CurrentHour;
            }

            // 経済
            if (gm.EconomyManager != null)
            {
                data.CurrentBalance = gm.EconomyManager.CurrentBalance;
                data.TotalRevenueEarned = gm.EconomyManager.TotalRevenueEarned;
                data.TotalExpensesPaid = gm.EconomyManager.TotalExpensesPaid;

                // 入場料
                if (gm.EconomyManager.Pricing != null)
                    data.EntranceFee = gm.EconomyManager.Pricing.EntranceFee;

                // ローンデータ
                var loans = gm.EconomyManager.GetActiveLoans();
                foreach (var loan in loans)
                {
                    data.ActiveLoans.Add(new SavedLoan
                    {
                        LoanId = loan.LoanId,
                        Principal = loan.Principal,
                        AnnualInterestRate = loan.AnnualInterestRate,
                        RemainingBalance = loan.RemainingBalance,
                        MonthlyPayment = loan.MonthlyPayment,
                        TermMonths = loan.TermMonths,
                        ElapsedMonths = loan.ElapsedMonths
                    });
                }
            }

            // 来場者統計
            if (gm.VisitorManager != null)
            {
                data.TotalVisitorsToday = gm.VisitorManager.TotalVisitorsToday;
                data.AverageHappiness = gm.VisitorManager.AverageHappiness;

                // ライフサイクル統計
                data.LifecycleWaitingTime = gm.VisitorManager.TotalWaitingTime;
                data.LifecycleEnjoyingTime = gm.VisitorManager.TotalEnjoyingTime;
                data.LifecycleLeavingTime = gm.VisitorManager.TotalLeavingTime;
                data.LifecycleExperienceStarts = gm.VisitorManager.TotalExperienceStarts;
                data.LifecycleExperienceCompletions = gm.VisitorManager.TotalExperienceCompletions;
                data.LifecycleEnjoymentRatio = gm.VisitorManager.OverallEnjoymentRatio;
            }

            // パーク
            if (gm.ParkManager != null)
            {
                data.TotalAttractions = gm.ParkManager.Stats.TotalAttractions;
                data.TotalShops = gm.ParkManager.Stats.TotalShops;
                data.TotalVisitorsEver = gm.ParkManager.Stats.TotalVisitorsEver;
                data.IsScenarioMode = gm.ParkManager.IsScenarioMode;

                if (gm.ParkManager.CurrentScenario != null)
                    data.ScenarioCountry = gm.ParkManager.CurrentScenario.Country.ToString();

                foreach (var zone in gm.ParkManager.GetUnlockedZones())
                {
                    data.UnlockedZones.Add(zone.ToString());
                }
            }

            // 研究
            if (gm.ResearchManager != null)
            {
                foreach (var research in gm.ResearchManager.GetCompletedResearch())
                {
                    data.CompletedResearchIds.Add(research.ResearchId);
                }
                if (gm.ResearchManager.CurrentResearch != null)
                {
                    data.CurrentResearchId = gm.ResearchManager.CurrentResearch.ResearchId;
                    data.CurrentResearchProgress = gm.ResearchManager.CurrentResearch.CurrentProgress;
                }
            }

            // スタッフ
            if (gm.StaffManager != null)
            {
                data.TotalStaff = gm.StaffManager.TotalStaffCount;
                CollectStaffData(data, gm.StaffManager);
            }

            // パーク評価
            if (gm.ParkManager != null && gm.ParkManager.Rating != null)
            {
                data.RatingOverall = gm.ParkManager.Rating.OverallRating;
                data.RatingFame = gm.ParkManager.Rating.GetCategoryScore(CertificateCategory.Fame);
                data.RatingSafety = gm.ParkManager.Rating.GetCategoryScore(CertificateCategory.Safety);
                data.RatingComfort = gm.ParkManager.Rating.GetCategoryScore(CertificateCategory.Comfort);
                data.RatingExcitement = gm.ParkManager.Rating.GetCategoryScore(CertificateCategory.Excitement);
                data.RatingMood = gm.ParkManager.Rating.GetCategoryScore(CertificateCategory.Mood);

                // 認定証
                foreach (var cert in gm.ParkManager.Rating.AwardedCertificates)
                {
                    data.AwardedCertificates.Add(cert.ToString());
                }
            }

            // 天候
            if (gm.WeatherSystem != null)
            {
                data.CurrentWeather = gm.WeatherSystem.CurrentWeather.ToString();
                data.CurrentSeason = gm.WeatherSystem.CurrentSeason.ToString();
            }

            // SNSレピュテーション
            if (gm.AIManager?.SNSSystem != null)
            {
                data.SNSReputation = gm.AIManager.SNSSystem.Reputation;
                data.SNSTotalPosts = gm.AIManager.SNSSystem.Feed.Count;
            }

            // Phase 8-10: アクシデント
            if (AccidentEventSystem.Instance != null)
            {
                data.AccidentResolvedCount = AccidentEventSystem.Instance.ResolvedAccidents;
                data.AccidentTotalCount = AccidentEventSystem.Instance.TotalAccidents;
                data.AccidentHasResolvedCritical = AccidentEventSystem.Instance.HasResolvedCritical;
            }

            // Phase 8-10: 口コミ
            if (AI.WordOfMouthSystem.Instance != null)
            {
                data.WordOfMouthScore = AI.WordOfMouthSystem.Instance.WordOfMouthScore;
            }

            // Phase 8-10: ライバル
            if (Park.RivalParkSystem.Instance != null)
            {
                data.RivalParkCount = Park.RivalParkSystem.Instance.Rivals.Count;
            }

            // Phase 8-10: セール
            if (Economy.SaleCampaignSystem.Instance != null)
            {
                data.SaleTypesUsed = Economy.SaleCampaignSystem.Instance.ActiveSales.Count;
            }

            // Phase 8-10: チャレンジ
            if (ChallengeSystem.Instance != null)
            {
                data.ChallengesCompleted = ChallengeSystem.Instance.TotalChallengesCompleted;
            }

            // 実績用トラッカー
            if (AchievementSystem.Instance != null)
            {
                data.HadLoan = gm.EconomyManager != null && gm.EconomyManager.ActiveLoanCount > 0;
            }

            // 配置済み建物を収集
            CollectPlacedBuildings(data);

            return data;
        }

        /// <summary>
        /// ロード時に復元すべき建物データ。
        /// RuntimeGameSetupがこのデータを参照してサンプル建設をスキップし代わりに復元する。
        /// </summary>
        public static List<SavedBuilding> PendingBuildingsToRestore { get; set; }

        private static void CollectPlacedBuildings(SaveData data)
        {
            // アトラクション
            var attractions = UnityEngine.Object.FindObjectsOfType<ThemeParkGame.Attraction.Attraction>();
            foreach (var attr in attractions)
            {
                if (attr.Data == null) continue;
                var sb = new SavedBuilding
                {
                    Type = "Attraction",
                    DataId = attr.Data.AttractionId ?? attr.DisplayName,
                    PosX = attr.transform.position.x,
                    PosY = attr.transform.position.y,
                    PosZ = attr.transform.position.z,
                    UpgradeLevel = attr.UpgradeLevel,
                    TicketPrice = attr.TicketPrice,
                    Category = attr.Data.Category.ToString(),
                    Excitement = attr.Data.ExcitementRating,
                    NauseaFactor = attr.Data.NauseaFactor,
                    Capacity = attr.Data.Capacity,
                    RideDuration = attr.Data.RideDuration,
                    BuildCost = attr.Data.BuildCost,
                    Zone = "LostKingdom"
                };
                // ゾーン判定
                if (attr.ThemeZone != ThemeZone.LostKingdom)
                    sb.Zone = attr.ThemeZone.ToString();
                data.PlacedBuildings.Add(sb);
            }

            // ショップ
            var shops = UnityEngine.Object.FindObjectsOfType<Shop>();
            foreach (var shop in shops)
            {
                string typeStr;
                switch (shop.ShopType)
                {
                    case ShopType.DrinkShop:    typeStr = "DrinkShop"; break;
                    case ShopType.SouvenirShop: typeStr = "SouvenirShop"; break;
                    default:                    typeStr = "FoodShop"; break;
                }
                var sb = new SavedBuilding
                {
                    Type = typeStr,
                    DataId = shop.DisplayName ?? shop.name,
                    PosX = shop.transform.position.x,
                    PosY = shop.transform.position.y,
                    PosZ = shop.transform.position.z,
                    WholesalePrice = shop.WholesalePrice,
                    SellingPrice = shop.SellingPrice,
                    Stock = shop.MaxStock,
                    Zone = "LostKingdom"
                };
                if (shop.ThemeZone != ThemeZone.LostKingdom)
                    sb.Zone = shop.ThemeZone.ToString();
                data.PlacedBuildings.Add(sb);
            }

            // 簡易施設（トイレ・ベンチ等）: タグで判別
            string[] facilityTags = { "Toilet", "Bench" };
            foreach (var tag in facilityTags)
            {
                GameObject[] tagged;
                try { tagged = GameObject.FindGameObjectsWithTag(tag); }
                catch { continue; }

                foreach (var go in tagged)
                {
                    data.PlacedBuildings.Add(new SavedBuilding
                    {
                        Type = tag,
                        DataId = go.name,
                        PosX = go.transform.position.x,
                        PosY = go.transform.position.y,
                        PosZ = go.transform.position.z,
                        Zone = "LostKingdom"
                    });
                }
            }

            WebGLOptimizer.LogVerbose($"[SaveSystem] 建物データ収集: {data.PlacedBuildings.Count}件");
        }

        /// <summary>スタッフデータを収集する</summary>
        private static void CollectStaffData(SaveData data, StaffManager sm)
        {
            foreach (var staff in sm.GetAllStaff())
            {
                if (staff == null) continue;
                var ss = new SavedStaff
                {
                    Type = staff.StaffType.ToString(),
                    Name = staff.Name,
                    SkillLevel = staff.SkillLevel,
                    Fatigue = staff.Fatigue,
                    Salary = staff.Salary,
                    WorkExperience = staff.WorkExperience,
                    PosX = staff.transform.position.x,
                    PosY = staff.transform.position.y,
                    PosZ = staff.transform.position.z
                };
                if (staff.HasPatrolArea)
                {
                    var bounds = staff.PatrolArea;
                    ss.PatrolCenterX = bounds.center.x;
                    ss.PatrolCenterY = bounds.center.y;
                    ss.PatrolCenterZ = bounds.center.z;
                    ss.PatrolSizeX = bounds.size.x;
                    ss.PatrolSizeY = bounds.size.y;
                    ss.PatrolSizeZ = bounds.size.z;
                }
                data.StaffMembers.Add(ss);
            }
            WebGLOptimizer.LogVerbose($"[SaveSystem] スタッフデータ収集: {data.StaffMembers.Count}名");

            // パーク拡張データ
            if (Park.ParkExpansionSystem.Instance != null)
            {
                data.PurchasedLandPlots = Park.ParkExpansionSystem.Instance.GetPurchasedPlotIds();
            }
        }

        /// <summary>
        /// ロード時に復元すべきスタッフデータ。
        /// RuntimeGameSetupがこのデータを参照してデフォルトスタッフ配置をスキップする。
        /// </summary>
        public static List<SavedStaff> PendingStaffToRestore { get; set; }

        // ================================================================
        // データ復元
        // ================================================================

        private static void ApplySaveData(SaveData data)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 難易度の復元
            if (!string.IsNullOrEmpty(data.Difficulty) &&
                Enum.TryParse<GameDifficulty>(data.Difficulty, out var difficulty))
            {
                gm.RestoreDifficulty(difficulty);
            }

            // ゴールデンチケット
            gm.RestoreGoldenTickets(data.GoldenTickets);

            // 経済データの復元
            if (gm.EconomyManager != null)
            {
                gm.EconomyManager.Initialize((int)data.CurrentBalance);

                // 入場料復元
                if (gm.EconomyManager.Pricing != null)
                    gm.EconomyManager.Pricing.SetEntranceFee(data.EntranceFee);

                // ローン復元
                if (data.ActiveLoans != null && data.ActiveLoans.Count > 0)
                {
                    gm.EconomyManager.RestoreLoans(data.ActiveLoans);
                }
            }

            // 時間の復元
            if (gm.TimeManager != null)
            {
                gm.TimeManager.RestoreTime(
                    data.CurrentYear, data.CurrentMonth,
                    data.CurrentDay, data.CurrentHour);
            }

            // パーク初期化（デフォルトゾーンで）
            if (gm.ParkManager != null)
            {
                gm.ParkManager.Initialize(ThemeZone.LostKingdom);

                // パーク評価と認定証を復元
                if (gm.ParkManager.Rating != null)
                {
                    gm.ParkManager.Rating.RestoreFromSave(
                        data.RatingFame, data.RatingSafety,
                        data.RatingComfort, data.RatingExcitement, data.RatingMood,
                        data.AwardedCertificates);
                }
            }

            // 来場者・スタッフ初期化
            if (gm.VisitorManager != null)
            {
                gm.VisitorManager.Initialize();

                // ライフサイクル統計を復元
                gm.VisitorManager.RestoreLifecycleStats(
                    data.LifecycleWaitingTime,
                    data.LifecycleEnjoyingTime,
                    data.LifecycleLeavingTime,
                    data.LifecycleExperienceStarts,
                    data.LifecycleExperienceCompletions);
            }
            if (gm.StaffManager != null)
                gm.StaffManager.Initialize();

            // 研究データの復元
            if (gm.ResearchManager != null)
            {
                gm.ResearchManager.Initialize();
                foreach (string researchId in data.CompletedResearchIds)
                {
                    gm.ResearchManager.DebugCompleteResearch(researchId);
                }
                if (!string.IsNullOrEmpty(data.CurrentResearchId))
                {
                    gm.ResearchManager.StartResearch(data.CurrentResearchId);
                }
            }

            // 天候の復元
            if (gm.WeatherSystem != null)
            {
                gm.WeatherSystem.Initialize();
                if (!string.IsNullOrEmpty(data.CurrentWeather) &&
                    Enum.TryParse<Weather>(data.CurrentWeather, out Weather weather))
                {
                    gm.WeatherSystem.ForceWeather(weather);
                }
            }

            // SNSレピュテーション復元
            if (gm.AIManager?.SNSSystem != null && data.SNSReputation > 0f)
            {
                gm.AIManager.SNSSystem.RestoreReputation(data.SNSReputation);
            }

            // 建物データをRuntimeGameSetup用にセット（Playing遷移前に必要）
            if (data.PlacedBuildings != null && data.PlacedBuildings.Count > 0)
            {
                PendingBuildingsToRestore = data.PlacedBuildings;
                WebGLOptimizer.LogVerbose($"[SaveSystem] 建物復元データ準備: {data.PlacedBuildings.Count}件");
            }
            else
            {
                PendingBuildingsToRestore = null;
            }

            // スタッフデータをRuntimeGameSetup用にセット
            if (data.StaffMembers != null && data.StaffMembers.Count > 0)
            {
                PendingStaffToRestore = data.StaffMembers;
                WebGLOptimizer.LogVerbose($"[SaveSystem] スタッフ復元データ準備: {data.StaffMembers.Count}名");
            }
            else
            {
                PendingStaffToRestore = null;
            }

            // パーク拡張データを復元
            if (data.PurchasedLandPlots != null && data.PurchasedLandPlots.Count > 0)
            {
                if (Park.ParkExpansionSystem.Instance != null)
                {
                    Park.ParkExpansionSystem.Instance.RestorePurchasedPlots(data.PurchasedLandPlots);
                }
            }

            // Phase 8-10データ復元
            // AccidentEventSystem, WordOfMouthSystem, RivalParkSystem, SaleCampaignSystem は
            // RuntimeInitialize で自動初期化されるため、ここでは統計値のみ復元する
            // (各システムのRestore系メソッドが存在する場合のみ)
            WebGLOptimizer.LogVerbose($"[SaveSystem] Phase 8-10 データ: " +
                $"Accidents={data.AccidentResolvedCount}/{data.AccidentTotalCount}, " +
                $"WoM={data.WordOfMouthScore:F1}, " +
                $"Rivals={data.RivalParkCount}, " +
                $"Challenges={data.ChallengesCompleted}");

            // ゲーム状態をPlayingに遷移
            gm.RestorePlayingState();

            WebGLOptimizer.LogVerbose($"[SaveSystem] Game state restored " +
                      $"(Year {data.CurrentYear} M{data.CurrentMonth} D{data.CurrentDay}, " +
                      $"Balance: ${data.CurrentBalance:N0}, " +
                      $"Difficulty: {data.Difficulty})");
        }

        // ================================================================
        // セーブスロット概要テキスト
        // ================================================================

        /// <summary>スロットの概要テキストを返す</summary>
        public static string GetSlotSummary(int slot)
        {
            var info = GetSaveInfo(slot);
            if (info == null) return "--- EMPTY ---";

            string enjoyPct = info.LifecycleEnjoymentRatio > 0f
                ? $"  Enjoy:{info.LifecycleEnjoymentRatio * 100f:F0}%"
                : "";
            string stars = info.RatingOverall > 0f
                ? $"  [{Park.ParkRatingEvaluator.StarsToText(Park.ParkRatingEvaluator.ScoreToStars(info.RatingOverall))}]"
                : "";
            string snsRep = info.SNSReputation > 0f
                ? $"  SNS:{info.SNSReputation:F0}"
                : "";
            return $"Y{info.CurrentYear} M{info.CurrentMonth} D{info.CurrentDay}  " +
                   $"${info.CurrentBalance:N0}  " +
                   $"Ticket:{info.GoldenTickets}{stars}{enjoyPct}{snsRep}  " +
                   $"{info.SaveDate}";
        }

        // ================================================================
        // セーブデータマイグレーション
        // ================================================================

        private static readonly string CURRENT_SAVE_VERSION = "4.0";

        /// <summary>古いバージョンのセーブデータを現在のバージョンに変換する</summary>
        private static SaveData MigrateSaveData(SaveData data)
        {
            if (data == null) return data;

            string version = data.SaveVersion ?? "1.0";

            // v2.3以前 → v3.0: Phase 8-10フィールドのデフォルト値を設定
            if (string.Compare(version, "3.0") < 0)
            {
                // 新規フィールドはJsonUtilityによりデフォルト値(0/false/null)で初期化済み
                // 追加のマイグレーションロジックが必要な場合はここに記述
                WebGLOptimizer.LogVerbose($"[SaveSystem] セーブデータを v{version} → v3.0 にマイグレーション");
                version = "3.0";
            }

            // v3.0 → v4.0: ストレージをPlayerPrefsからIndexedDB+localStorageに移行
            if (string.Compare(version, "4.0") < 0)
            {
                // データ形式自体は変更なし（JSON構造は同一）
                // ストレージバックエンドの切り替えのみ
                // InitializeMigration() で旧PlayerPrefsデータの自動移行を処理済み
                WebGLOptimizer.LogVerbose($"[SaveSystem] セーブデータを v{version} → v{CURRENT_SAVE_VERSION} にマイグレーション（ストレージ移行）");
            }

            data.SaveVersion = CURRENT_SAVE_VERSION;
            return data;
        }

        /// <summary>
        /// ストレージ移行の初期化。旧PlayerPrefsに残存するセーブデータを検出し、
        /// WebGLStorageHelper（IndexedDB+localStorage）に自動移行する。
        /// ゲーム起動時に一度だけ呼び出す。
        /// </summary>
        public static void InitializeMigration()
        {
            bool migrated = false;

            // 各セーブスロットの旧データを移行
            for (int i = 0; i < MAX_SAVE_SLOTS; i++)
            {
                string key = SAVE_KEY_PREFIX + i;
                // PlayerPrefsに直接残っているデータを確認（WebGLStorageHelper経由ではなく直接）
                if (PlayerPrefs.HasKey(key))
                {
                    string json = PlayerPrefs.GetString(key);
                    if (!string.IsNullOrEmpty(json))
                    {
                        // 新ストレージに書き込み
                        WebGLStorageHelper.SetItem(key, json);
                        // 旧PlayerPrefsから削除
                        PlayerPrefs.DeleteKey(key);
                        migrated = true;
                        WebGLOptimizer.LogVerbose($"[SaveSystem] スロット {i} のデータをPlayerPrefsから新ストレージに移行");
                    }
                }
            }

            // オートセーブの旧データも移行
            if (PlayerPrefs.HasKey(AUTOSAVE_KEY))
            {
                string json = PlayerPrefs.GetString(AUTOSAVE_KEY);
                if (!string.IsNullOrEmpty(json))
                {
                    WebGLStorageHelper.SetItem(AUTOSAVE_KEY, json);
                    PlayerPrefs.DeleteKey(AUTOSAVE_KEY);
                    migrated = true;
                    WebGLOptimizer.LogVerbose("[SaveSystem] オートセーブデータをPlayerPrefsから新ストレージに移行");
                }
            }

            if (migrated)
            {
                PlayerPrefs.Save();
                WebGLOptimizer.LogVerbose("[SaveSystem] PlayerPrefsからのデータ移行完了");
            }
            else
            {
                WebGLOptimizer.LogVerbose("[SaveSystem] 移行対象のPlayerPrefsデータなし");
            }
        }

        /// <summary>セーブデータのバリデーション。破損データを検出する。</summary>
        private static bool ValidateSaveData(SaveData data)
        {
            if (data == null) return false;
            if (data.CurrentYear < 0 || data.CurrentYear > 100) return false;
            if (data.CurrentMonth < 1 || data.CurrentMonth > 12) return false;
            if (data.CurrentDay < 1 || data.CurrentDay > 31) return false;
            if (float.IsNaN(data.CurrentBalance) || float.IsInfinity(data.CurrentBalance)) return false;
            return true;
        }

        // ================================================================
        // オートセーブ
        // ================================================================

        /// <summary>
        /// オートセーブを有効化する。GameManager初期化後に一度だけ呼ぶ。
        /// TimeManager.OnMonthChangedに購読して月末に自動保存する。
        /// </summary>
        public static void EnableAutoSave()
        {
            if (_autoSaveSubscribed) return;
            if (GameManager.Instance == null || GameManager.Instance.TimeManager == null) return;

            GameManager.Instance.TimeManager.OnMonthChanged += PerformAutoSave;
            _autoSaveSubscribed = true;
            WebGLOptimizer.LogVerbose("[SaveSystem] オートセーブ有効化");
        }

        /// <summary>オートセーブの購読を解除する</summary>
        public static void DisableAutoSave()
        {
            if (!_autoSaveSubscribed) return;
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
                GameManager.Instance.TimeManager.OnMonthChanged -= PerformAutoSave;
            _autoSaveSubscribed = false;
        }

        /// <summary>月末オートセーブ実行</summary>
        private static void PerformAutoSave()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            try
            {
                SaveData data = CollectSaveData();
                string json = JsonUtility.ToJson(data);
                WebGLStorageHelper.SetItem(AUTOSAVE_KEY, json);

                // 通知表示
                if (NotificationSystem.Instance != null)
                    NotificationSystem.Instance.Notify("オートセーブ完了", NotifLevel.Info);

                WebGLOptimizer.LogVerbose($"[SaveSystem] AutoSave completed ({json.Length} bytes)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] AutoSave failed: {e.Message}");
            }
        }

        /// <summary>オートセーブデータが存在するか</summary>
        public static bool HasAutoSave()
        {
            return WebGLStorageHelper.HasKey(AUTOSAVE_KEY);
        }

        /// <summary>オートセーブデータの概要を取得する</summary>
        public static string GetAutoSaveSummary()
        {
            if (!HasAutoSave()) return "--- NO AUTOSAVE ---";

            try
            {
                string json = WebGLStorageHelper.GetItem(AUTOSAVE_KEY);
                var info = JsonUtility.FromJson<SaveData>(json);
                return $"[AUTO] Y{info.CurrentYear} M{info.CurrentMonth} D{info.CurrentDay}  " +
                       $"${info.CurrentBalance:N0}  {info.SaveDate}";
            }
            catch { return "--- AUTOSAVE CORRUPTED ---"; }
        }

        /// <summary>オートセーブデータをロードする</summary>
        public static bool LoadAutoSave()
        {
            if (!HasAutoSave()) return false;

            try
            {
                string json = WebGLStorageHelper.GetItem(AUTOSAVE_KEY);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                data = MigrateSaveData(data);

                if (!ValidateSaveData(data))
                {
                    Debug.LogError("[SaveSystem] AutoSave data is corrupted");
                    return false;
                }

                ApplySaveData(data);
                GameEvents.FireGameLoaded();
                WebGLOptimizer.LogVerbose("[SaveSystem] AutoSave loaded");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] AutoSave load failed: {e.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// WebGL環境向けストレージヘルパー。
    /// WebGLではIndexedDB（localStorageフォールバック付き）を使用し、
    /// 非WebGL環境ではPlayerPrefsにフォールバックする。
    /// Application.ExternalEvalでJavaScript実行するため.jslibファイル不要。
    /// </summary>
    internal static class WebGLStorageHelper
    {
        private static bool IsWebGL => Application.platform == RuntimePlatform.WebGLPlayer;

        // JS↔C#同期ブリッジ用の一時キー（localStorageを経由してExternalEvalの結果をC#に返す）
        private const string BRIDGE_KEY = "_tpg_bridge_tmp";

        // IndexedDB初期化用JavaScriptコード
        // localStorageをsync wrapperとして使用し、バックグラウンドでIndexedDBに永続化する
        private static bool _initialized;

        /// <summary>
        /// WebGL環境でのストレージを初期化する。
        /// localStorageを即時アクセス用に、IndexedDBをバックアップ永続化用に使用する。
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!IsWebGL || _initialized) return;
            _initialized = true;

            // IndexedDB初期化とlocalStorageとの同期ブリッジを構築
            string initScript = @"
                if (!window._tpgStorage) {
                    window._tpgStorage = {
                        dbName: 'ThemeParkGameDB',
                        storeName: 'savedata',
                        db: null,
                        init: function() {
                            var request = indexedDB.open(this.dbName, 1);
                            var self = this;
                            request.onupgradeneeded = function(e) {
                                var db = e.target.result;
                                if (!db.objectStoreNames.contains(self.storeName)) {
                                    db.createObjectStore(self.storeName);
                                }
                            };
                            request.onsuccess = function(e) {
                                self.db = e.target.result;
                                self.syncFromIDB();
                            };
                            request.onerror = function(e) {
                                console.warn('[TPG Storage] IndexedDB init failed, using localStorage only');
                            };
                        },
                        setItem: function(key, value) {
                            try { localStorage.setItem(key, value); } catch(e) {
                                console.warn('[TPG Storage] localStorage setItem failed: ' + e);
                            }
                            this.writeToIDB(key, value);
                        },
                        getItem: function(key) {
                            try { return localStorage.getItem(key) || ''; } catch(e) {
                                console.warn('[TPG Storage] localStorage getItem failed: ' + e);
                                return '';
                            }
                        },
                        deleteItem: function(key) {
                            try { localStorage.removeItem(key); } catch(e) {}
                            this.deleteFromIDB(key);
                        },
                        hasKey: function(key) {
                            try { return localStorage.getItem(key) !== null; } catch(e) { return false; }
                        },
                        writeToIDB: function(key, value) {
                            if (!this.db) return;
                            try {
                                var tx = this.db.transaction(this.storeName, 'readwrite');
                                tx.objectStore(this.storeName).put(value, key);
                            } catch(e) {
                                console.warn('[TPG Storage] IDB write failed: ' + e);
                            }
                        },
                        deleteFromIDB: function(key) {
                            if (!this.db) return;
                            try {
                                var tx = this.db.transaction(this.storeName, 'readwrite');
                                tx.objectStore(this.storeName).delete(key);
                            } catch(e) {}
                        },
                        syncFromIDB: function() {
                            if (!this.db) return;
                            try {
                                var tx = this.db.transaction(this.storeName, 'readonly');
                                var store = tx.objectStore(this.storeName);
                                var request = store.openCursor();
                                request.onsuccess = function(e) {
                                    var cursor = e.target.result;
                                    if (cursor) {
                                        try {
                                            if (localStorage.getItem(cursor.key) === null) {
                                                localStorage.setItem(cursor.key, cursor.value);
                                            }
                                        } catch(ex) {}
                                        cursor.continue();
                                    }
                                };
                            } catch(e) {
                                console.warn('[TPG Storage] IDB sync failed: ' + e);
                            }
                        }
                    };
                    window._tpgStorage.init();
                }
            ";
            Application.ExternalEval(initScript);
            WebGLOptimizer.LogVerbose("[WebGLStorageHelper] IndexedDB ストレージ初期化完了");
        }

        /// <summary>キーに対して値を保存する</summary>
        public static void SetItem(string key, string value)
        {
            if (IsWebGL)
            {
                EnsureInitialized();
                // JavaScriptの文字列エスケープ（シングルクォート、バックスラッシュ、改行）
                string escapedKey = EscapeForJS(key);
                string escapedValue = EscapeForJS(value);
                Application.ExternalEval($"window._tpgStorage.setItem('{escapedKey}', '{escapedValue}');");
                WebGLOptimizer.LogVerbose($"[WebGLStorageHelper] SetItem: {key} ({value.Length} bytes)");
            }
            else
            {
                PlayerPrefs.SetString(key, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>キーに対応する値を取得する。存在しない場合は空文字を返す</summary>
        public static string GetItem(string key)
        {
            if (IsWebGL)
            {
                EnsureInitialized();
                string escapedKey = EscapeForJS(key);
                // ExternalEvalは戻り値を返せないため、localStorageブリッジキー経由で同期取得する
                // Unity WebGLのPlayerPrefsはlocalStorageを内部使用するため同期読み取りが可能
                Application.ExternalEval(
                    $"try {{ localStorage.setItem('{BRIDGE_KEY}', window._tpgStorage.getItem('{escapedKey}')); }} catch(e) {{}}");
                string result = PlayerPrefs.GetString(BRIDGE_KEY, "");
                Application.ExternalEval(
                    $"try {{ localStorage.removeItem('{BRIDGE_KEY}'); }} catch(e) {{}}");
                WebGLOptimizer.LogVerbose($"[WebGLStorageHelper] GetItem: {key} ({result.Length} bytes)");
                return result;
            }
            else
            {
                return PlayerPrefs.GetString(key, "");
            }
        }

        /// <summary>キーに対応するデータを削除する</summary>
        public static void DeleteItem(string key)
        {
            if (IsWebGL)
            {
                EnsureInitialized();
                string escapedKey = EscapeForJS(key);
                Application.ExternalEval($"window._tpgStorage.deleteItem('{escapedKey}');");
                WebGLOptimizer.LogVerbose($"[WebGLStorageHelper] DeleteItem: {key}");
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        /// <summary>キーが存在するかチェックする</summary>
        public static bool HasKey(string key)
        {
            if (IsWebGL)
            {
                EnsureInitialized();
                string escapedKey = EscapeForJS(key);
                // ブリッジキー経由でbool結果を同期取得
                Application.ExternalEval(
                    $"try {{ localStorage.setItem('{BRIDGE_KEY}', window._tpgStorage.hasKey('{escapedKey}') ? '1' : '0'); }} catch(e) {{}}");
                string result = PlayerPrefs.GetString(BRIDGE_KEY, "0");
                Application.ExternalEval(
                    $"try {{ localStorage.removeItem('{BRIDGE_KEY}'); }} catch(e) {{}}");
                return result == "1";
            }
            else
            {
                return PlayerPrefs.HasKey(key);
            }
        }

        /// <summary>JavaScript文字列リテラル用のエスケープ処理</summary>
        private static string EscapeForJS(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("'", "\\'")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r");
        }

    }
}
