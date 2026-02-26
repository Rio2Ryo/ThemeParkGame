// ============================================================
// ThemeParkGame - AccessibilitySystem
// アクセシビリティ改善 - キーボード操作完全対応・ハイコントラストモード
// ============================================================

using UnityEngine;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// アクセシビリティ機能を提供するシステム。
    ///
    /// 【機能】
    /// ・ハイコントラストモード: UI背景を濃くし、テキストを白/黄に統一
    /// ・大きいフォントモード: UIテキストサイズを1.5倍に拡大
    /// ・キーボードショートカット: 主要操作をキーボードで完結
    ///   - Escape: ポーズ/再開
    ///   - 1-5: ゲーム速度切り替え
    ///   - F: 天気予報パネル
    ///   - E: イベントパネル
    ///   - C: チャレンジパネル
    ///   - B: 建設パネル
    ///   - S: シェアパネル
    ///   - Tab: 次のUIパネル
    ///   - F12: スクリーンショット
    /// </summary>
    public class AccessibilitySystem : MonoBehaviour
    {
        // ============================================================
        // 設定
        // ============================================================

        private bool _highContrastEnabled;
        private bool _largeFontEnabled;
        private bool _keyboardNavEnabled = true;
        private float _fontScale = 1f;

        // UI
        private GameObject _settingsPanel;
        private Text _contrastLabel;
        private Text _fontLabel;
        private Text _keyboardLabel;
        private bool _visible;

        public static AccessibilitySystem Instance { get; private set; }

        // プロパティ
        public bool HighContrastEnabled => _highContrastEnabled;
        public bool LargeFontEnabled => _largeFontEnabled;
        public float FontScale => _fontScale;

        // ============================================================
        // ライフサイクル
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // PlayerPrefsから設定復元
            _highContrastEnabled = PlayerPrefs.GetInt("Accessibility_HighContrast", 0) == 1;
            _largeFontEnabled = PlayerPrefs.GetInt("Accessibility_LargeFont", 0) == 1;
            _keyboardNavEnabled = PlayerPrefs.GetInt("Accessibility_Keyboard", 1) == 1;
            _fontScale = _largeFontEnabled ? 1.5f : 1f;

            if (_highContrastEnabled) ApplyHighContrast();
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

            // 速度切り替え: 1-5
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

        public void ToggleLargeFont()
        {
            _largeFontEnabled = !_largeFontEnabled;
            _fontScale = _largeFontEnabled ? 1.5f : 1f;
            PlayerPrefs.SetInt("Accessibility_LargeFont", _largeFontEnabled ? 1 : 0);
            PlayerPrefs.Save();

            ApplyFontScale();
            RefreshSettingsUI();
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
            // 全Canvasの背景色を濃くする
            var images = FindObjectsOfType<Image>();
            foreach (var img in images)
            {
                if (img.color.a < 0.5f && img.color.a > 0.1f)
                {
                    Color c = img.color;
                    c.a = Mathf.Max(c.a, 0.9f);
                    img.color = c;
                }
            }

            // テキストの色をハイコントラストに
            var texts = FindObjectsOfType<Text>();
            foreach (var t in texts)
            {
                if (t.color.r < 0.5f && t.color.g < 0.5f && t.color.b < 0.5f)
                {
                    t.color = Color.white;
                }
            }

            WebGLOptimizer.LogVerbose("[Accessibility] ハイコントラストモード有効");
        }

        private void RemoveHighContrast()
        {
            WebGLOptimizer.LogVerbose("[Accessibility] ハイコントラストモード無効");
        }

        private void ApplyFontScale()
        {
            var texts = FindObjectsOfType<Text>();
            foreach (var t in texts)
            {
                // パネルタイトルなど大きいフォント以外をスケール
                if (t.fontSize <= 16)
                {
                    t.fontSize = Mathf.RoundToInt(t.fontSize * (_largeFontEnabled ? 1.3f : 1f / 1.3f));
                }
            }
            WebGLOptimizer.LogVerbose($"[Accessibility] フォントスケール: {_fontScale:F1}x");
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
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _settingsPanel = new GameObject("AccessibilityPanel");
            _settingsPanel.transform.SetParent(canvas.transform, false);
            var rt = _settingsPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.25f, 0.2f);
            rt.anchorMax = new Vector2(0.75f, 0.8f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _settingsPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.96f);

            // タイトル
            MakeText(_settingsPanel.transform, "Title",
                new Vector2(0.02f, 0.88f), new Vector2(0.8f, 1f),
                "Accessibility Settings", 22, FontStyle.Bold, Color.white);

            // 閉じる
            MakeBtn(_settingsPanel.transform, "Close",
                new Vector2(0.9f, 0.9f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            // ハイコントラスト
            float y = 0.72f;
            _contrastLabel = MakeText(_settingsPanel.transform, "ContrastLabel",
                new Vector2(0.05f, y), new Vector2(0.6f, y + 0.1f),
                "", 16, FontStyle.Normal, Color.white).GetComponent<Text>();
            MakeBtn(_settingsPanel.transform, "ContrastBtn",
                new Vector2(0.65f, y + 0.02f), new Vector2(0.92f, y + 0.08f),
                "Toggle", new Color(0.3f, 0.4f, 0.5f), ToggleHighContrast);

            // 大きいフォント
            y = 0.58f;
            _fontLabel = MakeText(_settingsPanel.transform, "FontLabel",
                new Vector2(0.05f, y), new Vector2(0.6f, y + 0.1f),
                "", 16, FontStyle.Normal, Color.white).GetComponent<Text>();
            MakeBtn(_settingsPanel.transform, "FontBtn",
                new Vector2(0.65f, y + 0.02f), new Vector2(0.92f, y + 0.08f),
                "Toggle", new Color(0.3f, 0.4f, 0.5f), ToggleLargeFont);

            // キーボードナビゲーション
            y = 0.44f;
            _keyboardLabel = MakeText(_settingsPanel.transform, "KeyboardLabel",
                new Vector2(0.05f, y), new Vector2(0.6f, y + 0.1f),
                "", 16, FontStyle.Normal, Color.white).GetComponent<Text>();
            MakeBtn(_settingsPanel.transform, "KeyboardBtn",
                new Vector2(0.65f, y + 0.02f), new Vector2(0.92f, y + 0.08f),
                "Toggle", new Color(0.3f, 0.4f, 0.5f), ToggleKeyboardNav);

            // ショートカット一覧
            MakeText(_settingsPanel.transform, "ShortcutsTitle",
                new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.4f),
                "--- Keyboard Shortcuts ---", 14, FontStyle.Bold, new Color(1f, 0.9f, 0.5f));

            MakeText(_settingsPanel.transform, "Shortcuts",
                new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.32f),
                "Escape  : Pause / Resume\n" +
                "Space   : Toggle Pause\n" +
                "1-4     : Speed (||, x1, x2, x5)\n" +
                "F       : Weather Forecast\n" +
                "E       : Events Panel\n" +
                "C       : Challenges\n" +
                "N       : Social Share\n" +
                "F12     : Screenshot",
                13, FontStyle.Normal, new Color(0.7f, 0.7f, 0.8f));

            _settingsPanel.SetActive(false);
        }

        private void RefreshSettingsUI()
        {
            if (_contrastLabel != null)
                _contrastLabel.text = $"High Contrast: {(_highContrastEnabled ? "ON" : "OFF")}";
            if (_fontLabel != null)
                _fontLabel.text = $"Large Font: {(_largeFontEnabled ? "ON" : "OFF")}";
            if (_keyboardLabel != null)
                _keyboardLabel.text = $"Keyboard Nav: {(_keyboardNavEnabled ? "ON" : "OFF")}";
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
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 14; txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = text;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
