// ============================================================
// ThemeParkGame - EconomyManager PlayMode Tests
// EconomyManager の公開APIに対するPlayModeテスト
// ============================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ThemeParkGame.Economy;

namespace ThemeParkGame.Tests
{
    /// <summary>
    /// EconomyManager の PlayMode テスト。
    /// MonoBehaviour であるため new GameObject() + AddComponent で生成し、
    /// TearDown で DestroyImmediate によるクリーンアップを行う。
    /// </summary>
    [TestFixture]
    public class EconomyManagerTests
    {
        private GameObject _gameObject;
        private EconomyManager _economy;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("EconomyManager_Test");
            _economy = _gameObject.AddComponent<EconomyManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
            _gameObject = null;
            _economy = null;
        }

        /// <summary>
        /// Initialize に正の初期資金を渡した場合、CurrentBalance がその値になること。
        /// Initialize(0) の場合は 0 であること（ゼロまたは正）。
        /// </summary>
        [UnityTest]
        public IEnumerator InitialBalance_IsZeroOrPositive()
        {
            // Initialize に 0 を渡した場合
            _economy.Initialize(0);
            yield return null;

            Assert.AreEqual(0f, _economy.CurrentBalance,
                "Initialize(0) 後の CurrentBalance は 0 であるべき");

            // Initialize に正の値を渡した場合
            _economy.Initialize(50000);
            yield return null;

            Assert.AreEqual(50000f, _economy.CurrentBalance,
                "Initialize(50000) 後の CurrentBalance は 50000 であるべき");
            Assert.GreaterOrEqual(_economy.CurrentBalance, 0f,
                "CurrentBalance は 0 以上であるべき");
        }

        /// <summary>
        /// AddRevenue を呼び出すと CurrentBalance が増加すること。
        /// </summary>
        [UnityTest]
        public IEnumerator AddRevenue_IncreasesBalance()
        {
            _economy.Initialize(10000);
            yield return null;

            float balanceBefore = _economy.CurrentBalance;
            _economy.AddRevenue(500f, RevenueCategory.EntranceFee);
            yield return null;

            Assert.AreEqual(balanceBefore + 500f, _economy.CurrentBalance,
                "AddRevenue(500) 後、残高が 500 増加しているべき");
            Assert.AreEqual(500f, _economy.TotalRevenueEarned,
                "TotalRevenueEarned が加算された収入と一致するべき");
        }

        /// <summary>
        /// PayExpense を呼び出すと CurrentBalance が減少すること。
        /// 戻り値が true であること。
        /// </summary>
        [UnityTest]
        public IEnumerator PayExpense_DecreasesBalance()
        {
            _economy.Initialize(10000);
            yield return null;

            float balanceBefore = _economy.CurrentBalance;
            bool result = _economy.PayExpense(300f, ExpenseCategory.Maintenance);
            yield return null;

            Assert.IsTrue(result, "PayExpense は成功時に true を返すべき");
            Assert.AreEqual(balanceBefore - 300f, _economy.CurrentBalance,
                "PayExpense(300) 後、残高が 300 減少しているべき");
            Assert.AreEqual(300f, _economy.TotalExpensesPaid,
                "TotalExpensesPaid が支出額と一致するべき");
        }

        /// <summary>
        /// GetMonthlyProfit が当月の収入 - 支出の正しい値を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetMonthlyProfit_ReturnsCorrectValue()
        {
            _economy.Initialize(100000);
            yield return null;

            // 収入を加算
            _economy.AddRevenue(2000f, RevenueCategory.AttractionFee);
            _economy.AddRevenue(500f, RevenueCategory.ShopSale);

            // 支出を加算
            _economy.PayExpense(800f, ExpenseCategory.StaffSalary);

            yield return null;

            float expectedProfit = (2000f + 500f) - 800f;
            Assert.AreEqual(expectedProfit, _economy.GetMonthlyProfit(), 0.01f,
                "GetMonthlyProfit は当月の (収入合計 - 支出合計) を返すべき");
        }
    }
}
