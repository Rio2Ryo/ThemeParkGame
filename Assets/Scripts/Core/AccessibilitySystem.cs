// ============================================================
// ThemeParkGame - AccessibilitySystem
// アクセシビリティ改善 - キーボード操作・ハイコントラスト・色覚フィルター・
// フラッシュ軽減・テキストサイズ3段階・ナレーションログ
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>色覚多様性モード</summary>
    public enum ColorVisionMode
    {
        None,          // フィルターなし
        Protanopia,    // 1型色覚（赤色覚異常）
        Deuteranopia,  // 2型色覚（緑色覚異常）
        Tritanopia     // 3型色覚（青色覚異常）
    }

    /// <summary>
    /// アクセシビリティ機能を提供するシステム。
    ///
    /// 【機能】
    /// ・ハイコントラストモード: UI背景を濃くし、テキストを白/黄に統一
    /// ・テキストサイズ3段階: 通常(1.0x) / 大(1.3x) / 特大(1.6x)
    /// ・色覚多様性対応: Protanopia/Deuteranopia/Tritanopia用カラーフィルター
    /// ・画面フラッシュ軽減: 雷雨時のホワイトフラッシュを無効化
    /// ・ナレーションログ: ゲームイベントのテキスト読み上げ表示
    /// ・キーボードショートカット: 主要操作をキーボードで完結
    ///   - Escape: ポーズ/再開
    ///   - 1-4: ゲーム速度切り替え
    ///   - F: 天気予報パネル
    ///   - E: イベントパネル
    ///   - C: チャレンジパネル
    ///   - N: シェアパネル
    ///   - R: ナレーションログ
    ///   - Tab: 次のUIパネル
    ///   - F12: スクリーンショット
    /// </summary>
    public class AccessibilitySystem : MonoBehaviour
    {
        // ============================================================
        // 設定
        // ============================================================

        private bool _highContrastEnabled;
        private int _textSizeLevel; // 0=通常, 1=大, 2=特大
        private bool _keyboardNavEnabled = true;
        private float _fontScale = 1f;
        private ColorVisionMode _colorVisionMode = ColorVisionMode.None;
        private bool _reduceFlashEnabled;
        private bool _narrationEnabled;

        // テキストサイズ設定テーブル
        private static readonly float[] TextSizeScales = { 1.0f, 1.3f, 1.6f };
        private static readonly string[] TextSizeLabels = { "通常", "大", "特大" };

        // ハイコントラスト復元用: 変更前の値を保存
        private readonly Dictionary<int, float> _originalImageAlphas = new Dictionary<int, float>();
        private readonly Dictionary<int, Color> _originalTextColors = new Dictionary<int, Color>();
        private readonly Dictionary<int, int> _originalFontSizes = new Dictionary<int, int>();

        // 色覚フィルターオーバーレイ
        private Canvas _cvdCanvas;
        private Image _cvdImage;

        // ナレーションログ
        private readonly List<string> _narrationLog = new List<string>();
        private const int MaxNarrationEntries = 30;
        private GameObject _narrationPanel;
        private Text _narrationText;
        private bool _narrationPanelVisible;

        // 設定UI
        private GameObject _settingsPanel;
        private Text _contrastLabel;
        private Text _textSizeLabel;
        private Text _colorVisionLabel;
        private Text _reduceFlashLabel;
        private Text _narrationLabel;
        private Text _keyboardLabel;
        private bool _visible;

        public static AccessibilitySystem Instance { get; private set; }

        // プロパティ
        public bool HighContrastEnabled => _highContrastEnabled;
        public int TextSizeLevel => _textSizeLevel;
        public float FontScale => _fontScale;
        public ColorVisionMode CurrentColorVisionMode => _colorVisionMode;
        public bool ReduceFlashEnabled => _reduceFlashEnabled;
        public bool NarrationEnabled => _narrationEnabled;

        // 後方互換
        public bool LargeFontEnabled => _textSizeLevel > 0;

        // ============================================================
        // ライフサイクル
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // PlayerPrefsから設定復元
            _highContrastEnabled = PlayerPrefs.GetInt("Accessibility_HighContrast", 0) == 1;
            _keyboardNavEnabled = PlayerPrefs.GetInt("Accessibility_Keyboard", 1) == 1;
            _reduceFlashEnabled = PlayerPrefs.GetInt("Accessibility_ReduceFlash", 0) == 1;
            _narrationEnabled = PlayerPrefs.GetInt("Accessibility_Narration", 0) == 1;
            _colorVisionMode = (ColorVisionMode)Mathf.Clamp(
                PlayerPrefs.GetInt("Accessibility_ColorVision", 0), 0, 3);

            // テキストサイズ: 新形式優先、旧形式からの移行
            if (PlayerPrefs.HasKey("Accessibility_TextSizeLevel"))
            {
                _textSizeLevel = Mathf.Clamp(PlayerPrefs.GetInt("Accessibility_TextSizeLevel", 0), 0, 2);
            }
            else if (PlayerPrefs.GetInt("Accessibility_LargeFont", 0) == 1)
            {
                _textSizeLevel = 1; // 旧設定からの移行
            }
            _fontScale = TextSizeScales[_textSizeLevel];

            if (_highContrastEnabled) ApplyHighContrast();
            if (_textSizeLevel > 0) ApplyFontScale();

            CreateCVDOverlay();
            ApplyColorVisionFilter();

            // ナレーション用イベント購読
            GameEvents.OnWeatherChanged += OnWeatherChangedNarration;
            GameEvents.OnAttractionBrokenDown += OnAttractionBrokenNarration;
            GameEvents.OnAttractionRepaired += OnAttractionRepairedNarration;
            GameEvents.OnVIPArrived += OnVIPArrivedNarration;
            GameEvents.OnParkEventStarted += OnParkEventStartedNarration;
            GameEvents.OnParkEventEnded += OnParkEventEndedNarration;
            GameEvents.OnResearchCompleted += OnResearchCompletedNarration;
            GameEvents.OnAccidentOccurred += OnAccidentOccurredNarration;
        }

        private void Update()
        {
            if (!_keyboardNavEnabled) return;

            // キーボードショートカット
            if (GameManager.Instance == null) return;

            // Escape: ポーズ/再開
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (GameManager.Instance.IsPaused)
                    GameManager.Instance.ResumeGame();
                else if (GameManager.Instance.CurrentState == GameState.Playing)
                    GameManager.Instance.PauseGame();
            }

            // ゲームプレイ中のショートカット
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            // 速度切り替え: 1-4
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                GameManager.Instance.SpeedLevel = 0; // ポーズ
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                GameManager.Instance.SpeedLevel = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                GameManager.Instance.SpeedLevel = 2;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                GameManager.Instance.SpeedLevel = 5;

            // パネルトグル
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (UI.WeatherForecastUI.Instance != null)
                    UI.WeatherForecastUI.Instance.ToggleUI();
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (UI.SpecialEventUI.Instance != null)
                    UI.SpecialEventUI.Instance.ToggleUI();
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (ChallengeSystem.Instance != null)
                    ChallengeSystem.Instance.ToggleUI();
            }
            if (Input.GetKeyDown(KeyCode.N))
            {
                if (SocialShareSystem.Instance != null)
                    SocialShareSystem.Instance.ToggleUI();
            }

            // R: ナレーションログ表示トグル
            if (Input.GetKeyDown(KeyCode.R) && _narrationEnabled)
            {
                ToggleNarrationPanel();
            }

            // F12: スクリーンショット
            if (Input.GetKeyDown(KeyCode.F12))
            {
                if (SocialShareSystem.Instance != null)
                    SocialShareSystem.Instance.CaptureScreenshot();
            }

            // スペース: 速度トグル（0↔1）
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (GameManager.Instance.SpeedLevel == 0)
                    GameManager.Instance.SpeedLevel = 1;
                else
                    GameManager.Instance.SpeedLevel = 0;
            }
        }

        // ============================================================
        // 設定切り替え
        // ============================================================

        public void ToggleHighContrast()
        {
            _highContrastEnabled = !_highContrastEnabled;
            PlayerPrefs.SetInt("Accessibility_HighContrast", _highContrastEnabled ? 1 : 0);
            PlayerPrefs.Save();

            if (_highContrastEnabled) ApplyHighContrast();
            else RemoveHighContrast();

            RefreshSettingsUI();
        }

        public void CycleTextSize()
        {
            _textSizeLevel = (_textSizeLevel + 1) % 3;
            _fontScale = TextSizeScales[_textSizeLevel];
            PlayerPrefs.SetInt("Accessibility_TextSizeLevel", _textSizeLevel);
            PlayerPrefs.Save();

            ApplyFontScale();
            RefreshSettingsUI();
        }

        /// <summary>後方互換: 旧APIのToggleLargeFontをCycleTextSizeに転送</summary>
        public void ToggleLargeFont()
        {
            CycleTextSize();
        }

        public void CycleColorVisionMode()
        {
            int next = ((int)_colorVisionMode + 1) % 4;
            _colorVisionMode = (ColorVisionMode)next;
            PlayerPrefs.SetInt("Accessibility_ColorVision", next);
            PlayerPrefs.Save();

            ApplyColorVisionFilter();
            RefreshSettingsUI();
        }

        public void ToggleReduceFlash()
        {
            _reduceFlashEnabled = !_reduceFlashEnabled;
            PlayerPrefs.SetInt("Accessibility_ReduceFlash", _reduceFlashEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSettingsUI();

            AddNarration(_reduceFlashEnabled
                ? "画面フラッシュ軽減: 有効 — 雷雨時のホワイトフラッシュを抑制します"
                : "画面フラッシュ軽減: 無効");
        }

        public void ToggleNarration()
        {
            _narrationEnabled = !_narrationEnabled;
            PlayerPrefs.SetInt("Accessibility_Narration", _narrationEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSettingsUI();

            if (_narrationEnabled)
                AddNarration("ナレーションログ: 有効 — Rキーでログ表示/非表示");
        }

        public void ToggleKeyboardNav()
        {
            _keyboardNavEnabled = !_keyboardNavEnabled;
            PlayerPrefs.SetInt("Accessibility_Keyboard", _keyboardNavEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSettingsUI();
        }

        // ============================================================
        // ハイコントラストモード
        // ============================================================

        private void ApplyHighContrast()
        {
            var images = FindObjectsOfType<Image>();
            foreach (var img in images)
            {
                if (img.color.a < 0.5f && img.color.a > 0.1f)
                {
                    int id = img.GetInstanceID();
                    if (!_originalImageAlphas.ContainsKey(id))
                    {
                        _originalImageAlphas[id] = img.color.a;
                    }
                    Color c = img.color;
                    c.a = Mathf.Max(c.a, 0.9f);
                    img.color = c;
                }
            }

            var texts = FindObjectsOfType<Text>();
            foreach (var t in texts)
            {
                if (t.color.r < 0.5f && t.color.g < 0.5f && t.color.b < 0.5f)
                {
                    int id = t.GetInstanceID();
                    if (!_originalTextColors.ContainsKey(id))
                    {
                        _originalTextColors[id] = t.color;
                    }
                    t.color = Color.white;
                }
            }

            WebGLOptimizer.LogVerbose("[Accessibility] ハイコントラストモード有効");
        }

        private void RemoveHighContrast()
        {
            var images = FindObjectsOfType<Image>();
            foreach (var img in images)
            {
                int id = img.GetInstanceID();
                if (_originalImageAlphas.TryGetValue(id, out float originalAlpha))
                {
                    Color c = img.color;
                    c.a = originalAlpha;
                    img.color = c;
                }
            }

            var texts = FindObjectsOfType<Text>();
            foreach (var t in texts)
            {
                int id = t.GetInstanceID();
                if (_originalTextColors.TryGetValue(id, out Color originalColor))
                {
                    t.color = originalColor;
                }
            }

            _originalImageAlphas.Clear();
            _originalTextColors.Clear();

            WebGLOptimizer.LogVerbose("[Accessibility] ハイコントラストモード無効");
        }

        private void ApplyFontScale()
        {
            var texts = FindObjectsOfType<Text>();
            foreach (var t in texts)
            {
                int id = t.GetInstanceID();

                if (!_originalFontSizes.ContainsKey(id))
                {
                    _originalFontSizes[id] = t.fontSize;
                }

                int originalSize = _originalFontSizes[id];

                if (originalSize <= 16)
                {
                    t.fontSize = _textSizeLevel > 0
                        ? Mathf.RoundToInt(originalSize * _fontScale)
                        : originalSize;
                }
            }
            WebGLOptimizer.LogVerbose($"[Accessibility] テキストサイズ: {TextSizeLabels[_textSizeLevel]} ({_fontScale:F1}x)");
        }

        // ============================================================
        // 色覚多様性フィルター
        // ============================================================

        private void CreateCVDOverlay()
        {
            var go = new GameObject("CVDFilterOverlay");
            go.transform.SetParent(transform);

            _cvdCanvas = go.AddComponent<Canvas>();
            _cvdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _cvdCanvas.sortingOrder = 44; // 天候オーバーレイ(45)より下

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var imgGo = new GameObject("CVDTint");
            imgGo.transform.SetParent(go.transform, false);
            _cvdImage = imgGo.AddComponent<Image>();
            _cvdImage.color = new Color(0f, 0f, 0f, 0f);
            _cvdImage.raycastTarget = false;

            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void ApplyColorVisionFilter()
        {
            if (_cvdImage == null) return;

            switch (_colorVisionMode)
            {
                case ColorVisionMode.None:
                    _cvdImage.color = new Color(0f, 0f, 0f, 0f);
                    break;
                case ColorVisionMode.Protanopia:
                    // 赤色覚異常: 暖色域の区別を補助するシアン系フィルター
                    _cvdImage.color = new Color(0.0f, 0.3f, 0.5f, 0.1f);
                    break;
                case ColorVisionMode.Deuteranopia:
                    // 緑色覚異常: 緑域の区別を補助するマゼンタ系フィルター
                    _cvdImage.color = new Color(0.5f, 0.0f, 0.4f, 0.1f);
                    break;
                case ColorVisionMode.Tritanopia:
                    // 青色覚異常: 青黄域の区別を補助するアンバー系フィルター
                    _cvdImage.color = new Color(0.5f, 0.4f, 0.0f, 0.08f);
                    break;
            }

            WebGLOptimizer.LogVerbose($"[Accessibility] 色覚フィルター: {GetColorVisionLabel()}");
        }

        private string GetColorVisionLabel()
        {
            switch (_colorVisionMode)
            {
                case ColorVisionMode.Protanopia: return "P型(赤)";
                case ColorVisionMode.Deuteranopia: return "D型(緑)";
                case ColorVisionMode.Tritanopia: return "T型(青)";
                default: return "なし";
            }
        }

        // ============================================================
        // ナレーションログ
        // ============================================================

        /// <summary>ナレーションログにエントリを追加する（外部システムからも呼び出し可能）</summary>
        public void AddNarration(string message)
        {
            if (!_narrationEnabled) return;

            string timestamp = GameManager.Instance != null && GameManager.Instance.TimeManager != null
                ? $"Day{GameManager.Instance.TimeManager.CurrentDay} {GameManager.Instance.TimeManager.CurrentHour:F0}:00"
                : System.DateTime.Now.ToString("HH:mm:ss");

            string entry = $"[{timestamp}] {message}";
            _narrationLog.Add(entry);
            if (_narrationLog.Count > MaxNarrationEntries)
                _narrationLog.RemoveAt(0);

            if (_narrationPanelVisible && _narrationText != null)
                UpdateNarrationText();
        }

        private void OnWeatherChangedNarration(Weather weather)
        {
            string name;
            switch (weather)
            {
                case Weather.Sunny: name = "晴れ"; break;
                case Weather.Cloudy: name = "曇り"; break;
                case Weather.Rainy: name = "雨"; break;
                case Weather.Snowy: name = "雪"; break;
                case Weather.Hot: name = "猛暑"; break;
                case Weather.Typhoon: name = "台風"; break;
                case Weather.Thunderstorm: name = "雷雨"; break;
                default: name = weather.ToString(); break;
            }
            AddNarration($"天候変化: {name}");
        }

        private void OnAttractionBrokenNarration(int id) =>
            AddNarration($"アトラクション(ID:{id})が故障しました");

        private void OnAttractionRepairedNarration(int id) =>
            AddNarration($"アトラクション(ID:{id})の修理が完了しました");

        private void OnVIPArrivedNarration(int id) =>
            AddNarration($"VIPゲスト(ID:{id})が来場しました");

        private void OnParkEventStartedNarration(string eventId, string displayName) =>
            AddNarration($"イベント開始: {displayName}");

        private void OnParkEventEndedNarration(string eventId, string displayName) =>
            AddNarration($"イベント終了: {displayName}");

        private void OnResearchCompletedNarration(string researchId) =>
            AddNarration($"研究完了: {researchId}");

        private void OnAccidentOccurredNarration(int id, AccidentType type) =>
            AddNarration($"事故発生(ID:{id}): {type}");

        private void ToggleNarrationPanel()
        {
            _narrationPanelVisible = !_narrationPanelVisible;
            if (_narrationPanel == null) BuildNarrationPanel();
            _narrationPanel.SetActive(_narrationPanelVisible);

            if (_narrationPanelVisible) UpdateNarrationText();
        }

        private void BuildNarrationPanel()
        {
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            _narrationPanel = new GameObject("NarrationPanel");
            _narrationPanel.transform.SetParent(canvas.transform, false);
            var rt = _narrationPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.6f, 0.05f);
            rt.anchorMax = new Vector2(0.98f, 0.45f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _narrationPanel.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.08f, 0.92f);

            // タイトル
            MakeText(_narrationPanel.transform, "NarTitle",
                new Vector2(0.03f, 0.90f), new Vector2(0.7f, 1f),
                "ナレーションログ [R]", 16, FontStyle.Bold, new Color(0.6f, 0.9f, 0.7f));

            // 閉じる
            MakeBtn(_narrationPanel.transform, "NarClose",
                new Vector2(0.88f, 0.92f), new Vector2(0.97f, 0.99f),
                "X", new Color(0.6f, 0.15f, 0.15f),
                () => { _narrationPanelVisible = false; _narrationPanel.SetActive(false); });

            // ログテキスト
            var logObj = MakeText(_narrationPanel.transform, "NarLog",
                new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.89f),
                "", 12, FontStyle.Normal, new Color(0.8f, 0.85f, 0.9f));
            _narrationText = logObj.GetComponent<Text>();
            _narrationText.alignment = TextAnchor.LowerLeft;

            _narrationPanel.SetActive(false);
        }

        private void UpdateNarrationText()
        {
            if (_narrationText == null) return;

            int startIdx = Mathf.Max(0, _narrationLog.Count - 15);
            var sb = new System.Text.StringBuilder();
            for (int i = startIdx; i < _narrationLog.Count; i++)
            {
                sb.AppendLine(_narrationLog[i]);
            }
            _narrationText.text = sb.ToString();
        }

        // ============================================================
        // 設定UI
        // ============================================================

        public void ToggleUI()
        {
            if (_visible) HideUI();
            else ShowUI();
        }

        public void ShowUI()
        {
            if (_settingsPanel == null) BuildSettingsUI();
            RefreshSettingsUI();
            _settingsPanel.SetActive(true);
            _visible = true;
        }

        public void HideUI()
        {
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            _visible = false;
        }

        private void BuildSettingsUI()
        {
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            _settingsPanel = new GameObject("AccessibilityPanel");
            _settingsPanel.transform.SetParent(canvas.transform, false);
            var rt = _settingsPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.08f);
            rt.anchorMax = new Vector2(0.8f, 0.92f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _settingsPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);

            // タイトル
            MakeText(_settingsPanel.transform, "Title",
                new Vector2(0.02f, 0.93f), new Vector2(0.8f, 1f),
                "アクセシビリティ設定", 22, FontStyle.Bold, Color.white);

            // 閉じる
            MakeBtn(_settingsPanel.transform, "Close",
                new Vector2(0.9f, 0.94f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            float rowH = 0.07f;
            float gap = 0.005f;
            float y;

            // 1. ハイコントラスト
            y = 0.84f;
            _contrastLabel = MakeText(_settingsPanel.transform, "ContrastLabel",
                new Vector2(0.05f, y), new Vector2(0.6f, y + rowH),
                "", 15, FontStyle.Normal, Color.white).GetComponent<Text>();
            MakeBtn(_settingsPanel.transform, "ContrastBtn",
                new Vector2(0.65f, y + 0.01f), new Vector2(0.92f, y + rowH - 0.01f),
                "切替", new Color(0.3f, 0.4f, 0.5f), ToggleHighContrast);

            // 2. テキストサイズ（3段階サイクル）
            y -= (rowH + gap);
            _textSizeLabel = MakeText(_settingsPanel.transform, "TextSizeLabel",
                new Vector2(0.05f, y), new Vector2(0.6f, y + rowH),
                "", 15, FontStyle.Normal, Color.white).GetComponent<Text>();
            MakeBtn(_settingsPanel.transform, "TextSizeBtn",
                new Vector2(0.65f, y + 0.01f), new Vector2(0.92f, y + rowH - 0.01f),
                "切替", new Color(0.3f, 0.4f, 0.5f), CycleTextSize);

            // 3. 色覚フィルター（4モードサイクル）
            y -= (rowH + gap);
            _colorVisionLabel = MakeText(_settingsPanel.transform, "ColorVisionLabel",
                new Vector2(0.05f, y), new Vector2(0.6f, y + rowH),
                "", 15, FontStyle.Normal, Color.white).GetComponent<Text>();
            MakeBtn(_settingsPanel.transform, "ColorVisionBtn",
                new Vector2(0.65f, y + 0.01f), new Vector2(0.92f, y + rowH - 0.01f),
                "切替", new Color(0.3f, 0.4f, 0.5f), CycleColorVisionMode);

            // 4. 画面フラッシュ軽減
            y -= (rowH + gap);
            _reduceFlashLabel = MakeText(_settingsPanel.transform, "ReduceFlashLabel",
                new Vector2(0.05f, y), new Vector2(0.6f, y + rowH),
                "", 15, FontStyle.Normal, Color.white).GetComponent<Text>();
            MakeBtn(_settingsPanel.transform, "ReduceFlashBtn",
                new Vector2(0.65f, y + 0.01f), new Vector2(0.92f, y + rowH - 0.01f),
                "切替", new Color(0.3f, 0.4f, 0.5f), ToggleReduceFlash);

            // 5. ナレーション
            y -= (rowH + gap);
            _narrationLabel = MakeText(_settingsPanel.transform, "NarrationLabel",
                new Vector2(0.05f, y), new Vector2(0.6f, y + rowH),
                "", 15, FontStyle.Normal, Color.white).GetComponent<Text>();
            MakeBtn(_settingsPanel.transform, "NarrationBtn",
                new Vector2(0.65f, y + 0.01f), new Vector2(0.92f, y + rowH - 0.01f),
                "切替", new Color(0.3f, 0.4f, 0.5f), ToggleNarration);

            // 6. キーボード操作
            y -= (rowH + gap);
            _keyboardLabel = MakeText(_settingsPanel.transform, "KeyboardLabel",
                new Vector2(0.05f, y), new Vector2(0.6f, y + rowH),
                "", 15, FontStyle.Normal, Color.white).GetComponent<Text>();
            MakeBtn(_settingsPanel.transform, "KeyboardBtn",
                new Vector2(0.65f, y + 0.01f), new Vector2(0.92f, y + rowH - 0.01f),
                "切替", new Color(0.3f, 0.4f, 0.5f), ToggleKeyboardNav);

            // ショートカット一覧
            MakeText(_settingsPanel.transform, "ShortcutsTitle",
                new Vector2(0.05f, 0.26f), new Vector2(0.95f, 0.33f),
                "--- キーボードショートカット ---", 14, FontStyle.Bold, new Color(1f, 0.9f, 0.5f));

            MakeText(_settingsPanel.transform, "Shortcuts",
                new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.26f),
                "Escape  : 一時停止 / 再開\n" +
                "Space   : 一時停止切替\n" +
                "1-4     : 速度 (||, x1, x2, x5)\n" +
                "F       : 天気予報\n" +
                "E       : イベントパネル\n" +
                "C       : チャレンジ\n" +
                "N       : シェア\n" +
                "R       : ナレーションログ\n" +
                "F12     : スクリーンショット",
                13, FontStyle.Normal, new Color(0.7f, 0.7f, 0.8f));

            _settingsPanel.SetActive(false);
        }

        private void RefreshSettingsUI()
        {
            if (_contrastLabel != null)
                _contrastLabel.text = $"ハイコントラスト: {(_highContrastEnabled ? "ON" : "OFF")}";
            if (_textSizeLabel != null)
                _textSizeLabel.text = $"テキストサイズ: {TextSizeLabels[_textSizeLevel]} ({_fontScale:F1}x)";
            if (_colorVisionLabel != null)
                _colorVisionLabel.text = $"色覚フィルター: {GetColorVisionLabel()}";
            if (_reduceFlashLabel != null)
                _reduceFlashLabel.text = $"画面フラッシュ軽減: {(_reduceFlashEnabled ? "ON" : "OFF")}";
            if (_narrationLabel != null)
                _narrationLabel.text = $"ナレーション: {(_narrationEnabled ? "ON" : "OFF")}";
            if (_keyboardLabel != null)
                _keyboardLabel.text = $"キーボード操作: {(_keyboardNavEnabled ? "ON" : "OFF")}";
        }

        // ============================================================
        // UIヘルパー
        // ============================================================

        private GameObject MakeText(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string content, int size, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = new Vector2(5f, 0f);
            r.offsetMax = new Vector2(-5f, 0f);
            var t = obj.AddComponent<Text>();
            t.text = content;
            t.font = FontManager.Regular;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            return obj;
        }

        private void MakeBtn(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string text, Color bg,
            UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var r = obj.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>();
            img.color = bg;
            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var txt = new GameObject("Text").AddComponent<Text>();
            txt.transform.SetParent(obj.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            txt.font = FontManager.Regular;
            txt.fontSize = 14; txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = text;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // イベント購読解除
            GameEvents.OnWeatherChanged -= OnWeatherChangedNarration;
            GameEvents.OnAttractionBrokenDown -= OnAttractionBrokenNarration;
            GameEvents.OnAttractionRepaired -= OnAttractionRepairedNarration;
            GameEvents.OnVIPArrived -= OnVIPArrivedNarration;
            GameEvents.OnParkEventStarted -= OnParkEventStartedNarration;
            GameEvents.OnParkEventEnded -= OnParkEventEndedNarration;
            GameEvents.OnResearchCompleted -= OnResearchCompletedNarration;
            GameEvents.OnAccidentOccurred -= OnAccidentOccurredNarration;
        }
    }
}
