// ============================================================
// ThemeParkGame - GameManager PlayMode Tests
// GameManager の公開APIに対するPlayModeテスト
// ============================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ThemeParkGame.Core;

namespace ThemeParkGame.Tests
{
    /// <summary>
    /// GameManager の PlayMode テスト。
    /// 静的メソッド（難易度パラメータ）および
    /// シングルトン生成後のインスタンスメソッドをテストする。
    /// </summary>
    [TestFixture]
    public class GameManagerTests
    {
        private GameObject _gameObject;
        private GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            // 既存のインスタンスをクリーンアップ
            if (GameManager.Instance != null)
            {
                Object.DestroyImmediate(GameManager.Instance.gameObject);
            }
            GameEvents.ClearAll();

            _gameObject = new GameObject("GameManager_Test");
            _gm = _gameObject.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
            _gameObject = null;
            _gm = null;
            GameEvents.ClearAll();
            Time.timeScale = 1f;
        }

        // ================================================================
        // シングルトン テスト
        // ================================================================

        /// <summary>
        /// GameManager.Instance がAwake後に設定されること。
        /// </summary>
        [UnityTest]
        public IEnumerator Instance_IsSetAfterAwake()
        {
            yield return null;

            Assert.IsNotNull(GameManager.Instance,
                "Awake 後に Instance が設定されるべき");
            Assert.AreEqual(_gm, GameManager.Instance,
                "Instance は生成したGameManagerと同一であるべき");
        }

        /// <summary>
        /// 初期状態が MainMenu であること。
        /// </summary>
        [UnityTest]
        public IEnumerator InitialState_IsMainMenu()
        {
            yield return null;

            Assert.AreEqual(GameState.MainMenu, _gm.CurrentState,
                "初期状態は MainMenu であるべき");
        }

        // ================================================================
        // 難易度パラメータ テスト（静的メソッド）
        // ================================================================

        /// <summary>
        /// GetStartingMoney が難易度ごとに正しい初期資金を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetStartingMoney_ReturnsCorrectValues()
        {
            yield return null;

            Assert.AreEqual(80000, GameManager.GetStartingMoney(GameDifficulty.Easy),
                "Easy の初期資金は 80000 であるべき");
            Assert.AreEqual(50000, GameManager.GetStartingMoney(GameDifficulty.Normal),
                "Normal の初期資金は 50000 であるべき");
            Assert.AreEqual(30000, GameManager.GetStartingMoney(GameDifficulty.Hard),
                "Hard の初期資金は 30000 であるべき");
        }

        /// <summary>
        /// GetSpawnInterval が難易度ごとに正しいスポーン間隔を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetSpawnInterval_ReturnsCorrectValues()
        {
            yield return null;

            Assert.AreEqual(8f, GameManager.GetSpawnInterval(GameDifficulty.Easy),
                "Easy のスポーン間隔は 8 であるべき");
            Assert.AreEqual(5f, GameManager.GetSpawnInterval(GameDifficulty.Normal),
                "Normal のスポーン間隔は 5 であるべき");
            Assert.AreEqual(2.5f, GameManager.GetSpawnInterval(GameDifficulty.Hard),
                "Hard のスポーン間隔は 2.5 であるべき");
        }

        /// <summary>
        /// GetMaxVisitors が難易度ごとに正しい最大来場者数を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetMaxVisitors_ReturnsCorrectValues()
        {
            yield return null;

            Assert.AreEqual(30, GameManager.GetMaxVisitors(GameDifficulty.Easy),
                "Easy の最大来場者数は 30 であるべき");
            Assert.AreEqual(50, GameManager.GetMaxVisitors(GameDifficulty.Normal),
                "Normal の最大来場者数は 50 であるべき");
            Assert.AreEqual(80, GameManager.GetMaxVisitors(GameDifficulty.Hard),
                "Hard の最大来場者数は 80 であるべき");
        }

        /// <summary>
        /// GetMaintenanceCostMultiplier が難易度ごとに正しい倍率を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetMaintenanceCostMultiplier_ReturnsCorrectValues()
        {
            yield return null;

            Assert.AreEqual(0.7f, GameManager.GetMaintenanceCostMultiplier(GameDifficulty.Easy), 0.01f,
                "Easy の維持費倍率は 0.7 であるべき");
            Assert.AreEqual(1.0f, GameManager.GetMaintenanceCostMultiplier(GameDifficulty.Normal), 0.01f,
                "Normal の維持費倍率は 1.0 であるべき");
            Assert.AreEqual(1.4f, GameManager.GetMaintenanceCostMultiplier(GameDifficulty.Hard), 0.01f,
                "Hard の維持費倍率は 1.4 であるべき");
        }

