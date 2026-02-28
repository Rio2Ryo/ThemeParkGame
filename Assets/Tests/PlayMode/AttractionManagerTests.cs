// ============================================================
// ThemeParkGame - AttractionManager PlayMode Tests
// AttractionManager の公開APIに対するPlayModeテスト
// ============================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ThemeParkGame.Attraction;

namespace ThemeParkGame.Tests
{
    /// <summary>
    /// AttractionManager の PlayMode テスト。
    /// MonoBehaviour であるため new GameObject() + AddComponent で生成し、
    /// TearDown で DestroyImmediate によるクリーンアップを行う。
    /// </summary>
    [TestFixture]
    public class AttractionManagerTests
    {
        private GameObject _gameObject;
        private AttractionManager _attractionManager;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("AttractionManager_Test");
            _attractionManager = _gameObject.AddComponent<AttractionManager>();
            _attractionManager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
            _gameObject = null;
            _attractionManager = null;
        }

        /// <summary>
        /// Initialize 直後の TotalCount が 0 であること。
        /// </summary>
        [UnityTest]
        public IEnumerator TotalCount_InitiallyZero()
        {
            yield return null;

            Assert.AreEqual(0, _attractionManager.TotalCount,
                "Initialize 直後の TotalCount は 0 であるべき");
        }

        /// <summary>
        /// Initialize 直後の OperationalCount が 0 であること。
        /// </summary>
        [UnityTest]
        public IEnumerator OperationalCount_InitiallyZero()
        {
            yield return null;

            Assert.AreEqual(0, _attractionManager.OperationalCount,
                "Initialize 直後の OperationalCount は 0 であるべき");
        }

        /// <summary>
        /// Initialize 直後の BrokenDownCount が 0 であること。
        /// </summary>
        [UnityTest]
        public IEnumerator BrokenDownCount_InitiallyZero()
        {
            yield return null;

            Assert.AreEqual(0, _attractionManager.BrokenDownCount,
                "Initialize 直後の BrokenDownCount は 0 であるべき");
        }
    }
}
