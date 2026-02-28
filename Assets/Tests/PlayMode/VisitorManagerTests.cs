// ============================================================
// ThemeParkGame - VisitorManager PlayMode Tests
// VisitorManager の公開APIに対するPlayModeテスト
// ============================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Tests
{
    /// <summary>
    /// VisitorManager の PlayMode テスト。
    /// MonoBehaviour であるため new GameObject() + AddComponent で生成し、
    /// TearDown で DestroyImmediate によるクリーンアップを行う。
    /// </summary>
    [TestFixture]
    public class VisitorManagerTests
    {
        private GameObject _gameObject;
        private VisitorManager _visitorManager;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("VisitorManager_Test");
            _visitorManager = _gameObject.AddComponent<VisitorManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
            _gameObject = null;
            _visitorManager = null;
        }

        /// <summary>
        /// コンポーネント追加直後の ActiveVisitorCount が 0 であること。
        /// </summary>
        [UnityTest]
        public IEnumerator ActiveVisitorCount_InitiallyZero()
        {
            yield return null;

            Assert.AreEqual(0, _visitorManager.ActiveVisitorCount,
                "初期状態の ActiveVisitorCount は 0 であるべき");
        }

        /// <summary>
        /// コンポーネント追加直後の TotalVisitorsToday が 0 であること。
        /// </summary>
        [UnityTest]
        public IEnumerator TotalVisitorsToday_InitiallyZero()
        {
            yield return null;

            Assert.AreEqual(0, _visitorManager.TotalVisitorsToday,
                "初期状態の TotalVisitorsToday は 0 であるべき");
        }

        /// <summary>
        /// 来場者がいない状態で AverageHappiness が 0 を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator AverageHappiness_ReturnsZeroWhenNoVisitors()
        {
            yield return null;

            Assert.AreEqual(0f, _visitorManager.AverageHappiness,
                "来場者がいない場合、AverageHappiness は 0 であるべき");
        }
    }
}