        /// <summary>
        /// GetScoreMultiplier が難易度ごとに正しい倍率を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetScoreMultiplier_ReturnsCorrectValues()
        {
            yield return null;

            Assert.AreEqual(0.8f, GameManager.GetScoreMultiplier(GameDifficulty.Easy), 0.01f,
                "Easy のスコア倍率は 0.8 であるべき");
            Assert.AreEqual(1.0f, GameManager.GetScoreMultiplier(GameDifficulty.Normal), 0.01f,
                "Normal のスコア倍率は 1.0 であるべき");
            Assert.AreEqual(1.5f, GameManager.GetScoreMultiplier(GameDifficulty.Hard), 0.01f,
                "Hard のスコア倍率は 1.5 であるべき");
        }

        /// <summary>
        /// GetWeatherImpactMultiplier が難易度ごとに正しい倍率を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetWeatherImpactMultiplier_ReturnsCorrectValues()
        {
            yield return null;

            Assert.AreEqual(0.5f, GameManager.GetWeatherImpactMultiplier(GameDifficulty.Easy), 0.01f,
                "Easy の天候影響倍率は 0.5 であるべき");
            Assert.AreEqual(1.0f, GameManager.GetWeatherImpactMultiplier(GameDifficulty.Normal), 0.01f,
                "Normal の天候影響倍率は 1.0 であるべき");
            Assert.AreEqual(1.5f, GameManager.GetWeatherImpactMultiplier(GameDifficulty.Hard), 0.01f,
                "Hard の天候影響倍率は 1.5 であるべき");
        }

        /// <summary>
        /// GetBreakdownRateMultiplier が難易度ごとに正しい倍率を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetBreakdownRateMultiplier_ReturnsCorrectValues()
        {
            yield return null;

            Assert.AreEqual(0.6f, GameManager.GetBreakdownRateMultiplier(GameDifficulty.Easy), 0.01f,
                "Easy の故障率倍率は 0.6 であるべき");
            Assert.AreEqual(1.5f, GameManager.GetBreakdownRateMultiplier(GameDifficulty.Hard), 0.01f,
                "Hard の故障率倍率は 1.5 であるべき");
        }

        /// <summary>
        /// GetVisitorCashMultiplier が難易度ごとに正しい倍率を返すこと。
        /// </summary>
        [UnityTest]
        public IEnumerator GetVisitorCashMultiplier_ReturnsCorrectValues()
        {
            yield return null;

            Assert.AreEqual(1.3f, GameManager.GetVisitorCashMultiplier(GameDifficulty.Easy), 0.01f,
                "Easy の来場者所持金倍率は 1.3 であるべき");
            Assert.AreEqual(1.0f, GameManager.GetVisitorCashMultiplier(GameDifficulty.Normal), 0.01f,
                "Normal の来場者所持金倍率は 1.0 であるべき");
            Assert.AreEqual(0.8f, GameManager.GetVisitorCashMultiplier(GameDifficulty.Hard), 0.01f,
                "Hard の来場者所持金倍率は 0.8 であるべき");
        }

        // ================================================================
        // ゴールデンチケット テスト
        // ================================================================

        /// <summary>
        /// 初期状態で GoldenTickets が 0 であること。
        /// </summary>
        [UnityTest]
        public IEnumerator GoldenTickets_InitiallyZero()
        {
            yield return null;

            Assert.AreEqual(0, _gm.GoldenTickets,
                "初期の GoldenTickets は 0 であるべき");
        }

        /// <summary>
        /// AwardGoldenTicket を呼ぶと GoldenTickets が増加すること。
        /// </summary>
        [UnityTest]
        public IEnumerator AwardGoldenTicket_IncrementsCount()
        {
            yield return null;

            _gm.AwardGoldenTicket();
            Assert.AreEqual(1, _gm.GoldenTickets,
                "AwardGoldenTicket 後に GoldenTickets が 1 であるべき");

            _gm.AwardGoldenTicket();
            _gm.AwardGoldenTicket();
            Assert.AreEqual(3, _gm.GoldenTickets,
                "3回 AwardGoldenTicket 後に GoldenTickets が 3 であるべき");
        }

