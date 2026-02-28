// ============================================================
// ThemeParkGame - SaveSystem PlayMode Tests
// SaveSystem / SaveData の公開APIに対するPlayModeテスト
// ============================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ThemeParkGame.Core;

namespace ThemeParkGame.Tests
{
    /// <summary>
    /// SaveSystem の PlayMode テスト。
    /// 静的メソッドおよび SaveData 構造体のテストを行う。
    /// WebGLStorageHelper 依存のメソッドはストレージ操作が可能な範囲でテストする。
    /// </summary>
    [TestFixture]
    public class SaveSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            // テスト用スロットのクリーンアップ
            for (int i = 0; i < SaveSystem.MaxSlots; i++)
            {
                SaveSystem.DeleteSave(i);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // テスト後のクリーンアップ
            for (int i = 0; i < SaveSystem.MaxSlots; i++)
            {
                SaveSystem.DeleteSave(i);
            }
        }

        // ================================================================
        // GetSaveKey テスト
        // ================================================================

        /// <summary>
        /// GetSaveKey がスロット番号を含む正しいキー文字列を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetSaveKey_ReturnsCorrectKeyFormat()
        {
            yield return null;

            string key0 = SaveSystem.GetSaveKey(0);
            string key1 = SaveSystem.GetSaveKey(1);
            string key2 = SaveSystem.GetSaveKey(2);

            Assert.AreEqual("ThemeParkGame_Save_0", key0,
                "GetSaveKey(0) は 'ThemeParkGame_Save_0' を返すべき");
            Assert.AreEqual("ThemeParkGame_Save_1", key1,
                "GetSaveKey(1) は 'ThemeParkGame_Save_1' を返すべき");
            Assert.AreEqual("ThemeParkGame_Save_2", key2,
                "GetSaveKey(2) は 'ThemeParkGame_Save_2' を返すべき");
        }

        /// <summary>
        /// GetSaveKey がスロットごとに異なるキーを返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetSaveKey_ReturnsUniqueKeysPerSlot()
        {
            yield return null;

            string key0 = SaveSystem.GetSaveKey(0);
            string key1 = SaveSystem.GetSaveKey(1);
            string key2 = SaveSystem.GetSaveKey(2);

            Assert.AreNotEqual(key0, key1, "スロット0と1のキーは異なるべき");
            Assert.AreNotEqual(key1, key2, "スロット1と2のキーは異なるべき");
            Assert.AreNotEqual(key0, key2, "スロット0と2のキーは異なるべき");
        }

        // ================================================================
        // MaxSlots テスト
        // ================================================================

        /// <summary>
        /// MaxSlots が正の値（3）を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator MaxSlots_ReturnsThree()
        {
            yield return null;

            Assert.AreEqual(3, SaveSystem.MaxSlots,
                "MaxSlots は 3 であるべき");
            Assert.Greater(SaveSystem.MaxSlots, 0,
                "MaxSlots は正の値であるべき");
        }

        // ================================================================
        // HasSaveData テスト
        // ================================================================

        /// <summary>
        /// 空のスロットに対して HasSaveData が false を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator HasSaveData_ReturnsFalseForEmptySlot()
        {
            yield return null;

            Assert.IsFalse(SaveSystem.HasSaveData(0),
                "データがないスロットでは HasSaveData は false を返すべき");
        }

        /// <summary>
        /// 範囲外のスロット番号に対して HasSaveData が false を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator HasSaveData_ReturnsFalseForInvalidSlot()
        {
            yield return null;

            Assert.IsFalse(SaveSystem.HasSaveData(-1),
                "負のスロットでは false を返すべき");
            Assert.IsFalse(SaveSystem.HasSaveData(999),
                "範囲外のスロットでは false を返すべき");
        }

        /// <summary>
        /// 全スロットが空の場合、HasAnySaveData が false を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator HasAnySaveData_ReturnsFalseWhenEmpty()
        {
            yield return null;

            Assert.IsFalse(SaveSystem.HasAnySaveData(),
                "全スロットが空の場合、HasAnySaveData は false を返すべき");
        }

        // ================================================================
        // SaveData 構造体テスト
        // ================================================================

        /// <summary>
        /// SaveData のデフォルトコンストラクタでフィールドが正しく初期化されること。
        /// </summary>
        [UnityTest]
        public IEnumerator SaveData_DefaultValues_AreCorrect()
        {
            yield return null;

            var data = new SaveData();

            Assert.AreEqual("4.0", data.SaveVersion,
                "SaveVersion のデフォルトは '4.0' であるべき");
            Assert.AreEqual(15f, data.EntranceFee,
                "EntranceFee のデフォルトは 15 であるべき");
            Assert.IsNotNull(data.UnlockedZones,
                "UnlockedZones はnullでないべき");
            Assert.IsNotNull(data.CompletedResearchIds,
                "CompletedResearchIds はnullでないべき");
            Assert.IsNotNull(data.ActiveLoans,
                "ActiveLoans はnullでないべき");
            Assert.IsNotNull(data.PlacedBuildings,
                "PlacedBuildings はnullでないべき");
            Assert.IsNotNull(data.StaffMembers,
                "StaffMembers はnullでないべき");
        }

