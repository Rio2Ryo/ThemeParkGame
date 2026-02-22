// ============================================================
// ThemeParkGame - SaveSystem
// セーブ/ロードシステム（JSON形式でPlayerPrefsに保存）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.Economy;
using ThemeParkGame.Park;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// セーブデータの構造体。ゲーム全体の状態をシリアライズ可能な形式で保持する。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public string SaveVersion = "1.0";
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

        /// <summary>
        /// 現在のゲーム状態をセーブする。
        /// </summary>
        /// <param name="slot">セーブスロット番号（0～2）</param>
        /// <returns>セーブ成功ならtrue</returns>
        public static bool Save(int slot)
        {
            if (slot < 0 || slot >= MAX_SAVE_SLOTS)
            {
                Debug.LogError($"[SaveSystem] 無効なスロット番号: {slot}");
                return false;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogError("[SaveSystem] GameManagerが見つかりません");
                return false;
            }

            try
            {
                SaveData data = CollectSaveData();
                string json = JsonUtility.ToJson(data, true);
                PlayerPrefs.SetString(SAVE_KEY_PREFIX + slot, json);
                PlayerPrefs.Save();

                GameEvents.FireGameSaved();
                Debug.Log($"[SaveSystem] スロット{slot}にセーブ完了");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] セーブ失敗: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// セーブデータをロードする。
        /// </summary>
        /// <param name="slot">セーブスロット番号（0～2）</param>
        /// <returns>ロード成功ならtrue</returns>
        public static bool Load(int slot)
        {
            if (!HasSaveData(slot))
            {
                Debug.LogWarning($"[SaveSystem] スロット{slot}にセーブデータがありません");
                return false;
            }

            try
            {
                string json = PlayerPrefs.GetString(SAVE_KEY_PREFIX + slot);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                ApplySaveData(data);

                GameEvents.FireGameLoaded();
                Debug.Log($"[SaveSystem] スロット{slot}からロード完了");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] ロード失敗: {e.Message}");
                return false;
            }
        }

        /// <summary>セーブデータを削除する</summary>
        public static void DeleteSave(int slot)
        {
            if (slot < 0 || slot >= MAX_SAVE_SLOTS) return;
            PlayerPrefs.DeleteKey(SAVE_KEY_PREFIX + slot);
            PlayerPrefs.Save();
            Debug.Log($"[SaveSystem] スロット{slot}のセーブデータを削除");
        }

        /// <summary>セーブスロットのメタ情報を取得する（セーブ日時等）</summary>
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

        /// <summary>現在のゲーム状態からセーブデータを収集する</summary>
        private static SaveData CollectSaveData()
        {
            var gm = GameManager.Instance;
            var data = new SaveData
            {
                SaveDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                GoldenTickets = gm.GoldenTickets
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

            // パーク
            if (gm.ParkManager != null)
            {
                data.TotalAttractions = gm.ParkManager.Stats.TotalAttractions;
                data.TotalShops = gm.ParkManager.Stats.TotalShops;
                data.TotalVisitorsEver = gm.ParkManager.Stats.TotalVisitorsEver;
                data.IsScenarioMode = gm.ParkManager.IsScenarioMode;

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

            // 天候
            if (gm.WeatherSystem != null)
            {
                data.CurrentWeather = gm.WeatherSystem.CurrentWeather.ToString();
                data.CurrentSeason = gm.WeatherSystem.CurrentSeason.ToString();
            }

            return data;
        }

        /// <summary>セーブデータからゲーム状態を復元する</summary>
        private static void ApplySaveData(SaveData data)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 経済データの復元
            if (gm.EconomyManager != null)
            {
                gm.EconomyManager.Initialize((int)data.CurrentBalance);
            }

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
            if (gm.WeatherSystem != null && !string.IsNullOrEmpty(data.CurrentWeather))
            {
                if (Enum.TryParse<Weather>(data.CurrentWeather, out Weather weather))
                {
                    gm.WeatherSystem.ForceWeather(weather);
                }
            }

            Debug.Log($"[SaveSystem] ゲーム状態を復元しました (Year {data.CurrentYear}, 残高: {data.CurrentBalance:F0})");
        }
    }
}