        /// <summary>
        /// SpendGoldenTicket が十分なチケットがある場合に成功すること。
        /// </summary>
        [UnityTest]
        public IEnumerator SpendGoldenTicket_SucceedsWithSufficientTickets()
        {
            yield return null;

            _gm.AwardGoldenTicket();
            _gm.AwardGoldenTicket();

            bool result = _gm.SpendGoldenTicket(1);
            Assert.IsTrue(result, "十分なチケットがある場合、SpendGoldenTicket は true を返すべき");
            Assert.AreEqual(1, _gm.GoldenTickets,
                "1枚使用後のチケット数は 1 であるべき");
        }

        /// <summary>
        /// SpendGoldenTicket がチケット不足の場合に失敗すること。
        /// </summary>
        [UnityTest]
        public IEnumerator SpendGoldenTicket_FailsWithInsufficientTickets()
        {
            yield return null;

            bool result = _gm.SpendGoldenTicket(1);
            Assert.IsFalse(result, "チケットがない場合、SpendGoldenTicket は false を返すべき");
            Assert.AreEqual(0, _gm.GoldenTickets,
                "失敗後もチケット数は変わらないべき");
        }

        /// <summary>
        /// RestoreGoldenTickets でチケット数を復元できること。
        /// </summary>
        [UnityTest]
        public IEnumerator RestoreGoldenTickets_SetsCorrectCount()
        {
            yield return null;

            _gm.RestoreGoldenTickets(5);
            Assert.AreEqual(5, _gm.GoldenTickets,
                "RestoreGoldenTickets(5) 後のチケット数は 5 であるべき");

            // 負の値は 0 にクランプ
            _gm.RestoreGoldenTickets(-3);
            Assert.AreEqual(0, _gm.GoldenTickets,
                "負の値は 0 にクランプされるべき");
        }

        // ================================================================
        // ゲーム状態 テスト
        // ================================================================

        /// <summary>
        /// EnterBuildMode / ExitBuildMode で状態が正しく遷移すること。
        /// </summary>
        [UnityTest]
        public IEnumerator BuildMode_StateTransitions()
        {
            yield return null;

            _gm.EnterBuildMode();
            Assert.AreEqual(GameState.BuildMode, _gm.CurrentState,
                "EnterBuildMode 後は BuildMode であるべき");

            _gm.ExitBuildMode();
            Assert.AreEqual(GameState.Playing, _gm.CurrentState,
                "ExitBuildMode 後は Playing であるべき");
        }

        /// <summary>
        /// SwitchViewMode で視点モードが正しく変更されること。
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchViewMode_ChangesViewMode()
        {
            yield return null;

            Assert.AreEqual(ViewMode.GodView, _gm.CurrentViewMode,
                "初期の ViewMode は GodView であるべき");

            _gm.SwitchViewMode(ViewMode.FirstPerson);
            Assert.AreEqual(ViewMode.FirstPerson, _gm.CurrentViewMode,
                "SwitchViewMode 後に FirstPerson であるべき");
            Assert.AreEqual(GameState.FirstPersonMode, _gm.CurrentState,
                "FirstPerson 時のゲーム状態は FirstPersonMode であるべき");
        }

        /// <summary>
        /// RestoreDifficulty で難易度を復元できること。
        /// </summary>
        [UnityTest]
        public IEnumerator RestoreDifficulty_SetsCorrectDifficulty()
        {
            yield return null;

            Assert.AreEqual(GameDifficulty.Normal, _gm.CurrentDifficulty,
                "初期の難易度は Normal であるべき");

            _gm.RestoreDifficulty(GameDifficulty.Hard);
            Assert.AreEqual(GameDifficulty.Hard, _gm.CurrentDifficulty,
                "RestoreDifficulty(Hard) 後の難易度は Hard であるべき");
        }

        /// <summary>
        /// IsPaused プロパティが GameState.Paused のときだけ true であること。
        /// </summary>
        [UnityTest]
        public IEnumerator IsPaused_ReflectsCurrentState()
        {
            yield return null;

            Assert.IsFalse(_gm.IsPaused,
                "MainMenu 状態では IsPaused は false であるべき");
        }
    }
}
