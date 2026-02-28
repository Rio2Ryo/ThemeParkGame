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

        /// <summary>
        /// FireStaffHired を呼び出すと OnStaffHired イベントが発火し、
        /// 購読者が正しい staffId と StaffType を受け取ること。
        /// </summary>
        [UnityTest]
        public IEnumerator FireStaffHired_InvokesEventWithCorrectArgs()
        {
            int receivedId = -1;
            StaffType receivedType = StaffType.Cleaner;
            bool wasCalled = false;

            GameEvents.OnStaffHired += (id, type) =>
            {
                receivedId = id;
                receivedType = type;
                wasCalled = true;
            };

            yield return null;

            GameEvents.FireStaffHired(1001, StaffType.Mechanic);

            yield return null;

            Assert.IsTrue(wasCalled,
                "FireStaffHired 呼び出し後、OnStaffHired の購読者が呼ばれるべき");
            Assert.AreEqual(1001, receivedId,
                "購読者に渡された staffId は FireStaffHired に渡した値と一致するべき");
            Assert.AreEqual(StaffType.Mechanic, receivedType,
                "購読者に渡された StaffType は Mechanic であるべき");
        }

        /// <summary>
        /// FireMoneyChanged を呼び出すと OnMoneyChanged イベントが発火し、
        /// 購読者が正しい金額を受け取ること。
        /// </summary>
        [UnityTest]
        public IEnumerator FireMoneyChanged_InvokesEventWithCorrectAmount()
        {
            float receivedAmount = 0f;
            bool wasCalled = false;

            GameEvents.OnMoneyChanged += (amount) =>
            {
                receivedAmount = amount;
                wasCalled = true;
            };

            yield return null;

            GameEvents.FireMoneyChanged(50000f);

            yield return null;

            Assert.IsTrue(wasCalled,
                "FireMoneyChanged 呼び出し後、OnMoneyChanged の購読者が呼ばれるべき");
            Assert.AreEqual(50000f, receivedAmount, 0.01f,
                "購読者に渡された金額は 50000 であるべき");
        }

        /// <summary>
        /// FireWeatherChanged を呼び出すと OnWeatherChanged イベントが発火すること。
        /// </summary>
        [UnityTest]
        public IEnumerator FireWeatherChanged_InvokesEvent()
        {
            Weather receivedWeather = Weather.Sunny;
            bool wasCalled = false;

            GameEvents.OnWeatherChanged += (w) =>
            {
                receivedWeather = w;
                wasCalled = true;
            };

            yield return null;

            GameEvents.FireWeatherChanged(Weather.Rainy);

            yield return null;

            Assert.IsTrue(wasCalled,
                "FireWeatherChanged 呼び出し後、OnWeatherChanged の購読者が呼ばれるべき");
            Assert.AreEqual(Weather.Rainy, receivedWeather,
                "購読者に渡された天候は Rainy であるべき");
        }

        /// <summary>
        /// FireParkEventStarted を呼び出すと OnParkEventStarted イベントが発火すること。
        /// </summary>
        [UnityTest]
        public IEnumerator FireParkEventStarted_InvokesEventWithCorrectArgs()
        {
            string receivedEventId = null;
            string receivedDisplayName = null;
            bool wasCalled = false;

            GameEvents.OnParkEventStarted += (eventId, displayName) =>
            {
                receivedEventId = eventId;
                receivedDisplayName = displayName;
                wasCalled = true;
            };

            yield return null;

            GameEvents.FireParkEventStarted("halloween_2026", "ハロウィンイベント");

            yield return null;

            Assert.IsTrue(wasCalled,
                "FireParkEventStarted 呼び出し後、OnParkEventStarted の購読者が呼ばれるべき");
            Assert.AreEqual("halloween_2026", receivedEventId,
                "購読者に渡された eventId は正しいべき");
            Assert.AreEqual("ハロウィンイベント", receivedDisplayName,
                "購読者に渡された displayName は正しいべき");
        }

        /// <summary>
        /// FireGameSaved / FireGameLoaded がそれぞれ正しく発火すること。
        /// </summary>
        [UnityTest]
        public IEnumerator FireGameSavedAndLoaded_InvokeEvents()
        {
            bool savedCalled = false;
            bool loadedCalled = false;

            GameEvents.OnGameSaved += () => { savedCalled = true; };
            GameEvents.OnGameLoaded += () => { loadedCalled = true; };

            yield return null;

            GameEvents.FireGameSaved();
            Assert.IsTrue(savedCalled, "FireGameSaved 後に OnGameSaved が呼ばれるべき");

            GameEvents.FireGameLoaded();
            Assert.IsTrue(loadedCalled, "FireGameLoaded 後に OnGameLoaded が呼ばれるべき");
        }

        /// <summary>
        /// 複数の購読者が同じイベントを受け取れること。
        /// </summary>
        [UnityTest]
        public IEnumerator MultipleSubscribers_AllReceiveEvent()
        {
            int callCount = 0;

            GameEvents.OnVisitorEnterPark += (_) => { callCount++; };
            GameEvents.OnVisitorEnterPark += (_) => { callCount++; };
            GameEvents.OnVisitorEnterPark += (_) => { callCount++; };

            yield return null;

            GameEvents.FireVisitorEnterPark(1);

            yield return null;

            Assert.AreEqual(3, callCount,
                "3つの購読者全てが呼ばれるべき");
        }
    }
}
