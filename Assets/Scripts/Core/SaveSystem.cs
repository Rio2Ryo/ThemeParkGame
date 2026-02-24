// ============================================================
// ThemeParkGame - SaveSystem
// セーブ/ロードシステム（JSON形式でPlayerPrefsに保存）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// セーブデータの構造体。ゲーム全体の状態をシリアライズ可能な形式で保持する。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public string SaveVersion = "2.1";
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

        // ライフサイクル統計（累積）
        public float LifecycleWaitingTime;
        public float LifecycleEnjoyingTime;
        public float LifecycleLeavingTime;
        public int LifecycleExperienceStarts;
        public int LifecycleExperienceCompletions;
        public float LifecycleEnjoymentRatio;
    }

    /// <summary>
    /// セーブ/ロードを管理するシステム。
    /// PlayerPrefsを使用してJSONシリアライズされたデータを保存する。
    /// 最大3つのセーブスロットをサポート。
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        private const string SAVE_KEY_PREFIX = "ThemeParkGame_Save_";
        private const int MAX_SAVE_SLOTS = 3;

        /// <summary>セーブスロットにデータが存在するか確認する</summary>
        public static bool HasSaveData(int slot)
        {
            if (slot < 0 || slot >= MAX_SAVE_SLOTS) return false;
            return PlayerPrefs.HasKey(SAVE_KEY_PREFIX + slot);
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
                PlayerPrefs.SetString(SAVE_KEY_PREFIX + slot, json);
                PlayerPrefs.Save();

                GameEvents.FireGameSaved();
                Debug.Log($"[SaveSystem] Saved to slot {slot} ({json.Length} bytes)");
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
                string json = PlayerPrefs.GetString(SAVE_KEY_PREFIX + slot);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                ApplySaveData(data);

                GameEvents.FireGameLoaded();
                Debug.Log($"[SaveSystem] Loaded from slot {slot} " +
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
            PlayerPrefs.DeleteKey(SAVE_KEY_PREFIX + slot);
            PlayerPrefs.Save();
            Debug.Log($"[SaveSystem] Deleted slot {slot}");
        }

        /// <summary>セーブスロットのメタ情報を取得する</summary>
        public static SaveData GetSaveInfo(int slot)
        {
            if (!HasSaveData(slot)) return null;

            try
            {
                string json = PlayerPrefs.GetString(SAVE_KEY_PREFIX + slot);
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
            }

            // 天候
            if (gm.WeatherSystem != null)
            {
                data.CurrentWeather = gm.WeatherSystem.CurrentWeather.ToString();
                data.CurrentSeason = gm.WeatherSystem.CurrentSeason.ToString();
            }

            return data;
        }

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

            // ゲーム状態をPlayingに遷移
            gm.RestorePlayingState();

            Debug.Log($"[SaveSystem] Game state restored " +
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
            return $"Y{info.CurrentYear} M{info.CurrentMonth} D{info.CurrentDay}  " +
                   $"${info.CurrentBalance:N0}  " +
                   $"Ticket:{info.GoldenTickets}{enjoyPct}  " +
                   $"{info.SaveDate}";
        }
    }
}
