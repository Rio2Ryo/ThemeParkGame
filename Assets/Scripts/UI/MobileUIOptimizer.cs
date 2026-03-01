// ============================================================
// ThemeParkGame - MobileUIOptimizer
// モバイルUI最適化＆レスポンシブレイアウト (L7)
// 画面サイズ検出・CanvasScaler動的調整・タッチ最適化・
// パネル折り畳み・スワイプジェスチャー・フォントスケール・
// セーフエリア対応を一元管理するシングルトンUI
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;

namespace ThemeParkGame.UI
{
    /// <summary>スワイプ方向</summary>
    public enum SwipeDirection { Left, Right, Up, Down }

    /// <summary>
    /// モバイルUI最適化システム。画面サイズに応じたレスポンシブレイアウトを提供する。
    ///
    /// 【機能一覧】
    /// 1. 画面サイズ検出: Desktop / Tablet / Mobile を自動判定（2秒ポーリング）
    /// 2. CanvasScaler動的調整: レイアウトモードに応じた matchWidthOrHeight 設定
    /// 3. ボタンサイズスケーリング: タッチターゲット44x44dp最小保証
    /// 4. パネル折り畳み: モバイル時のサイドパネル自動折り畳み＋ハンバーガーメニュー
    /// 5. スワイプジェスチャー: 左右上下のスワイプ検出（50px閾値・0.3秒最大）
    /// 6. フォントサイズスケーリング: 小文字の自動拡大（Mobile 1.3x / Tablet 1.15x）
    /// 7. セーフエリア対応: ノッチ・パンチホール回避
    /// </summary>
    public class MobileUIOptimizer : MonoBehaviour
    {
        // ---- シングルトン ----
        public static MobileUIOptimizer Instance { get; private set; }

        // ---- 定数 ----
        private const int DesktopWidthThreshold = 1024;
        private const int TabletWidthThreshold  = 768;
        private const float ScreenPollInterval  = 2.0f;
        private const float SwipeMinDistance     = 50f;
        private const float SwipeMaxDuration     = 0.3f;
        private const float MinTouchTargetSize   = 44f;
        private const float ScaleDesktop = 1.0f;
        private const float ScaleTablet  = 1.2f;
        private const float ScaleMobile  = 1.5f;
        private const float FontScaleMobile  = 1.3f;
        private const float FontScaleTablet  = 1.15f;
        private const int   FontScaleTargetMaxSize = 14;
        private static readonly Vector2 MobileReferenceResolution  = new Vector2(720f, 1280f);
        private static readonly Vector2 DefaultReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Color BgDark = new Color(0.06f, 0.09f, 0.16f, 0.92f);

        // ---- プロパティ・イベント ----

        /// <summary>現在のレイアウトモード</summary>
        public UILayoutMode CurrentLayout { get; private set; }

        /// <summary>レイアウトモード変更イベント</summary>
        public event Action<UILayoutMode> OnLayoutChanged;

        /// <summary>スワイプ検出イベント</summary>
        public event Action<SwipeDirection> OnSwipeDetected;

        // ---- 内部状態 ----
        private float _screenPollTimer;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private UILayoutMode? _layoutOverride;

        // スワイプ
        private Vector2 _swipeStartPos;
        private float _swipeStartTime;
        private bool _swipeTracking;

        // ボタン復元
        private readonly Dictionary<int, Vector2> _originalButtonSizes = new Dictionary<int, Vector2>();

        // パネル折り畳み
        private readonly Dictionary<string, GameObject> _collapsiblePanels = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, bool> _panelCollapsedState = new Dictionary<string, bool>();
        private readonly List<string> _expandedPanelOrder = new List<string>();
        private GameObject _hamburgerButton;
        private GameObject _hamburgerMenuPanel;
        private bool _hamburgerMenuVisible;

        // フォント復元
        private readonly Dictionary<int, int> _originalFontSizes = new Dictionary<int, int>();

        // 設定UI
        private GameObject _settingsPanel;
        private GameObject _settingsButton;
        private Text _layoutLabel;
        private bool _settingsVisible;

