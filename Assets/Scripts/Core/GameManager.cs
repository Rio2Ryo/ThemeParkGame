// ============================================================
// ThemeParkGame - GameManager (Singleton)
// ゲーム全体の統括管理
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ThemeParkGame.AI;
using ThemeParkGame.Attraction;
using ThemeParkGame.Economy;
using ThemeParkGame.Park;
using ThemeParkGame.Staff;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// ゲーム全体を統括するシングルトンマネージャー。
    /// 各サブシステムの初期化・更新・終了を管理する。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game Settings")]
        [SerializeField] private float gameSpeedMultiplier = 1f;
        [SerializeField] private int startingMoney = 50000;

        // サブシステム参照
        public ParkManager ParkManager { get; private set; }
        public EconomyManager EconomyManager { get; private set; }
        public VisitorManager VisitorManager { get; private set; }
        public StaffManager StaffManager { get; private set; }
        public ResearchManager ResearchManager { get; private set; }
        public WeatherSystem WeatherSystem { get; private set; }
        public AIConversationManager AIManager { get; private set; }
        public TimeManager TimeManager { get; private set; }
        public AttractionManager AttractionManager { get; private set; }

        // ゲーム状態
        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public ViewMode CurrentViewMode { get; private set; } = ViewMode.GodView;
        public int GoldenTickets { get; private set; }
        public bool IsPaused => CurrentState == GameState.Paused;

        // ゲーム速度: 0=一時停止, 1=通常, 2=2倍速, 3=3倍速
        private int _speedLevel = 1;
        public int SpeedLevel
        {
            get => _speedLevel;
            set
            {
                _speedLevel = Mathf.Clamp(value, 0, 5);
                Time.timeScale = _speedLevel * gameSpeedMultiplier;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeSubSystems();
        }

        private void InitializeSubSystems()
        {
            TimeManager = GetOrAddComponent<TimeManager>();
            EconomyManager = GetOrAddComponent<EconomyManager>();
            ParkManager = GetOrAddComponent<ParkManager>();
            VisitorManager = GetOrAddComponent<VisitorManager>();
            StaffManager = GetOrAddComponent<StaffManager>();
            ResearchManager = GetOrAddComponent<ResearchManager>();
            WeatherSystem = GetOrAddComponent<WeatherSystem>();
            AIManager = GetOrAddComponent<AIConversationManager>();
            AttractionManager = GetOrAddComponent<AttractionManager>();
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            var component = GetComponent<T>();
            if (component == null)
                component = gameObject.AddComponent<T>();
            return component;
        }

        /// <summary>新しいゲームを開始する</summary>
        public void StartNewGame(ThemeZone startingZone)
        {
            EconomyManager.Initialize(startingMoney);
            ParkManager.Initialize(startingZone);
            TimeManager.Initialize();
            VisitorManager.Initialize();
            StaffManager.Initialize();
            ResearchManager.Initialize();
            WeatherSystem.Initialize();

            GoldenTickets = 0;
            CurrentState = GameState.Playing;
            SpeedLevel = 1;

            GameEvents.FireParkOpened();
            Debug.Log($"[GameManager] New game started in {startingZone}");
        }

        /// <summary>シナリオモードを開始する</summary>
        public void StartScenario(ScenarioCountry country)
        {
            var scenarioData = ScenarioDatabase.GetScenario(country);
            if (scenarioData == null)
            {
                Debug.LogError($"[GameManager] Scenario not found: {country}");
                return;
            }

            EconomyManager.Initialize(scenarioData.StartingMoney);
            ParkManager.InitializeFromScenario(scenarioData);
            TimeManager.Initialize();
            VisitorManager.Initialize();
            StaffManager.Initialize();
            ResearchManager.Initialize();
            WeatherSystem.Initialize();

            GoldenTickets = 0;
            CurrentState = GameState.Playing;
            SpeedLevel = 1;

            GameEvents.FireParkOpened();
            Debug.Log($"[GameManager] Scenario started: {country}");
        }

        /// <summary>視点モードを切り替える</summary>
        public void SwitchViewMode(ViewMode mode)
        {
            CurrentViewMode = mode;
            GameEvents.FireViewModeChanged(mode);

            switch (mode)
            {
                case ViewMode.GodView:
                    CurrentState = GameState.Playing;
                    break;
                case ViewMode.ResidentView:
                    CurrentState = GameState.Playing;
                    break;
                case ViewMode.FirstPerson:
                    CurrentState = GameState.FirstPersonMode;
                    break;
            }
        }

        /// <summary>ゴールデンチケットを獲得する</summary>
        public void AwardGoldenTicket()
        {
            GoldenTickets++;
            GameEvents.FireGoldenTicketEarned(GoldenTickets);
            Debug.Log($"[GameManager] Golden Ticket earned! Total: {GoldenTickets}");
        }

        /// <summary>ゴールデンチケットを使用する</summary>
        public bool SpendGoldenTicket(int amount = 1)
        {
            if (GoldenTickets < amount) return false;
            GoldenTickets -= amount;
            return true;
        }

        public void PauseGame()
        {
            if (CurrentState == GameState.Playing)
            {
                CurrentState = GameState.Paused;
                Time.timeScale = 0;
            }
        }

        public void ResumeGame()
        {
            if (CurrentState == GameState.Paused)
            {
                CurrentState = GameState.Playing;
                Time.timeScale = SpeedLevel * gameSpeedMultiplier;
            }
        }

        public void EnterBuildMode()
        {
            CurrentState = GameState.BuildMode;
        }

        public void ExitBuildMode()
        {
            CurrentState = GameState.Playing;
        }

        /// <summary>ゲームを終了して結果画面へ遷移する</summary>
        public void EndGame()
        {
            CurrentState = GameState.GameOver;
            Time.timeScale = 0f;
            Debug.Log("[GameManager] ゲーム終了 → 結果画面");
        }

        /// <summary>現在のゲームをリスタートする（結果画面から直接再開始）</summary>
        public void RestartGame()
        {
            Time.timeScale = 1f;

            // パーク閉園イベント（VisitorManagerのスポーン停止等）
            GameEvents.FireParkClosed();

            // 新しいゲームを開始
            StartNewGame(ThemeZone.LostKingdom);

            Debug.Log("[GameManager] ゲームをリスタートしました");
        }

        /// <summary>メインメニューに戻る</summary>
        public void ReturnToMainMenu()
        {
            CurrentState = GameState.MainMenu;
            Time.timeScale = 1f;

            // スポーン停止（ParkClosedイベント経由でVisitorManagerが対応）
            GameEvents.FireParkClosed();

            // スタート画面を再生成する
            GameBootstrapper.RecreateStartScreen();

            Debug.Log("[GameManager] メインメニューに戻りました");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                GameEvents.ClearAll();
                Instance = null;
            }
        }
    }
}
