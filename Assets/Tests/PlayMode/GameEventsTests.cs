// ============================================================
// ThemeParkGame - GameEvents PlayMode Tests
// GameEvents (静的イベントバス) の公開APIに対するPlayModeテスト
// ============================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ThemeParkGame.Core;

namespace ThemeParkGame.Tests
{
    /// <summary>
    /// GameEvents の PlayMode テスト。
    /// 静的クラスのため、TearDown で必ず ClearAll を呼び出してリセットする。
    /// </summary>
    [TestFixture]
    public class GameEventsTests
    {
        [SetUp]
        public void SetUp()
        {
            // 他テストからの購読が残らないようにクリア
            GameEvents.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            // テスト後の確実なクリーンアップ
            GameEvents.ClearAll();
        }

        /// <summary>
        /// FireVisitorEnterPark を呼び出すと OnVisitorEnterPark イベントが発火し、
        /// 購読者が正しい visitorId を受け取ること。
        /// </summary>
        [UnityTest]
        public IEnumerator FireVisitorEnterPark_InvokesEvent()
        {
            int receivedId = -1;
            bool wasCalled = false;

            GameEvents.OnVisitorEnterPark += (id) =>
            {
                receivedId = id;
                wasCalled = true;
            };

            yield return null;

            GameEvents.FireVisitorEnterPark(42);

            yield return null;

            Assert.IsTrue(wasCalled,
                "FireVisitorEnterPark を呼んだ後、OnVisitorEnterPark の購読者が呼ばれるべき");
            Assert.AreEqual(42, receivedId,
                "購読者に渡された visitorId は FireVisitorEnterPark に渡した値と一致するべき");
        }

        /// <summary>
        /// ClearAll を呼び出すと全イベントの購読が解除され、
        /// その後 Fire しても購読者が呼ばれないこと。
        /// </summary>
        [UnityTest]
        public IEnumerator ClearAll_RemovesAllSubscriptions()
        {
            bool visitorEventCalled = false;
            bool moneyEventCalled = false;
            bool attractionEventCalled = false;

            GameEvents.OnVisitorEnterPark += (_) => { visitorEventCalled = true; };
            GameEvents.OnMoneyChanged += (_) => { moneyEventCalled = true; };
            GameEvents.OnAttractionBuilt += (_) => { attractionEventCalled = true; };

            yield return null;

            // ClearAll で全購読を解除
            GameEvents.ClearAll();

            // 各イベントを発火
            GameEvents.FireVisitorEnterPark(1);
            GameEvents.FireMoneyChanged(1000f);
            GameEvents.FireAttractionBuilt(1);

            yield return null;

            Assert.IsFalse(visitorEventCalled,
                "ClearAll 後に FireVisitorEnterPark しても購読者は呼ばれないべき");
            Assert.IsFalse(moneyEventCalled,
                "ClearAll 後に FireMoneyChanged しても購読者は呼ばれないべき");
            Assert.IsFalse(attractionEventCalled,
                "ClearAll 後に FireAttractionBuilt しても購読者は呼ばれないべき");
        }
    }
}