        // ============================================================
        // ライフサイクル
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Start()
        {
            _lastScreenWidth  = Screen.width;
            _lastScreenHeight = Screen.height;
            _screenPollTimer  = 0f;

            CurrentLayout = DetectLayoutMode();
            ApplyLayoutOptimizations(CurrentLayout);
            BuildHamburgerMenu();
            BuildSettingsButton();

            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] 初期レイアウト: {CurrentLayout} ({Screen.width}x{Screen.height})");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _screenPollTimer -= Time.unscaledDeltaTime;
            if (_screenPollTimer <= 0f)
            {
                _screenPollTimer = ScreenPollInterval;
                CheckScreenSizeChange();
            }
            DetectSwipeGesture();
        }

        // ============================================================
        // 1. 画面サイズ検出＆レイアウトモード
        // ============================================================

        /// <summary>画面幅からレイアウトモードを判定する。オーバーライド優先。</summary>
        private UILayoutMode DetectLayoutMode()
        {
            if (_layoutOverride.HasValue) return _layoutOverride.Value;
            int w = Screen.width;
            if (w >= DesktopWidthThreshold) return UILayoutMode.Desktop;
            if (w >= TabletWidthThreshold)  return UILayoutMode.Tablet;
            return UILayoutMode.Mobile;
        }