        /// <summary>
        /// SaveData がJSON形式で正しくシリアライズ/デシリアライズできること。
        /// </summary>
        [UnityTest]
        public IEnumerator SaveData_JsonSerialization_RoundTrip()
        {
            yield return null;

            var original = new SaveData
            {
                CurrentYear = 3,
                CurrentMonth = 6,
                CurrentDay = 15,
                CurrentBalance = 75000f,
                GoldenTickets = 2,
                TotalAttractions = 10,
                Difficulty = "Normal",
                AverageHappiness = 72.5f,
                EntranceFee = 25f
            };

            string json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(original.CurrentYear, restored.CurrentYear,
                "Year がラウンドトリップ後に一致するべき");
            Assert.AreEqual(original.CurrentMonth, restored.CurrentMonth,
                "Month がラウンドトリップ後に一致するべき");
            Assert.AreEqual(original.CurrentBalance, restored.CurrentBalance, 0.01f,
                "Balance がラウンドトリップ後に一致するべき");
            Assert.AreEqual(original.GoldenTickets, restored.GoldenTickets,
                "GoldenTickets がラウンドトリップ後に一致するべき");
            Assert.AreEqual(original.Difficulty, restored.Difficulty,
                "Difficulty がラウンドトリップ後に一致するべき");
            Assert.AreEqual(original.EntranceFee, restored.EntranceFee, 0.01f,
                "EntranceFee がラウンドトリップ後に一致するべき");
        }

        /// <summary>
        /// SaveData のリストフィールドがJSON化後も空リストとして復元されること。
        /// </summary>
        [UnityTest]
        public IEnumerator SaveData_ListFields_SurviveSerialization()
        {
            yield return null;

            var data = new SaveData();
            data.UnlockedZones.Add("LostKingdom");
            data.UnlockedZones.Add("SpaceZone");
            data.CompletedResearchIds.Add("research_01");

            string json = JsonUtility.ToJson(data);
            var restored = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(2, restored.UnlockedZones.Count,
                "UnlockedZones のカウントが保存後に一致するべき");
            Assert.AreEqual("LostKingdom", restored.UnlockedZones[0],
                "最初のゾーンが正しく復元されるべき");
            Assert.AreEqual(1, restored.CompletedResearchIds.Count,
                "CompletedResearchIds のカウントが保存後に一致するべき");
        }

        /// <summary>
        /// SavedBuilding がJSON化後に正しく復元されること。
        /// </summary>
        [UnityTest]
        public IEnumerator SavedBuilding_JsonSerialization_RoundTrip()
        {
            yield return null;

            var building = new SavedBuilding
            {
                Type = "Attraction",
                DataId = "roller_coaster_01",
                PosX = 10.5f,
                PosY = 0f,
                PosZ = -5.2f,
                UpgradeLevel = 2,
                TicketPrice = 30,
                Excitement = 85f,
                Capacity = 24
            };

            string json = JsonUtility.ToJson(building);
            var restored = JsonUtility.FromJson<SavedBuilding>(json);

            Assert.AreEqual("Attraction", restored.Type,
                "Type がラウンドトリップ後に一致するべき");
            Assert.AreEqual("roller_coaster_01", restored.DataId,
                "DataId がラウンドトリップ後に一致するべき");
            Assert.AreEqual(10.5f, restored.PosX, 0.01f,
                "PosX がラウンドトリップ後に一致するべき");
            Assert.AreEqual(2, restored.UpgradeLevel,
                "UpgradeLevel がラウンドトリップ後に一致するべき");
            Assert.AreEqual(24, restored.Capacity,
                "Capacity がラウンドトリップ後に一致するべき");
        }

        /// <summary>
        /// GetSaveInfo がデータのないスロットに対してnullを返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetSaveInfo_ReturnsNullForEmptySlot()
        {
            yield return null;

            var info = SaveSystem.GetSaveInfo(0);
            Assert.IsNull(info,
                "データがないスロットの GetSaveInfo は null を返すべき");
        }

        /// <summary>
        /// Save が無効なスロットに対して false を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator Save_ReturnsFalseForInvalidSlot()
        {
            yield return null;

            bool result = SaveSystem.Save(-1);
            Assert.IsFalse(result, "負のスロットへの Save は false を返すべき");

            result = SaveSystem.Save(100);
            Assert.IsFalse(result, "範囲外のスロットへの Save は false を返すべき");
        }
    }
}
