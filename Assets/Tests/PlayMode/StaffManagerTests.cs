// ============================================================
// ThemeParkGame - StaffManager PlayMode Tests
// StaffManager の公開APIに対するPlayModeテスト
// ============================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ThemeParkGame.Core;
using ThemeParkGame.Staff;

namespace ThemeParkGame.Tests
{
    /// <summary>
    /// StaffManager の PlayMode テスト。
    /// MonoBehaviour であるため new GameObject() + AddComponent で生成し、
    /// TearDown で DestroyImmediate によるクリーンアップを行う。
    /// </summary>
    [TestFixture]
    public class StaffManagerTests
    {
        private GameObject _gameObject;
        private StaffManager _staffManager;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("StaffManager_Test");
            _staffManager = _gameObject.AddComponent<StaffManager>();
            _staffManager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
            _gameObject = null;
            _staffManager = null;
        }

        // ================================================================
        // 初期化テスト
        // ================================================================

        /// <summary>
        /// Initialize 後の TotalStaffCount が 0 であること。
        /// </summary>
        [UnityTest]
        public IEnumerator TotalStaffCount_InitiallyZero()
        {
            yield return null;

            Assert.AreEqual(0, _staffManager.TotalStaffCount,
                "Initialize 直後の TotalStaffCount は 0 であるべき");
        }

        /// <summary>
        /// Initialize 後の StrikingStaffCount が 0 であること。
        /// </summary>
        [UnityTest]
        public IEnumerator StrikingStaffCount_InitiallyZero()
        {
            yield return null;

            Assert.AreEqual(0, _staffManager.StrikingStaffCount,
                "Initialize 直後の StrikingStaffCount は 0 であるべき");
        }

        /// <summary>
        /// Initialize 後の TotalMonthlySalary が 0 であること。
        /// </summary>
        [UnityTest]
        public IEnumerator TotalMonthlySalary_InitiallyZero()
        {
            yield return null;

            Assert.AreEqual(0f, _staffManager.TotalMonthlySalary,
                "Initialize 直後の TotalMonthlySalary は 0 であるべき");
        }

        // ================================================================
        // 雇用コスト・月給テスト
        // ================================================================

        /// <summary>
        /// GetHiringCost が全スタッフタイプに対して正の値を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetHiringCost_ReturnsPositiveForAllTypes()
        {
            yield return null;

            Assert.AreEqual(800f, _staffManager.GetHiringCost(StaffType.Mechanic),
                "Mechanic の雇用コストは 800 であるべき");
            Assert.AreEqual(500f, _staffManager.GetHiringCost(StaffType.Cleaner),
                "Cleaner の雇用コストは 500 であるべき");
            Assert.AreEqual(600f, _staffManager.GetHiringCost(StaffType.Entertainer),
                "Entertainer の雇用コストは 600 であるべき");
            Assert.AreEqual(700f, _staffManager.GetHiringCost(StaffType.Guard),
                "Guard の雇用コストは 700 であるべき");
            Assert.AreEqual(1000f, _staffManager.GetHiringCost(StaffType.Scientist),
                "Scientist の雇用コストは 1000 であるべき");
            Assert.AreEqual(900f, _staffManager.GetHiringCost(StaffType.Doctor),
                "Doctor の雇用コストは 900 であるべき");
            Assert.AreEqual(550f, _staffManager.GetHiringCost(StaffType.Vendor),
                "Vendor の雇用コストは 550 であるべき");
            Assert.AreEqual(450f, _staffManager.GetHiringCost(StaffType.Gardener),
                "Gardener の雇用コストは 450 であるべき");
        }