        /// <summary>画面サイズ変更を検出し、必要に応じてレイアウトを再適用する。</summary>
        private void CheckScreenSizeChange()
        {
            int w = Screen.width, h = Screen.height;
            if (w == _lastScreenWidth && h == _lastScreenHeight) return;
            _lastScreenWidth = w; _lastScreenHeight = h;

            var newLayout = DetectLayoutMode();
            if (newLayout == CurrentLayout) return;

            var old = CurrentLayout;
            CurrentLayout = newLayout;
            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] レイアウト変更: {old} -> {newLayout} ({w}x{h})");
            ApplyLayoutOptimizations(newLayout);
            OnLayoutChanged?.Invoke(newLayout);
        }

        /// <summary>全最適化を現在のレイアウトモードで再適用する。</summary>
        private void ApplyLayoutOptimizations(UILayoutMode mode)
        {
            AdjustCanvasScalers(mode);
            ScaleButtonsForTouch();
            ApplyFontScaling();
            ApplySafeAreaToAllCanvases();

            if (mode == UILayoutMode.Mobile) CollapseAllPanels();
            if (_hamburgerButton != null) _hamburgerButton.SetActive(mode == UILayoutMode.Mobile);

            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] レイアウト最適化適用完了: {mode}");
        }

        /// <summary>レイアウトモードを強制設定する。nullで自動判定に戻す。</summary>
        public void ForceLayoutMode(UILayoutMode? mode)
        {
            _layoutOverride = mode;
            var newLayout = DetectLayoutMode();
            if (newLayout != CurrentLayout)
            {
                CurrentLayout = newLayout;
                ApplyLayoutOptimizations(newLayout);
                OnLayoutChanged?.Invoke(newLayout);
            }
            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] レイアウト強制: {(mode.HasValue ? mode.Value.ToString() : "自動")}");
        }

        // ============================================================
        // 2. CanvasScaler 動的調整
        // ============================================================

        /// <summary>
        /// シーン内の全CanvasScalerをレイアウトモードに合わせて調整する。
        /// Desktop=0.5, Tablet=0.65, Mobile=1.0（高さ優先・ポートレート）
        /// </summary>
        private void AdjustCanvasScalers(UILayoutMode mode)
        {
            var scalers = FindObjectsOfType<CanvasScaler>();
            if (scalers == null || scalers.Length == 0) return;

            float match;
            Vector2 refRes;
            switch (mode)
            {
                case UILayoutMode.Tablet:  match = 0.65f; refRes = DefaultReferenceResolution;  break;
                case UILayoutMode.Mobile:  match = 1.0f;  refRes = MobileReferenceResolution;   break;
                default:                   match = 0.5f;  refRes = DefaultReferenceResolution;  break;
            }

            foreach (var s in scalers)
            {
                if (s == null) continue;
                s.matchWidthOrHeight = match;
                s.referenceResolution = refRes;
            }
            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] CanvasScaler: match={match:F2}, ref={refRes} ({scalers.Length}個)");
        }

        // ============================================================
        // 3. ボタンサイズスケーリング
        // ============================================================

        /// <summary>
        /// 全ボタンをタッチ操作に適したサイズにスケーリングする。
        /// Mobile: 最小44x44dp保証＋1.5倍、Tablet: 1.2倍、Desktop: 元サイズ復元。
        /// </summary>
        public void ScaleButtonsForTouch()
        {
            var buttons = FindObjectsOfType<Button>();
            if (buttons == null || buttons.Length == 0) return;

            float mul = CurrentLayout == UILayoutMode.Mobile  ? ScaleMobile
                      : CurrentLayout == UILayoutMode.Tablet  ? ScaleTablet
                      : ScaleDesktop;

            foreach (var b in buttons)
            {
                if (b == null) continue;
                var rt = b.GetComponent<RectTransform>();
                if (rt == null) continue;

                int id = rt.GetInstanceID();
                if (!_originalButtonSizes.ContainsKey(id))
                    _originalButtonSizes[id] = rt.sizeDelta;

                Vector2 scaled = _originalButtonSizes[id] * mul;
                if (CurrentLayout == UILayoutMode.Mobile)
                {
                    scaled.x = Mathf.Max(scaled.x, MinTouchTargetSize);
                    scaled.y = Mathf.Max(scaled.y, MinTouchTargetSize);
                }
                rt.sizeDelta = scaled;
            }
            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] ボタンスケーリング: x{mul:F1} ({buttons.Length}個)");
        }

        // ============================================================
        // 4. パネル折り畳みシステム
        // ============================================================

        /// <summary>折り畳み可能パネルを登録する。モバイル時は即座に折り畳む。</summary>
        public void RegisterCollapsiblePanel(GameObject panel, string panelId)
        {
            if (panel == null || string.IsNullOrEmpty(panelId)) return;
            _collapsiblePanels[panelId] = panel;
            _panelCollapsedState[panelId] = false;
            if (CurrentLayout == UILayoutMode.Mobile)
            {
                panel.SetActive(false);
                _panelCollapsedState[panelId] = true;
            }
            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] パネル登録: {panelId}");
        }

        /// <summary>全折り畳みパネルを折り畳む。</summary>
        public void CollapseAllPanels()
        {
            foreach (var kvp in _collapsiblePanels)
            {
                if (kvp.Value != null) { kvp.Value.SetActive(false); _panelCollapsedState[kvp.Key] = true; }
            }
            _expandedPanelOrder.Clear();
            WebGLOptimizer.LogVerbose("[MobileUIOptimizer] 全パネル折り畳み");
        }

        /// <summary>指定IDのパネルを展開する。</summary>
        public void ExpandPanel(string panelId)
        {
            if (!_collapsiblePanels.TryGetValue(panelId, out var panel) || panel == null) return;
            panel.SetActive(true);
            _panelCollapsedState[panelId] = false;
            _expandedPanelOrder.Remove(panelId);
            _expandedPanelOrder.Add(panelId);
            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] パネル展開: {panelId}");
        }

        /// <summary>指定IDのパネルを折り畳む。</summary>
        public void CollapsePanel(string panelId)
        {
            if (!_collapsiblePanels.TryGetValue(panelId, out var panel) || panel == null) return;
            panel.SetActive(false);
            _panelCollapsedState[panelId] = true;
            _expandedPanelOrder.Remove(panelId);
        }

        /// <summary>ハンバーガーメニュー（モバイル用左上ボタン＋パネルリスト）を構築する。</summary>
        private void BuildHamburgerMenu()
        {
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            // ---- ハンバーガーボタン ----
            _hamburgerButton = new GameObject("HamburgerButton");
            _hamburgerButton.transform.SetParent(canvas.transform, false);
            var btnRt = _hamburgerButton.AddComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0f, 1f);
            btnRt.pivot = new Vector2(0f, 1f);
            btnRt.anchoredPosition = new Vector2(8f, -8f);
            btnRt.sizeDelta = new Vector2(48f, 48f);

            var btnImg = _hamburgerButton.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.18f, 0.25f, 0.9f);
            var btn = _hamburgerButton.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(ToggleHamburgerMenu);

            // 三本線アイコン（テキスト代用）
            var iconTxt = MakeChildText(_hamburgerButton.transform, "Icon", "\u2261", 28, Color.white);
            StretchFill(iconTxt.rectTransform);

            // ---- ハンバーガーメニューパネル ----
            _hamburgerMenuPanel = new GameObject("HamburgerMenuPanel");
            _hamburgerMenuPanel.transform.SetParent(canvas.transform, false);
            var menuRt = _hamburgerMenuPanel.AddComponent<RectTransform>();
            menuRt.anchorMin = new Vector2(0f, 0.3f);
            menuRt.anchorMax = new Vector2(0.7f, 1f);
            menuRt.offsetMin = menuRt.offsetMax = Vector2.zero;

            var menuBg = _hamburgerMenuPanel.AddComponent<Image>();
            menuBg.color = new Color(0.05f, 0.07f, 0.14f, 0.95f);

            MakeAnchoredText(_hamburgerMenuPanel.transform, "MenuTitle",
                new Vector2(0.05f, 0.92f), new Vector2(0.7f, 0.99f),
                "パネル一覧", 18, FontStyle.Bold, Color.white);

            MakeAnchoredBtn(_hamburgerMenuPanel.transform, "CloseBtn",
                new Vector2(0.85f, 0.92f), new Vector2(0.97f, 0.99f),
                "X", new Color(0.6f, 0.15f, 0.15f), () => ToggleHamburgerMenu());

            _hamburgerMenuPanel.SetActive(false);
            _hamburgerButton.SetActive(CurrentLayout == UILayoutMode.Mobile);
        }

        /// <summary>ハンバーガーメニュー表示切り替え。</summary>
        private void ToggleHamburgerMenu()
        {
            _hamburgerMenuVisible = !_hamburgerMenuVisible;
            if (_hamburgerMenuPanel == null) return;
            if (_hamburgerMenuVisible) RefreshHamburgerMenuContent();
            _hamburgerMenuPanel.SetActive(_hamburgerMenuVisible);
        }

        /// <summary>ハンバーガーメニュー内のパネルリストを動的生成する。</summary>
        private void RefreshHamburgerMenuContent()
        {
            if (_hamburgerMenuPanel == null) return;

            // 既存パネルボタン削除
            for (int i = _hamburgerMenuPanel.transform.childCount - 1; i >= 0; i--)
            {
                var child = _hamburgerMenuPanel.transform.GetChild(i);
                if (child.name.StartsWith("PanelItem_")) Destroy(child.gameObject);
            }

            float y = 0.85f;
            const float rowH = 0.08f, gap = 0.01f;
            int idx = 0;

            foreach (var kvp in _collapsiblePanels)
            {
                string pid = kvp.Key;
                bool collapsed = _panelCollapsedState.ContainsKey(pid) && _panelCollapsedState[pid];
                float yPos = y - (rowH + gap) * idx;
                if (yPos < 0.05f) break;

                string capturedId = pid;
                Color bg = collapsed ? new Color(0.2f, 0.22f, 0.3f, 0.8f) : new Color(0.2f, 0.55f, 0.35f, 0.8f);
                string label = collapsed ? $"{pid} [折り畳み]" : $"{pid} [展開中]";

                MakeAnchoredBtn(_hamburgerMenuPanel.transform, $"PanelItem_{pid}",
                    new Vector2(0.05f, yPos), new Vector2(0.95f, yPos + rowH),
                    label, bg, () => { TogglePanelFromMenu(capturedId); });

                idx++;
            }
        }

        /// <summary>メニューからパネルの展開/折り畳みを切り替える。</summary>
        private void TogglePanelFromMenu(string panelId)
        {
            if (!_panelCollapsedState.ContainsKey(panelId)) return;
            if (_panelCollapsedState[panelId]) ExpandPanel(panelId);
            else CollapsePanel(panelId);
            RefreshHamburgerMenuContent();
        }

        // ============================================================
        // 5. スワイプジェスチャーサポート
        // ============================================================

        /// <summary>タッチ/マウスのスワイプを検出する。閾値50px・0.3秒以内。</summary>
        private void DetectSwipeGesture()
        {
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    _swipeStartPos = t.position; _swipeStartTime = Time.unscaledTime; _swipeTracking = true;
                }
                else if (t.phase == TouchPhase.Ended && _swipeTracking)
                {
                    ProcessSwipeEnd(t.position); _swipeTracking = false;
                }
                else if (t.phase == TouchPhase.Canceled) _swipeTracking = false;
            }
            else
            {
                if (Input.GetMouseButtonDown(0))
                {
                    _swipeStartPos = Input.mousePosition; _swipeStartTime = Time.unscaledTime; _swipeTracking = true;
                }
                else if (Input.GetMouseButtonUp(0) && _swipeTracking)
                {
                    ProcessSwipeEnd(Input.mousePosition); _swipeTracking = false;
                }
            }
        }

        /// <summary>スワイプ終了判定。距離・時間を検証しイベント発火＋デフォルトアクション実行。</summary>
        private void ProcessSwipeEnd(Vector2 endPos)
        {
            float dur = Time.unscaledTime - _swipeStartTime;
            if (dur > SwipeMaxDuration) return;

            Vector2 delta = endPos - _swipeStartPos;
            float dist = delta.magnitude;
            if (dist < SwipeMinDistance) return;

            SwipeDirection dir = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x > 0f ? SwipeDirection.Right : SwipeDirection.Left)
                : (delta.y > 0f ? SwipeDirection.Up    : SwipeDirection.Down);

            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] スワイプ: {dir} ({dist:F0}px, {dur:F2}s)");

            // デフォルトアクション
            switch (dir)
            {
                case SwipeDirection.Left:  OpenNextCollapsedPanel();  break;  // 次パネル
                case SwipeDirection.Right: CloseLastExpandedPanel();  break;  // 閉じる
                case SwipeDirection.Up:    ExpandFirstCollapsedPanel(); break; // 展開
            }
            OnSwipeDetected?.Invoke(dir);
        }

        private void OpenNextCollapsedPanel()
        {
            foreach (var kvp in _panelCollapsedState)
                if (kvp.Value) { ExpandPanel(kvp.Key); return; }
        }

        private void CloseLastExpandedPanel()
        {
            if (_expandedPanelOrder.Count == 0) return;
            CollapsePanel(_expandedPanelOrder[_expandedPanelOrder.Count - 1]);
        }

        private void ExpandFirstCollapsedPanel()
        {
            foreach (var kvp in _panelCollapsedState)
                if (kvp.Value) { ExpandPanel(kvp.Key); return; }
        }

        // ============================================================
        // 6. フォントサイズスケーリング
        // ============================================================

        /// <summary>
        /// fontSize <= 14 の全Textを拡大する。Mobile: 1.3倍、Tablet: 1.15倍。
        /// </summary>
        public void ApplyFontScaling()
        {
            var texts = FindObjectsOfType<Text>();
            if (texts == null || texts.Length == 0) return;

            float scale = CurrentLayout == UILayoutMode.Mobile ? FontScaleMobile
                        : CurrentLayout == UILayoutMode.Tablet ? FontScaleTablet
                        : 1.0f;
            int cnt = 0;
            foreach (var t in texts)
            {
                if (t == null) continue;
                int id = t.GetInstanceID();
                if (!_originalFontSizes.ContainsKey(id)) _originalFontSizes[id] = t.fontSize;
                int orig = _originalFontSizes[id];
                if (orig <= FontScaleTargetMaxSize)
                {
                    t.fontSize = Mathf.RoundToInt(orig * scale);
                    cnt++;
                }
            }
            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] フォントスケール: x{scale:F2} ({cnt}/{texts.Length}個)");
        }

        // ============================================================
        // 7. セーフエリア対応
        // ============================================================

        /// <summary>Screen.safeAreaをRectTransformに適用しノッチ/パンチホールを回避する。</summary>
        public void ApplySafeArea(RectTransform root)
        {
            if (root == null) return;
            Rect sa = Screen.safeArea;
            Vector2 ss = new Vector2(Screen.width, Screen.height);
            if (sa.width >= ss.x && sa.height >= ss.y) return;

            root.anchorMin = new Vector2(sa.xMin / ss.x, sa.yMin / ss.y);
            root.anchorMax = new Vector2(sa.xMax / ss.x, sa.yMax / ss.y);
            root.offsetMin = root.offsetMax = Vector2.zero;
            WebGLOptimizer.LogVerbose($"[MobileUIOptimizer] セーフエリア適用: {root.anchorMin}-{root.anchorMax}");
        }

        /// <summary>モバイル時に全Canvasへセーフエリアを適用する。</summary>
        private void ApplySafeAreaToAllCanvases()
        {
            if (CurrentLayout != UILayoutMode.Mobile) return;
            foreach (var c in FindObjectsOfType<Canvas>())
            {
                if (c == null) continue;
                var rt = c.GetComponent<RectTransform>();
                if (rt != null) ApplySafeArea(rt);
            }
        }

        // ============================================================
        // 設定UI（レイアウトオーバーライド）
        // ============================================================

        /// <summary>歯車アイコンの設定ボタンを画面右上に配置する。</summary>
        private void BuildSettingsButton()
        {
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            _settingsButton = new GameObject("MobileUI_SettingsBtn");
            _settingsButton.transform.SetParent(canvas.transform, false);
            var rt = _settingsButton.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-60f, -8f);
            rt.sizeDelta = new Vector2(36f, 36f);

            var img = _settingsButton.AddComponent<Image>();
            img.color = new Color(0.2f, 0.23f, 0.3f, 0.85f);
            var btn = _settingsButton.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(ToggleSettingsPanel);

            var icon = MakeChildText(_settingsButton.transform, "GearIcon", "\u2699", 22,
                new Color(0.8f, 0.82f, 0.88f));
            StretchFill(icon.rectTransform);
        }

        /// <summary>設定パネル表示切り替え。</summary>
        private void ToggleSettingsPanel()
        {
            _settingsVisible = !_settingsVisible;
            if (_settingsPanel == null) BuildSettingsPanel();
            RefreshSettingsLabel();
            _settingsPanel.SetActive(_settingsVisible);
        }

        /// <summary>レイアウト強制オーバーライド設定パネルを構築する。</summary>
        private void BuildSettingsPanel()
        {
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            _settingsPanel = new GameObject("MobileUI_SettingsPanel");
            _settingsPanel.transform.SetParent(canvas.transform, false);
            var rt = _settingsPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.6f, 0.55f);
            rt.anchorMax = new Vector2(0.95f, 0.95f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var bg = _settingsPanel.AddComponent<Image>();
            bg.color = BgDark;

            // タイトル＋閉じる
            MakeAnchoredText(_settingsPanel.transform, "Title",
                new Vector2(0.04f, 0.88f), new Vector2(0.75f, 0.98f),
                "レイアウト設定", 17, FontStyle.Bold, Color.white);
            MakeAnchoredBtn(_settingsPanel.transform, "CloseBtn",
                new Vector2(0.85f, 0.88f), new Vector2(0.97f, 0.98f),
                "X", new Color(0.6f, 0.15f, 0.15f),
                () => { _settingsVisible = false; _settingsPanel.SetActive(false); });

            // 現在レイアウト表示
            var lbl = MakeAnchoredText(_settingsPanel.transform, "CurrentLayout",
                new Vector2(0.04f, 0.74f), new Vector2(0.96f, 0.85f),
                "", 14, FontStyle.Normal, new Color(0.7f, 0.85f, 0.95f));
            _layoutLabel = lbl.GetComponent<Text>();

            // モード選択ボタン
            float y = 0.58f; const float h = 0.12f, g = 0.02f;

            MakeAnchoredBtn(_settingsPanel.transform, "AutoBtn",
                new Vector2(0.06f, y), new Vector2(0.94f, y + h),
                "自動検出", new Color(0.25f, 0.45f, 0.6f), () => ForceLayoutMode(null));
            y -= h + g;
            MakeAnchoredBtn(_settingsPanel.transform, "DesktopBtn",
                new Vector2(0.06f, y), new Vector2(0.94f, y + h),
                "Desktop (1024px+)", new Color(0.3f, 0.35f, 0.45f), () => ForceLayoutMode(UILayoutMode.Desktop));
            y -= h + g;
            MakeAnchoredBtn(_settingsPanel.transform, "TabletBtn",
                new Vector2(0.06f, y), new Vector2(0.94f, y + h),
                "Tablet (768-1024px)", new Color(0.3f, 0.35f, 0.45f), () => ForceLayoutMode(UILayoutMode.Tablet));
            y -= h + g;
            MakeAnchoredBtn(_settingsPanel.transform, "MobileBtn",
                new Vector2(0.06f, y), new Vector2(0.94f, y + h),
                "Mobile (768px\u672a\u6e80)", new Color(0.3f, 0.35f, 0.45f), () => ForceLayoutMode(UILayoutMode.Mobile));

            _settingsPanel.SetActive(false);
        }

        /// <summary>設定パネルのレイアウト表示ラベルを更新する。</summary>
        private void RefreshSettingsLabel()
        {
            if (_layoutLabel == null) return;
            string mode = CurrentLayout == UILayoutMode.Desktop ? "Desktop"
                        : CurrentLayout == UILayoutMode.Tablet  ? "Tablet"
                        : "Mobile";
            string ov = _layoutOverride.HasValue ? " [手動]" : " [自動]";
            _layoutLabel.text = $"現在: {mode}{ov}  ({Screen.width}x{Screen.height})";
        }

        // ============================================================
        // UIヘルパー
        // ============================================================

        /// <summary>アンカー指定テキストを生成する。</summary>
        private GameObject MakeAnchoredText(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string content, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = new Vector2(4f, 0f); r.offsetMax = new Vector2(-4f, 0f);
            var t = go.AddComponent<Text>();
            t.text = content; t.font = FontManager.Regular;
            t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
            return go;
        }

        /// <summary>アンカー指定ボタンを生成する。</summary>
        private void MakeAnchoredBtn(Transform parent, string name,
            Vector2 aMin, Vector2 aMax, string label, Color bgColor,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = r.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>(); img.color = bgColor;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var txt = MakeChildText(go.transform, "Label", label, 14, Color.white);
            StretchFill(txt.rectTransform);
        }

        /// <summary>子テキストオブジェクトを生成する。</summary>
        private Text MakeChildText(Transform parent, string name, string content, int size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var t = go.AddComponent<Text>();
            t.text = content; t.font = FontManager.Regular;
            t.fontSize = size; t.color = color;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            return t;
        }

        /// <summary>RectTransformを親いっぱいにストレッチする。</summary>
        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ============================================================
        // 公開ユーティリティ
        // ============================================================

        /// <summary>現在のレイアウトモード名（日本語）を返す。</summary>
        public string GetLayoutLabel()
        {
            switch (CurrentLayout)
            {
                case UILayoutMode.Desktop: return "デスクトップ";
                case UILayoutMode.Tablet:  return "タブレット";
                case UILayoutMode.Mobile:  return "モバイル";
                default: return "不明";
            }
        }

        /// <summary>モバイルレイアウトかどうか。</summary>
        public bool IsMobileLayout => CurrentLayout == UILayoutMode.Mobile;

        /// <summary>タブレット以下（モバイル含む）のコンパクトレイアウトかどうか。</summary>
        public bool IsCompactLayout => CurrentLayout == UILayoutMode.Mobile || CurrentLayout == UILayoutMode.Tablet;

        /// <summary>登録済みパネル数。</summary>
        public int RegisteredPanelCount => _collapsiblePanels.Count;

        /// <summary>指定パネルが折り畳み中かどうかを返す。</summary>
        public bool IsPanelCollapsed(string panelId)
        {
            return _panelCollapsedState.TryGetValue(panelId, out bool v) && v;
        }
    }
}
