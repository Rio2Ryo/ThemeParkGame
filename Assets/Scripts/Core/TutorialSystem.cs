// ============================================================
// ThemeParkGame - Tutorial System
// 初回プレイヤー向けチュートリアル管理
// コードからUI構築、ステップ進行、イベント連動自動進行
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
        BuildFirstAttraction, // 最初のアトラクション建設
        OpenPark,             // パークを開園（来場者を待つ）
        HireStaff,            // スタッフを雇う
        BuildShop,            // ショップ建設
        CheckVisitor,         // 来場者をクリックしてみよう
        SpeedControl,         // ゲーム速度を変更
        SaveGame,             // セーブのやり方
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
        public bool RequiresAction;
    }

    /// <summary>
    /// 初回プレイヤー向けのインタラクティブチュートリアルを管理する。
    /// コードからUIを構築し、ステップ進行とイベント連動を行う。
    /// </summary>
    public class TutorialSystem : MonoBehaviour
    {
        public static TutorialSystem Instance { get; private set; }

        private const string PREF_KEY = "TutorialCompleted";

        // 状態
        private TutorialStep _currentStep = TutorialStep.Welcome;
        private bool _isActive;
        private bool _tutorialCompleted;
        private float _autoHideTimer;
        private float _pulseTimer;

        public TutorialStep CurrentStep => _currentStep;
        public bool IsActive => _isActive;
        public bool IsCompleted => _tutorialCompleted;

        // チュートリアルステップデータ
        private readonly List<TutorialStepData> _steps = new List<TutorialStepData>();

        /// <summary>チュートリアルステップ完了イベント</summary>
        public event Action<TutorialStep> OnStepCompleted;

        /// <summary>チュートリアル全体完了イベント</summary>
        public event Action OnTutorialCompleted;

        // ---- UI要素 ----
        private Canvas _tutorialCanvas;
        private GameObject _panel;
        private Text _stepIndicator;
        private Text _titleText;
        private Text _descText;
        private Button _nextBtn;
        private Button _skipBtn;
        private Image _panelBg;
        private Image _arrowIndicator;
        private GameObject _dimOverlay;

        // カラー
        private static readonly Color PanelBg = new Color(0.04f, 0.08f, 0.18f, 0.96f);
        private static readonly Color AccentGold = new Color(0.95f, 0.88f, 0.45f);
        private static readonly Color AccentCyan = new Color(0.4f, 0.85f, 0.95f);
        private static readonly Color BtnGreen = new Color(0.2f, 0.6f, 0.35f);
        private static readonly Color BtnGray = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color TextMuted = new Color(0.6f, 0.65f, 0.75f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeSteps();
            BuildUI();
        }

        private void Start()
        {
            _tutorialCompleted = PlayerPrefs.GetInt(PREF_KEY, 0) == 1;

            // 初回プレイ時はゲーム開始イベントを待つ
            if (!_tutorialCompleted)
            {
                GameEvents.OnParkOpened += OnParkOpenedForTutorial;
            }

            HideUI();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameEvents.OnParkOpened -= OnParkOpenedForTutorial;
            UnsubscribeFromEvents();
        }

        // ================================================================
        // ステップ定義
        // ================================================================

        private void InitializeSteps()
        {
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.Welcome,
                Title = "ようこそ！テーマパークへ",
                Description = "あなたはテーマパークの経営者です！\n" +
                    "パークにアトラクションやショップを建設し、\n" +
                    "来場者を楽しませて収益を上げましょう。",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.CameraControls,
                Title = "カメラ操作",
                Description = "マウスドラッグでカメラを移動できます。\n" +
                    "スクロールホイールでズームイン/アウト。\n" +
                    "画面右上の速度ボタンでゲーム速度を変更できます。",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.BuildFirstAttraction,
                Title = "アトラクションが自動建設されます",
                Description = "このゲームではアトラクションとショップが\n" +
                    "自動的に建設されます。\n" +
                    "画面右下のパネルで各アトラクションの\n" +
                    "状態と収益を確認できます。",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.OpenPark,
                Title = "来場者がやってきます！",
                Description = "パークが開園すると来場者がスポーンします。\n" +
                    "画面左下のパネルで来場者の状態分布を確認。\n" +
                    "画面上部のスコアボードで入場者数・収益・\n" +
                    "満足度をリアルタイムで把握できます。",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.HireStaff,
                Title = "スタッフの重要性",
                Description = "スタッフはゲーム開始時に自動配置されます。\n" +
                    "メカニック → アトラクション修理\n" +
                    "スイーパー → パーク清掃\n" +
                    "エンターテイナー → 来場者の満足度UP\n" +
                    "ガード → パーク警備",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.BuildShop,
                Title = "ショップとトイレ",
                Description = "来場者はお腹が空いたり喉が渇きます。\n" +
                    "フード/ドリンクショップで欲求を満たせます。\n" +
                    "トイレも重要！不足すると事故が起きます。\n" +
                    "ショップは収益源にもなります。",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.CheckVisitor,
                Title = "来場者をクリック！",
                Description = "来場者をクリックすると詳細情報が表示されます。\n" +
                    "満足度・空腹・渇き・トイレ欲求など\n" +
                    "各パラメータをチェックできます。\n" +
                    "来場者の気持ちを理解して経営に活かそう！",
                RequiresAction = true
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.SpeedControl,
                Title = "ゲーム速度の操作",
                Description = "画面右上の速度ボタンでゲーム速度を変更:\n" +
                    "  || = 一時停止   x1 = 通常速度\n" +
                    "  x2 = 2倍速     x5 = 5倍速\n" +
                    "じっくり考えたい時は一時停止を活用！",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.SaveGame,
                Title = "セーブ＆ロード",
                Description = "MENUボタン → SAVE/LOAD でゲームを保存。\n" +
                    "3つのスロットに保存できます。\n" +
                    "タイトル画面のCONTINUEで再開できます。\n" +
                    "こまめなセーブを忘れずに！",
                RequiresAction = false
            });
            _steps.Add(new TutorialStepData
            {
                Step = TutorialStep.Completed,
                Title = "チュートリアル完了！",
                Description = "基本操作はこれでバッチリです！\n" +
                    "ヒント:\n" +
                    "  - 来場者の満足度が0になると退園します\n" +
                    "  - 天候変化にも注意しましょう\n" +
                    "  - シナリオモードで目標達成に挑戦！\n" +
                    "素敵なテーマパークを作ってくださいね！",
                RequiresAction = false
            });
        }

        // ================================================================
        // UI構築
        // ================================================================

        private void BuildUI()
        {
            // 専用Canvas（HUDより上に描画）
            var canvasGo = new GameObject("TutorialCanvas");
            canvasGo.transform.SetParent(transform, false);
            _tutorialCanvas = canvasGo.AddComponent<Canvas>();
            _tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _tutorialCanvas.sortingOrder = 80;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var canvasRt = canvasGo.GetComponent<RectTransform>();

            // ---- 半透明ディムオーバーレイ ----
            _dimOverlay = new GameObject("DimOverlay");
            _dimOverlay.transform.SetParent(canvasRt, false);
            var dimRt = _dimOverlay.AddComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            var dimImg = _dimOverlay.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.3f);
            dimImg.raycastTarget = false; // クリック透過

            // ---- メインパネル（画面下部中央） ----
            float panelW = 520f;
            float panelH = 260f;
            _panel = new GameObject("TutorialPanel");
            _panel.transform.SetParent(canvasRt, false);
            var panelRt = _panel.AddComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.anchoredPosition = new Vector2(0f, 30f);
            panelRt.sizeDelta = new Vector2(panelW, panelH);

            _panelBg = _panel.AddComponent<Image>();
            _panelBg.color = PanelBg;
            _panelBg.raycastTarget = true;

            // ---- 上部アクセントライン ----
            var accent = MakePanel(panelRt, "Accent", panelW, 3f, AccentCyan);
            var accentRt = accent.GetComponent<RectTransform>();
            accentRt.anchorMin = accentRt.anchorMax = new Vector2(0.5f, 1f);
            accentRt.pivot = new Vector2(0.5f, 1f);
            accentRt.anchoredPosition = Vector2.zero;

            // ---- ステップインジケーター ----
            _stepIndicator = MakeLabel(panelRt, "StepInd", "", 13, TextMuted,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            var siRt = _stepIndicator.rectTransform;
            siRt.anchorMin = siRt.anchorMax = new Vector2(0f, 1f);
            siRt.pivot = new Vector2(0f, 1f);
            siRt.anchoredPosition = new Vector2(16f, -10f);
            siRt.sizeDelta = new Vector2(200f, 20f);

            // ---- タイトル ----
            _titleText = MakeLabel(panelRt, "Title", "", 26, AccentGold,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            var ttRt = _titleText.rectTransform;
            ttRt.anchorMin = ttRt.anchorMax = new Vector2(0f, 1f);
            ttRt.pivot = new Vector2(0f, 1f);
            ttRt.anchoredPosition = new Vector2(16f, -32f);
            ttRt.sizeDelta = new Vector2(panelW - 32f, 36f);

            // ---- 説明テキスト ----
            _descText = MakeLabel(panelRt, "Desc", "", 17, Color.white,
                FontStyle.Normal, TextAnchor.UpperLeft);
            _descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _descText.verticalOverflow = VerticalWrapMode.Overflow;
            _descText.lineSpacing = 1.4f;
            var descRt = _descText.rectTransform;
            descRt.anchorMin = descRt.anchorMax = new Vector2(0f, 1f);
            descRt.pivot = new Vector2(0f, 1f);
            descRt.anchoredPosition = new Vector2(16f, -72f);
            descRt.sizeDelta = new Vector2(panelW - 32f, 120f);

            // ---- ボタンエリア ----
            float btnY = 16f;
            float btnH = 42f;

            // Nextボタン
            var nextGo = MakePanel(panelRt, "NextBtn", 160f, btnH, BtnGreen);
            var nextRt = nextGo.GetComponent<RectTransform>();
            nextRt.anchorMin = nextRt.anchorMax = new Vector2(1f, 0f);
            nextRt.pivot = new Vector2(1f, 0f);
            nextRt.anchoredPosition = new Vector2(-16f, btnY);
            var nextImg = nextGo.GetComponent<Image>();
            nextImg.raycastTarget = true;
            _nextBtn = nextGo.AddComponent<Button>();
            _nextBtn.targetGraphic = nextImg;
            var nc = _nextBtn.colors;
            nc.highlightedColor = new Color(0.25f, 0.7f, 0.42f);
            nc.pressedColor = new Color(0.15f, 0.48f, 0.28f);
            _nextBtn.colors = nc;
            _nextBtn.onClick.AddListener(OnNextClicked);
            var nextLabel = MakeLabel(nextRt, "Label", "次へ  >>", 20, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(nextLabel.rectTransform);

            // Skipボタン
            var skipGo = MakePanel(panelRt, "SkipBtn", 140f, btnH, BtnGray);
            var skipRt = skipGo.GetComponent<RectTransform>();
            skipRt.anchorMin = skipRt.anchorMax = new Vector2(0f, 0f);
            skipRt.pivot = new Vector2(0f, 0f);
            skipRt.anchoredPosition = new Vector2(16f, btnY);
            var skipImg = skipGo.GetComponent<Image>();
            skipImg.raycastTarget = true;
            _skipBtn = skipGo.AddComponent<Button>();
            _skipBtn.targetGraphic = skipImg;
            var sc = _skipBtn.colors;
            sc.highlightedColor = new Color(0.45f, 0.48f, 0.55f);
            sc.pressedColor = new Color(0.25f, 0.28f, 0.35f);
            _skipBtn.colors = sc;
            _skipBtn.onClick.AddListener(SkipTutorial);
            var skipLabel = MakeLabel(skipRt, "Label", "スキップ", 17, new Color(0.8f, 0.8f, 0.85f),
                FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchFill(skipLabel.rectTransform);

            // ---- アクション待ちヒント（RequiresAction時に表示） ----
            _arrowIndicator = MakePanel(panelRt, "Arrow", panelW - 32f, 24f,
                new Color(0.2f, 0.6f, 0.8f, 0.15f)).GetComponent<Image>();
            var arrowRt = _arrowIndicator.rectTransform;
            arrowRt.anchorMin = arrowRt.anchorMax = new Vector2(0.5f, 0f);
            arrowRt.pivot = new Vector2(0.5f, 0f);
            arrowRt.anchoredPosition = new Vector2(0f, btnY + btnH + 6f);
            var arrowLabel = MakeLabel(arrowRt, "ArrowText",
                ">>> アクションを実行してください <<<", 14,
                AccentCyan, FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchFill(arrowLabel.rectTransform);
        }

        // ================================================================
        // チュートリアル開始/進行
        // ================================================================

        private void OnParkOpenedForTutorial()
        {
            // 初回のパーク開園でチュートリアル開始
            if (_tutorialCompleted) return;
            GameEvents.OnParkOpened -= OnParkOpenedForTutorial;
            StartTutorial();
        }

        /// <summary>チュートリアルを開始する</summary>
        public void StartTutorial()
        {
            if (_tutorialCompleted) return;
            _isActive = true;
            _currentStep = TutorialStep.Welcome;
            ShowCurrentStep();
            Debug.Log("[TutorialSystem] Tutorial started");
        }

        /// <summary>チュートリアルをスキップする</summary>
        public void SkipTutorial()
        {
            _isActive = false;
            _tutorialCompleted = true;
            PlayerPrefs.SetInt(PREF_KEY, 1);
            PlayerPrefs.Save();
            HideUI();
            OnTutorialCompleted?.Invoke();
            Debug.Log("[TutorialSystem] Tutorial skipped");
        }

        /// <summary>チュートリアルリセット（デバッグ用）</summary>
        public static void ResetTutorial()
        {
            PlayerPrefs.DeleteKey(PREF_KEY);
            PlayerPrefs.Save();
            Debug.Log("[TutorialSystem] Tutorial reset");
        }

        /// <summary>次のステップに進む</summary>
        public void AdvanceToNextStep()
        {
            if (!_isActive) return;

            OnStepCompleted?.Invoke(_currentStep);

            int nextIndex = (int)_currentStep + 1;
            var values = Enum.GetValues(typeof(TutorialStep));
            if (nextIndex >= values.Length)
            {
                CompleteTutorial();
                return;
            }

            var nextStep = (TutorialStep)nextIndex;
            if (nextStep == TutorialStep.Completed)
            {
                CompleteTutorial();
                return;
            }

            _currentStep = nextStep;
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
            PlayerPrefs.SetInt(PREF_KEY, 1);
            PlayerPrefs.Save();

            // 完了ステップを表示後、数秒で自動的に閉じる
            _autoHideTimer = 5f;

            OnTutorialCompleted?.Invoke();
            Debug.Log("[TutorialSystem] Tutorial completed!");
        }

        // ================================================================
        // UI表示
        // ================================================================

        private void ShowCurrentStep()
        {
            var stepData = GetStepData(_currentStep);
            if (stepData == null) return;

            if (_tutorialCanvas != null)
                _tutorialCanvas.gameObject.SetActive(true);
            if (_panel != null)
                _panel.SetActive(true);
            if (_dimOverlay != null)
                _dimOverlay.SetActive(true);

            // ステップインジケーター
            int totalSteps = Enum.GetValues(typeof(TutorialStep)).Length - 1; // Completedを除く
            int currentNum = (int)_currentStep + 1;
            if (_stepIndicator != null)
                _stepIndicator.text = $"STEP {currentNum}/{totalSteps}";

            if (_titleText != null)
                _titleText.text = stepData.Title;
            if (_descText != null)
                _descText.text = stepData.Description;

            // Nextボタン: アクション必要なステップでは非表示
            if (_nextBtn != null)
            {
                bool isCompleted = stepData.Step == TutorialStep.Completed;
                _nextBtn.gameObject.SetActive(!stepData.RequiresAction || isCompleted);

                // 完了時はボタンラベルを変更
                if (isCompleted)
                {
                    var label = _nextBtn.GetComponentInChildren<Text>();
                    if (label != null) label.text = "閉じる";
                }
                else
                {
                    var label = _nextBtn.GetComponentInChildren<Text>();
                    if (label != null) label.text = "次へ  >>";
                }
            }

            // Skipボタン: 完了時は非表示
            if (_skipBtn != null)
                _skipBtn.gameObject.SetActive(stepData.Step != TutorialStep.Completed);

            // アクション待ちインジケーター
            if (_arrowIndicator != null)
                _arrowIndicator.gameObject.SetActive(stepData.RequiresAction);

            _pulseTimer = 0f;
        }

        private void HideUI()
        {
            if (_tutorialCanvas != null)
                _tutorialCanvas.gameObject.SetActive(false);
        }

        // ================================================================
        // ボタンハンドラ
        // ================================================================

        private void OnNextClicked()
        {
            if (_currentStep == TutorialStep.Completed)
            {
                _isActive = false;
                HideUI();
                return;
            }
            AdvanceToNextStep();
        }

        // ================================================================
        // Update
        // ================================================================

        private void Update()
        {
            if (!_isActive) return;

            // ゲームがまだメニュー状態ならUI非表示
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.MainMenu)
            {
                if (_tutorialCanvas != null && _tutorialCanvas.gameObject.activeSelf)
                    _tutorialCanvas.gameObject.SetActive(false);
                return;
            }

            // ポーズ/ゲームオーバー時はUI非表示
            if (GameManager.Instance != null &&
                (GameManager.Instance.CurrentState == GameState.Paused ||
                 GameManager.Instance.CurrentState == GameState.GameOver))
            {
                if (_panel != null && _panel.activeSelf)
                    _panel.SetActive(false);
                if (_dimOverlay != null && _dimOverlay.activeSelf)
                    _dimOverlay.SetActive(false);
                return;
            }

            // Playing時に復帰
            if (_panel != null && !_panel.activeSelf && _tutorialCanvas.gameObject.activeSelf)
            {
                _panel.SetActive(true);
                if (_dimOverlay != null) _dimOverlay.SetActive(true);
            }

            // 完了後の自動非表示
            if (_autoHideTimer > 0f)
            {
                _autoHideTimer -= Time.unscaledDeltaTime;
                if (_autoHideTimer <= 0f)
                {
                    _isActive = false;
                    HideUI();
                }
            }

            // アクション待ちインジケーターのパルス
            if (_arrowIndicator != null && _arrowIndicator.gameObject.activeSelf)
            {
                _pulseTimer += Time.unscaledDeltaTime * 2f;
                float alpha = 0.1f + 0.15f * Mathf.Sin(_pulseTimer);
                var c = _arrowIndicator.color;
                c.a = alpha;
                _arrowIndicator.color = c;
            }
        }

        // ================================================================
        // データアクセス
        // ================================================================

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
            GameEvents.OnVisitorSelected += OnVisitorSelected;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnAttractionBuilt -= OnAttractionBuilt;
            GameEvents.OnStaffHired -= OnStaffHired;
            GameEvents.OnVisitorSelected -= OnVisitorSelected;
        }

        private void OnAttractionBuilt(int id)
        {
            NotifyActionCompleted(TutorialStep.BuildFirstAttraction);
        }

        private void OnStaffHired(int staffId, StaffType type)
        {
            NotifyActionCompleted(TutorialStep.HireStaff);
        }

        private void OnVisitorSelected(int visitorId)
        {
            NotifyActionCompleted(TutorialStep.CheckVisitor);
        }

        // ================================================================
        // UIヘルパー（RuntimeHUDと同パターン）
        // ================================================================

        private static Font _font;

        private static Font CachedFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        private static GameObject MakePanel(RectTransform parent, string name, float w, float h, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.color = bg;
            img.raycastTarget = false;
            return go;
        }

        private static Text MakeLabel(RectTransform parent, string name, string content,
            int fontSize, Color color, FontStyle style, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var t = go.AddComponent<Text>();
            t.text = content;
            t.font = CachedFont();
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
