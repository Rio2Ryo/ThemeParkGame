// ============================================================
// ThemeParkGame - Tutorial System
// 初回プレイヤー向けチュートリアル管理
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>チュートリアルステップの識別子</summary>
    public enum TutorialStep
    {
        Welcome,              // ようこそ
        CameraControls,       // カメラ操作
        BuildFirstPath,       // 最初の道を敷く
        BuildFirstAttraction, // 最初のアトラクション建設
        SetTicketPrice,       // チケット価格設定
        OpenPark,             // パークを開園
        HireMechanic,         // メカニックを雇う
        HireCleaner,          // スイーパーを雇う
        BuildFoodShop,        // フードショップ建設
        BuildToilet,          // トイレ建設
        CheckFinances,        // 経営状況を確認
        ResearchIntro,        // 研究開発の紹介
        Completed             // チュートリアル完了
    }

    /// <summary>
    /// チュートリアルステップのデータ定義。
    /// </summary>
    [Serializable]
    public class TutorialStepData
    {
        public TutorialStep Step;
        public string Title;
        public string Description;
        public string HighlightTarget;
        public bool RequiresAction;
    }

    /// <summary>
    /// 初回プレイヤー向けのインタラクティブチュートリアルを管理する。
    /// 各ステップでUIハイライト・説明テキスト・操作ガイドを表示し、
    /// プレイヤーが対応するアクションを完了すると次のステップに進む。
    /// </summary>
    public class TutorialSystem : MonoBehaviour
    {
        public static TutorialSystem Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private GameObject highlightOverlay;

        [Header("Settings")]
        [SerializeField] private bool showOnFirstPlay = true;

        // 状態
        private TutorialStep _currentStep = TutorialStep.Welcome;
        private bool _isActive;
        private bool _tutorialCompleted;

        public TutorialStep CurrentStep => _currentStep;
        public bool IsActive => _isActive;
        public bool IsCompleted => _tutorialCompleted;

        // チュートリアルステップデータ
        private readonly List<TutorialStepData> _steps = new List<TutorialStepData>();

        /// <summary>チュートリアルステップ完了イベント</summary>
        public event Action<TutorialStep> OnStepCompleted;

        /// <summary>チュートリアル全体完了イベント</summary>
        public event Action OnTutorialCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeSteps();
        }

        private void Start()
        {
            // チュートリアル完了済みかチェック
            _tutorialCompleted = PlayerPrefs.GetInt("TutorialCompleted", 0) == 1;

            if (showOnFirstPlay && !_tutorialCompleted)
            {
                StartTutorial();
            }
            else
            {
                HideTutorialUI();
            }

            // ゲームイベントを購読
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void InitializeSteps()
        {
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.Welcome,
                Title = "ようこそ！",
                Description = "テーマパークの経営者になりましょう！\nパークを作って、お客さんを楽しませてください。",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.CameraControls,
                Title = "カメラ操作",
                Description = "画面をドラッグしてカメラを移動、\nピンチイン/アウトでズームできます。",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.BuildFirstPath,
                Title = "道を作ろう",
                Description = "まず入口から道を作りましょう。\n「建設」ボタンをタップして道を選んでください。",
                HighlightTarget = "BuildButton",
                RequiresAction = true
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.BuildFirstAttraction,
                Title = "アトラクション建設",
                Description = "道の横にアトラクションを建設しましょう！\n「アトラクション」タブから好きなものを選んでください。",
                HighlightTarget = "BuildPanel_Attractions",
                RequiresAction = true
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.SetTicketPrice,
                Title = "チケット価格設定",
                Description = "アトラクションをタップして価格を設定しましょう。\n高すぎるとお客さんが不満に、安すぎると赤字になります。",
                RequiresAction = true
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.OpenPark,
                Title = "パークを開園！",
                Description = "準備ができたらパークを開園しましょう！\nお客さんが入ってきます。",
                HighlightTarget = "OpenParkButton",
                RequiresAction = true
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.HireMechanic,
                Title = "メカニックを雇おう",
                Description = "アトラクションは壊れることがあります。\nメカニックを雇って修理してもらいましょう。",
                HighlightTarget = "StaffPanel",
                RequiresAction = true
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.HireCleaner,
                Title = "スイーパーを雇おう",
                Description = "パークをきれいに保つためにスイーパーを雇いましょう。\n汚いパークはお客さんの満足度が下がります。",
                RequiresAction = true
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.BuildFoodShop,
                Title = "フードショップ建設",
                Description = "お腹が空いたお客さんのためにフードショップを建てましょう。",
                RequiresAction = true
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.BuildToilet,
                Title = "トイレ建設",
                Description = "トイレも忘れずに！\nトイレがないとお客さんが困ります。",
                RequiresAction = true
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.CheckFinances,
                Title = "経営状況チェック",
                Description = "画面上部の資金表示をタップして、\n収支を確認しましょう。",
                HighlightTarget = "FinanceButton",
                RequiresAction = true
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.ResearchIntro,
                Title = "研究開発",
                Description = "サイエンティストを雇って新しいアトラクションを\n研究開発しましょう！パークの魅力が広がります。",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.Completed,
                Title = "チュートリアル完了！",
                Description = "基本操作はこれでバッチリです！\n素敵なテーマパークを作ってくださいね。\n\nヒント: お客さんをタップすると会話できます！",
                RequiresAction = false
            });
        }

        /// <summary>チュートリアルを開始する</summary>
        public void StartTutorial()
        {
            _isActive = true;
            _currentStep = TutorialStep.Welcome;
            ShowCurrentStep();
        }

        /// <summary>チュートリアルをスキップする</summary>
        public void SkipTutorial()
        {
            _isActive = false;
            _tutorialCompleted = true;
            PlayerPrefs.SetInt("TutorialCompleted", 1);
            PlayerPrefs.Save();
            HideTutorialUI();
            OnTutorialCompleted?.Invoke();
        }

        /// <summary>次のステップに進む</summary>
        public void AdvanceToNextStep()
        {
            if (!_isActive) return;

            OnStepCompleted?.Invoke(_currentStep);

            int nextIndex = (int)_currentStep + 1;
            if (nextIndex >= Enum.GetValues(typeof(TutorialStep)).Length ||
                (TutorialStep)nextIndex == TutorialStep.Completed)
            {
                CompleteTutorial();
                return;
            }

            _currentStep = (TutorialStep)nextIndex;
            ShowCurrentStep();
        }

        /// <summary>特定のアクションが完了した時に呼ばれる（外部システムから）</summary>
        public void NotifyActionCompleted(TutorialStep step)
        {
            if (!_isActive || step != _currentStep) return;

            var stepData = GetStepData(_currentStep);
            if (stepData != null && stepData.RequiresAction)
            {
                AdvanceToNextStep();
            }
        }

        private void CompleteTutorial()
        {
            _currentStep = TutorialStep.Completed;
            ShowCurrentStep();

            _tutorialCompleted = true;
            PlayerPrefs.SetInt("TutorialCompleted", 1);
            PlayerPrefs.Save();

            // 完了ステップ表示後にUIを非表示
            _isActive = false;
            OnTutorialCompleted?.Invoke();
        }

        private void ShowCurrentStep()
        {
            var stepData = GetStepData(_currentStep);
            if (stepData == null) return;

            if (tutorialPanel != null) tutorialPanel.SetActive(true);
            if (titleText != null) titleText.text = stepData.Title;
            if (descriptionText != null) descriptionText.text = stepData.Description;

            // ハイライト対象の設定
            if (highlightOverlay != null)
            {
                highlightOverlay.SetActive(!string.IsNullOrEmpty(stepData.HighlightTarget));
            }

            // 次へボタンの表示切替（アクション必要なステップでは非表示）
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(!stepData.RequiresAction);
            }
        }

        private void HideTutorialUI()
        {
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            if (highlightOverlay != null) highlightOverlay.SetActive(false);
        }

        private TutorialStepData GetStepData(TutorialStep step)
        {
            return _steps.Find(s => s.Step == step);
        }

        // ============================================================
        // Event Subscriptions (auto-advance on game events)
        // ============================================================

        private void SubscribeToEvents()
        {
            GameEvents.OnAttractionBuilt += OnAttractionBuilt;
            GameEvents.OnStaffHired += OnStaffHired;
            GameEvents.OnParkOpened += OnParkOpened;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnAttractionBuilt -= OnAttractionBuilt;
            GameEvents.OnStaffHired -= OnStaffHired;
            GameEvents.OnParkOpened -= OnParkOpened;
        }

        private void OnAttractionBuilt(int id)
        {
            NotifyActionCompleted(TutorialStep.BuildFirstAttraction);
        }

        private void OnStaffHired(int staffId, StaffType type)
        {
            if (type == StaffType.Mechanic)
                NotifyActionCompleted(TutorialStep.HireMechanic);
            else if (type == StaffType.Cleaner)
                NotifyActionCompleted(TutorialStep.HireCleaner);
        }

        private void OnParkOpened()
        {
            NotifyActionCompleted(TutorialStep.OpenPark);
        }
    }
}