        /// <summary>
        /// GetBaseSalary が全スタッフタイプに対して正の値を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetBaseSalary_ReturnsPositiveForAllTypes()
        {
            yield return null;

            Assert.AreEqual(400f, _staffManager.GetBaseSalary(StaffType.Mechanic),
                "Mechanic の月給は 400 であるべき");
            Assert.AreEqual(300f, _staffManager.GetBaseSalary(StaffType.Cleaner),
                "Cleaner の月給は 300 であるべき");
            Assert.AreEqual(350f, _staffManager.GetBaseSalary(StaffType.Entertainer),
                "Entertainer の月給は 350 であるべき");
            Assert.AreEqual(350f, _staffManager.GetBaseSalary(StaffType.Guard),
                "Guard の月給は 350 であるべき");
            Assert.AreEqual(500f, _staffManager.GetBaseSalary(StaffType.Scientist),
                "Scientist の月給は 500 であるべき");
            Assert.AreEqual(450f, _staffManager.GetBaseSalary(StaffType.Doctor),
                "Doctor の月給は 450 であるべき");
            Assert.AreEqual(280f, _staffManager.GetBaseSalary(StaffType.Vendor),
                "Vendor の月給は 280 であるべき");
            Assert.AreEqual(250f, _staffManager.GetBaseSalary(StaffType.Gardener),
                "Gardener の月給は 250 であるべき");
        }

        // ================================================================
        // スタッフ検索テスト
        // ================================================================

        /// <summary>
        /// GetStaffCountByType が初期状態で全タイプ 0 を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetStaffCountByType_InitiallyZeroForAllTypes()
        {
            yield return null;

            Assert.AreEqual(0, _staffManager.GetStaffCountByType(StaffType.Mechanic),
                "Mechanic のカウントは 0 であるべき");
            Assert.AreEqual(0, _staffManager.GetStaffCountByType(StaffType.Cleaner),
                "Cleaner のカウントは 0 であるべき");
            Assert.AreEqual(0, _staffManager.GetStaffCountByType(StaffType.Guard),
                "Guard のカウントは 0 であるべき");
            Assert.AreEqual(0, _staffManager.GetStaffCountByType(StaffType.Doctor),
                "Doctor のカウントは 0 であるべき");
        }

        /// <summary>
        /// HasStaffOfType が初期状態で全タイプ false を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator HasStaffOfType_InitiallyFalseForAllTypes()
        {
            yield return null;

            Assert.IsFalse(_staffManager.HasStaffOfType(StaffType.Mechanic),
                "Initialize 直後の HasStaffOfType(Mechanic) は false であるべき");
            Assert.IsFalse(_staffManager.HasStaffOfType(StaffType.Cleaner),
                "Initialize 直後の HasStaffOfType(Cleaner) は false であるべき");
        }

        /// <summary>
        /// GetStaffById が存在しないIDに対して null を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetStaffById_ReturnsNullForNonexistentId()
        {
            yield return null;

            Assert.IsNull(_staffManager.GetStaffById(9999),
                "存在しないIDでは null を返すべき");
        }

        /// <summary>
        /// GetStaffByType が初期状態で空リストを返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetStaffByType_ReturnsEmptyListInitially()
        {
            yield return null;

            var list = _staffManager.GetStaffByType(StaffType.Mechanic);
            Assert.IsNotNull(list, "GetStaffByType は null ではなく空リストを返すべき");
            Assert.AreEqual(0, list.Count, "初期状態のリストは空であるべき");
        }

        /// <summary>
        /// GetAllStaff が初期状態で空コレクションを返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetAllStaff_ReturnsEmptyCollectionInitially()
        {
            yield return null;

            var all = _staffManager.GetAllStaff();
            Assert.IsNotNull(all, "GetAllStaff は null ではなく空コレクションを返すべき");
            Assert.AreEqual(0, all.Count, "初期状態のコレクションは空であるべき");
        }

        // ================================================================
        // 統計テスト
        // ================================================================

        /// <summary>
        /// CalculateStaffSatisfaction がスタッフ0人の場合に 100 を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator CalculateStaffSatisfaction_Returns100WhenNoStaff()
        {
            yield return null;

            Assert.AreEqual(100f, _staffManager.CalculateStaffSatisfaction(),
                "スタッフ0人の場合、満足度は 100 であるべき");
        }

        /// <summary>
        /// GetTypeSummary が初期状態で正しいサマリーを返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetTypeSummary_ReturnsEmptySummaryInitially()
        {
            yield return null;

            var summary = _staffManager.GetTypeSummary(StaffType.Mechanic);

            Assert.AreEqual(StaffType.Mechanic, summary.Type,
                "サマリーの Type は Mechanic であるべき");
            Assert.AreEqual(0, summary.Count,
                "初期状態のサマリー Count は 0 であるべき");
            Assert.AreEqual(0f, summary.AverageSkillLevel,
                "初期状態の AverageSkillLevel は 0 であるべき");
            Assert.AreEqual(0f, summary.AverageFatigue,
                "初期状態の AverageFatigue は 0 であるべき");
            Assert.AreEqual(0, summary.StrikingCount,
                "初期状態の StrikingCount は 0 であるべき");
            Assert.AreEqual(0f, summary.TotalMonthlySalary,
                "初期状態の TotalMonthlySalary は 0 であるべき");
        }

        /// <summary>
        /// GetAverageMechanicSkill がメカニック0人の場合に 1 を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetAverageMechanicSkill_ReturnsOneWhenNoMechanics()
        {
            yield return null;

            Assert.AreEqual(1f, _staffManager.GetAverageMechanicSkill(),
                "メカニック0人の場合、平均スキルは 1 であるべき");
        }

        // ================================================================
        // 給与支払いテスト
        // ================================================================

        /// <summary>
        /// PayMonthlySalaries がスタッフ0人の場合に 0 を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator PayMonthlySalaries_ReturnsZeroWhenNoStaff()
        {
            yield return null;

            float paid = _staffManager.PayMonthlySalaries();
            Assert.AreEqual(0f, paid,
                "スタッフ0人の場合、PayMonthlySalaries は 0 を返すべき");
        }

        // ================================================================
        // 解雇テスト
        // ================================================================

        /// <summary>
        /// FireStaff が存在しないIDに対して false を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator FireStaff_ReturnsFalseForNonexistentId()
        {
            yield return null;

            bool result = _staffManager.FireStaff(9999);
            Assert.IsFalse(result,
                "存在しないIDでは FireStaff は false を返すべき");
        }

        // ================================================================
        // ストライキテスト
        // ================================================================

        /// <summary>
        /// ResolveAllStrikes がスタッフ0人の場合に 0 を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator ResolveAllStrikes_ReturnsZeroWhenNoStaff()
        {
            yield return null;

            int resolved = _staffManager.ResolveAllStrikes();
            Assert.AreEqual(0, resolved,
                "スタッフ0人の場合、ResolveAllStrikes は 0 を返すべき");
        }

        /// <summary>
        /// ResolveStrike が存在しないIDに対して false を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator ResolveStrike_ReturnsFalseForNonexistentId()
        {
            yield return null;

            bool result = _staffManager.ResolveStrike(9999);
            Assert.IsFalse(result,
                "存在しないIDでは ResolveStrike は false を返すべき");
        }

        // ================================================================
        // UI向けデータ取得テスト
        // ================================================================

        /// <summary>
        /// GetStaffData が存在しないIDに対して null を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetStaffData_ReturnsNullForNonexistentId()
        {
            yield return null;

            var data = _staffManager.GetStaffData(9999);
            Assert.IsNull(data,
                "存在しないIDでは GetStaffData は null を返すべき");
        }

        /// <summary>
        /// GetStaffListByType が初期状態で空リストを返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetStaffListByType_ReturnsEmptyListInitially()
        {
            yield return null;

            var list = _staffManager.GetStaffListByType(StaffType.Guard);
            Assert.IsNotNull(list, "GetStaffListByType は null ではなく空リストを返すべき");
            Assert.AreEqual(0, list.Count, "初期状態のリストは空であるべき");
        }

        // ================================================================
        // ClearAllStaff テスト
        // ================================================================

        /// <summary>
        /// ClearAllStaff がエラーなく完了すること（空の状態で呼んでも安全）。
        /// </summary>
        [UnityTest]
        public IEnumerator ClearAllStaff_SafeWhenEmpty()
        {
            yield return null;

            // 空の状態でクリアしても例外が発生しないこと
            Assert.DoesNotThrow(() => _staffManager.ClearAllStaff(),
                "空の状態で ClearAllStaff を呼んでもエラーにならないべき");
            Assert.AreEqual(0, _staffManager.TotalStaffCount,
                "ClearAllStaff 後の TotalStaffCount は 0 であるべき");
        }
    }
}
