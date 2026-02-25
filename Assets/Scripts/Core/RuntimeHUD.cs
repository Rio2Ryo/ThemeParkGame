// ============================================================
// ThemeParkGame - RuntimeHUD
// uGUI (Canvas + Text) ベースのランタイムHUDオーバーレイ
// プレハブ/シーン配置なしでコードからCanvasを構築し
// ①スコアボード: 入場者数・総収益・満足度をリアルタイム表示
// ②来場者クリック個別情報パネル
// ③アトラクション収益パネル
// ④メニューボタン・ポーズ画面
// ⑤ゲームオーバー画面（最終スコア・リスタート）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ThemeParkGame.Attraction;
using ThemeParkGame.Park;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Core
{
    public class RuntimeHUD : MonoBehaviour
    {
        // ---- Canvas ----
        private Canvas _canvas;
        private RectTransform _canvasRoot;

        // ---- スコアボード（画面上部中央） ----
        private Text _sbVisitorValue;
        private Text _sbRevenueValue;
        private Text _sbSatisfactionValue;
        private GameObject _sbSatisfactionBar;
        private Image _sbSatisfactionFill;

        // ---- トップバー（スコアボード下） ----
        private Text _moneyText;
        private Text _timeWeatherText;
        private Text _staffText;

        // ---- 速度ボタン ----
        private Image[] _speedBtnBgs;
        private readonly int[] _speedLevels = { 0, 1, 2, 5 };
        private readonly string[] _speedLabels = { "||", "x1", "x2", "x5" };

        // ---- 来場者状態パネル ----
        private Text[] _visitorStatTexts;

        // ---- アトラクションパネル ----
        private RectTransform _attrPanelRt;
        private readonly List<Text> _attrLines = new List<Text>();

        // ---- 来場者情報パネル ----
        private GameObject _visitorInfoPanel;
        private RectTransform _visitorInfoRt;
        private Text _viName;
        private Text _viType;
        private Text _viState;
        private Text _viHappiness;
        private Text _viCash;
        private Text _viHunger;
        private Text _viThirst;
        private Text _viNausea;
        private Text _viToilet;
        private Text _viExcitement;
        private Text _viRides;
        private Text _viSatisfaction;
        private Image _viSatBarFill;
        private GameObject _viSatBar;
        private Text _viLifecycle;
        private Text _viLifecycleStats;
        private VisitorAI _selectedVisitor;

        // ---- 満足度フローティングポップアップ ----
        private readonly List<FloatingScore> _floatingScores = new List<FloatingScore>();
        private struct FloatingScore
        {
            public GameObject Go;
            public Text Label;
            public int VisitorId;
            public float Timer;
            public Vector3 WorldPos;
        }
        private const float FloatingScoreDuration = 1.5f;

        // ---- メニュー/ポーズ/結果画面 ----
        private GameObject _menuBtn;
        private GameObject _pauseOverlay;
        private GameObject _resultsOverlay;
        private Text _resultsFinalScore;
        private Text _resultsBody;

        // ---- シナリオ目標パネル ----
        private GameObject _scenarioPanel;
        private Text _scenarioTitle;
        private Text _scenarioObjectives;

        // ---- セーブ/ロードUI ----
        private GameObject _saveLoadPanel;
        private Text[] _slotTexts;
        private Text _saveLoadMessage;

        // ---- 通路混雑度表示 ----
        private Text _congestionText;
        private Image _congestionBarFill;

        // ---- イベント表示 ----
        private Text _eventText;
        private GameObject _eventPanel;
        private float _eventBlinkTimer;

        // ---- ライフサイクルフェーズ表示 ----
        private Text _lcWaitingText;
        private Text _lcEnjoyingText;
        private Text _lcLeavingText;
        private Image _lcWaitingBar;
        private Image _lcEnjoyingBar;
        private Image _lcLeavingBar;

        // ---- パーク評価表示 ----
        private Text _ratingStarsText;
        private Text _ratingScoreText;
        private Text _ratingLabelText;
        private Text _ratingTrendText;
        private Text[] _ratingCatTexts;
        private Image[] _ratingCatBars;

        // ---- ファーストパーソンビュー ----
        private GameObject _fpvOverlay;
        private Text _fpvStatusText;
        private Text _fpvHappinessText;
        private Text _fpvPhaseText;
        private Text _fpvHintText;
        private Button _fpvExitButton;
        private Button _viFirstPersonButton;

        // ---- 通知バッジ ----
        private GameObject _notifBadge;
        private Text _notifBadgeText;

        // ---- データキャッシュ ----
        private float _updateTimer;
        private const float UpdateInterval = 0.3f;
        private Attraction.Attraction[] _attractions;
        private float _attrCacheTimer;
        private int _currentSpeed = 1;

        // 来場者状態カウント
        private int _walkingCount, _waitingCount, _ridingCount;
        private int _shoppingCount, _idleCount, _leavingCount;

        // ---- カラー定数 ----
        private static readonly Color BgDark     = new Color(0.06f, 0.09f, 0.16f, 0.92f);
        private static readonly Color Gold       = new Color(0.95f, 0.88f, 0.45f);
        private static readonly Color Green      = new Color(0.4f, 0.95f, 0.45f);
        private static readonly Color Yellow     = new Color(0.95f, 0.95f, 0.35f);
        private static readonly Color Red        = new Color(0.95f, 0.35f, 0.35f);
        private static readonly Color Muted      = new Color(0.7f, 0.72f, 0.8f);
        private static readonly Color BtnActive  = new Color(0.2f, 0.75f, 0.4f);
        private static readonly Color BtnNormal  = new Color(0.25f, 0.28f, 0.35f);
        private static readonly Color Cyan       = new Color(0.4f, 0.85f, 0.95f);

        private static readonly string[] StatLabels =
            { "移動中", "待ち行列", "搭乗中", "買い物/食事", "散策/休憩", "退園中" };
        private static readonly Color[] StatDotColors =
        {
            new Color(0.3f, 0.8f, 0.5f),   // 移動
            new Color(1.0f, 0.85f, 0.2f),  // 待ち
            new Color(1.0f, 0.45f, 0.1f),  // 搭乗
            new Color(0.9f, 0.6f, 0.8f),   // 買い物
            new Color(0.3f, 0.6f, 1.0f),   // 散策
            new Color(0.5f, 0.5f, 0.5f),   // 退園
        };

        // ================================================================
        // 初期化
        // ================================================================

        private void Awake()
        {
            BuildCanvas();
            GameEvents.OnVisitorSatisfactionChanged += OnVisitorSatisfactionChanged;
        }

        private void OnDestroy()
        {
            GameEvents.OnVisitorSatisfactionChanged -= OnVisitorSatisfactionChanged;
        }

        private void BuildCanvas()
        {
            var cGo = new GameObject("HUD_Canvas");
            cGo.transform.SetParent(transform);
            _canvas = cGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            var scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            cGo.AddComponent<GraphicRaycaster>();

            _canvasRoot = cGo.GetComponent<RectTransform>();

            BuildScoreboard(_canvasRoot);
            BuildInfoBar(_canvasRoot);
            BuildSpeedPanel(_canvasRoot);
            BuildVisitorPanel(_canvasRoot);
            BuildAttractionPanel(_canvasRoot);
            BuildVisitorInfoPanel(_canvasRoot);
            BuildMenuButton(_canvasRoot);
            BuildPauseOverlay(_canvasRoot);
            BuildResultsOverlay(_canvasRoot);
            BuildScenarioPanel(_canvasRoot);
            BuildSaveLoadPanel(_canvasRoot);
            BuildEventPanel(_canvasRoot);
            BuildLifecyclePanel(_canvasRoot);
            BuildRatingPanel(_canvasRoot);
            BuildFirstPersonOverlay(_canvasRoot);
        }

        // ================================================================
        // スコアボード（画面上部中央 740x80）
        // 入場者数・総収益・平均満足度を大きく目立つ表示
        // ================================================================

        private void BuildScoreboard(RectTransform root)
        {
            float boardW = 740f;
            float boardH = 80f;

            var bg = MakePanel(root, "Scoreboard", boardW, boardH, BgDark);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -4f);

            float colW = boardW / 3f;

            // ---- 入場者数 ----
            var visLabel = MakeLabel(rt, "VisLabel", "VISITORS", 13, new Color(0.5f, 0.6f, 0.7f),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(visLabel.rectTransform, 0f, boardH - 4f, colW, 20f, new Vector2(0f, 1f));

            _sbVisitorValue = MakeLabel(rt, "VisValue", "0", 34, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_sbVisitorValue.rectTransform, 0f, boardH - 24f, colW, 42f, new Vector2(0f, 1f));

            var visSub = MakeLabel(rt, "VisSub", "人", 12, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceInParent(visSub.rectTransform, 0f, boardH - 66f, colW, 16f, new Vector2(0f, 1f));

            // ---- 区切り線 1 ----
            var sep1 = MakePanel(rt, "Sep1", 2f, boardH - 16f, new Color(0.3f, 0.35f, 0.45f, 0.5f));
            var sep1Rt = sep1.GetComponent<RectTransform>();
            sep1Rt.anchorMin = sep1Rt.anchorMax = new Vector2(0f, 0.5f);
            sep1Rt.pivot = new Vector2(0.5f, 0.5f);
            sep1Rt.anchoredPosition = new Vector2(colW, 0f);

            // ---- 総収益 ----
            var revLabel = MakeLabel(rt, "RevLabel", "TOTAL REVENUE", 13, new Color(0.5f, 0.6f, 0.7f),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(revLabel.rectTransform, colW, boardH - 4f, colW, 20f, new Vector2(0f, 1f));

            _sbRevenueValue = MakeLabel(rt, "RevValue", "$0", 34, Green,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_sbRevenueValue.rectTransform, colW, boardH - 24f, colW, 42f, new Vector2(0f, 1f));

            var revSub = MakeLabel(rt, "RevSub", "", 12, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceInParent(revSub.rectTransform, colW, boardH - 66f, colW, 16f, new Vector2(0f, 1f));

            // ---- 区切り線 2 ----
            var sep2 = MakePanel(rt, "Sep2", 2f, boardH - 16f, new Color(0.3f, 0.35f, 0.45f, 0.5f));
            var sep2Rt = sep2.GetComponent<RectTransform>();
            sep2Rt.anchorMin = sep2Rt.anchorMax = new Vector2(0f, 0.5f);
            sep2Rt.pivot = new Vector2(0.5f, 0.5f);
            sep2Rt.anchoredPosition = new Vector2(colW * 2f, 0f);

            // ---- 平均満足度 ----
            var satLabel = MakeLabel(rt, "SatLabel", "SATISFACTION", 13, new Color(0.5f, 0.6f, 0.7f),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(satLabel.rectTransform, colW * 2f, boardH - 4f, colW, 20f, new Vector2(0f, 1f));

            _sbSatisfactionValue = MakeLabel(rt, "SatValue", "0%", 34, Green,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_sbSatisfactionValue.rectTransform, colW * 2f, boardH - 24f, colW, 42f, new Vector2(0f, 1f));

            // 満足度バー
            float barW = colW - 40f;
            float barH = 8f;
            float barX = colW * 2f + 20f;
            float barY = boardH - 70f;
            var barBg = MakePanel(rt, "SatBarBg", barW, barH, new Color(0.15f, 0.18f, 0.25f));
            PlaceInParent(barBg.GetComponent<RectTransform>(), barX, barY, barW, barH, new Vector2(0f, 1f));

            var barFill = MakePanel(rt, "SatBarFill", barW, barH, Green);
            PlaceInParent(barFill.GetComponent<RectTransform>(), barX, barY, barW, barH, new Vector2(0f, 1f));
            _sbSatisfactionBar = barBg;
            _sbSatisfactionFill = barFill.GetComponent<Image>();
        }

        // ================================================================
        // 情報バー（スコアボード下、資金・時間・天候・スタッフ）
        // ================================================================

        private void BuildInfoBar(RectTransform root)
        {
            float barW = 740f;
            float barH = 28f;

            var bg = MakePanel(root, "InfoBar", barW, barH, new Color(0.05f, 0.07f, 0.13f, 0.85f));
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -88f);

            _moneyText = MakeLabel(rt, "Money", "", 14, Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(_moneyText.rectTransform, 12f, barH, barW * 0.22f, barH, new Vector2(0f, 1f));

            _timeWeatherText = MakeLabel(rt, "TimeWeather", "", 14, Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceInParent(_timeWeatherText.rectTransform, barW * 0.22f, barH, barW * 0.3f, barH, new Vector2(0f, 1f));

            // 混雑度インジケーター
            _congestionText = MakeLabel(rt, "Congestion", "通路: 空き", 13, Green, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_congestionText.rectTransform, barW * 0.52f, barH, 85f, barH, new Vector2(0f, 1f));

            // 混雑度バー
            var barBg = MakePanel(rt, "CongBarBg", 60f, 10f, new Color(0.15f, 0.15f, 0.2f));
            var barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.anchorMin = barBgRt.anchorMax = new Vector2(0f, 0.5f);
            barBgRt.pivot = new Vector2(0f, 0.5f);
            barBgRt.anchoredPosition = new Vector2(barW * 0.52f + 86f, 0f);

            var fill = MakePanel(barBgRt, "CongBarFill", 60f, 10f, Green);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, 0f); // 初期は0幅
            _congestionBarFill = fill.GetComponent<Image>();

            _staffText = MakeLabel(rt, "Staff", "", 14, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            PlaceInParent(_staffText.rectTransform, barW * 0.76f, barH, barW * 0.22f, barH, new Vector2(0f, 1f));
        }

        // ================================================================
        // 速度ボタンパネル（右上）
        // ================================================================

        private void BuildSpeedPanel(RectTransform root)
        {
            float panelW = 230f;
            float panelH = 44f;

            var bg = MakePanel(root, "SpeedPanel", panelW, panelH, BgDark);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -122f);

            _speedBtnBgs = new Image[4];
            float btnW = 50f;
            float gap = 4f;
            float startX = 8f;

            for (int i = 0; i < 4; i++)
            {
                float bx = startX + i * (btnW + gap);

                var btnBg = MakePanel(rt, $"SpeedBtn{i}", btnW, 34f, BtnNormal);
                var btnRt = btnBg.GetComponent<RectTransform>();
                btnRt.anchorMin = btnRt.anchorMax = new Vector2(0f, 0.5f);
                btnRt.pivot = new Vector2(0f, 0.5f);
                btnRt.anchoredPosition = new Vector2(bx, 0f);

                var btnImg = btnBg.GetComponent<Image>();
                btnImg.raycastTarget = true;
                var btn = btnBg.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.35f, 0.4f, 0.5f);
                colors.pressedColor = BtnActive;
                btn.colors = colors;
                btn.targetGraphic = btnImg;

                var label = MakeLabel(btnRt, "Label", _speedLabels[i], 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                StretchFill(label.rectTransform);

                int idx = i;
                btn.onClick.AddListener(() => OnSpeedClicked(idx));

                _speedBtnBgs[i] = btnBg.GetComponent<Image>();
            }
        }

        // ================================================================
        // 来場者状態パネル（左下 220x200）
        // ================================================================

        private void BuildVisitorPanel(RectTransform root)
        {
            float panelW = 220f;
            float panelH = 200f;

            var bg = MakePanel(root, "VisitorPanel", panelW, panelH, BgDark);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(10f, 10f);

            var header = MakeLabel(rt, "Header", "来場者状況", 16, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(header.rectTransform, 0f, panelH - 4f, panelW, 24f, new Vector2(0f, 1f));

            _visitorStatTexts = new Text[6];
            float lineH = 24f;
            float startY = panelH - 34f;

            for (int i = 0; i < 6; i++)
            {
                float y = startY - i * lineH;

                var dot = MakePanel(rt, $"Dot{i}", 12f, 12f, StatDotColors[i]);
                var dotRt = dot.GetComponent<RectTransform>();
                dotRt.anchorMin = dotRt.anchorMax = new Vector2(0f, 0f);
                dotRt.pivot = new Vector2(0f, 0.5f);
                dotRt.anchoredPosition = new Vector2(10f, y - lineH * 0.5f);

                _visitorStatTexts[i] = MakeLabel(rt, $"Stat{i}", $"{StatLabels[i]}: 0", 14, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
                PlaceInParent(_visitorStatTexts[i].rectTransform, 28f, y, panelW - 36f, lineH, new Vector2(0f, 1f));
            }
        }

        // ================================================================
        // アトラクションパネル（右下 320xN）
        // ================================================================

        private void BuildAttractionPanel(RectTransform root)
        {
            float panelW = 320f;
            float panelH = 40f;

            var bg = MakePanel(root, "AttractionPanel", panelW, panelH, BgDark);
            _attrPanelRt = bg.GetComponent<RectTransform>();
            _attrPanelRt.anchorMin = _attrPanelRt.anchorMax = new Vector2(1f, 0f);
            _attrPanelRt.pivot = new Vector2(1f, 0f);
            _attrPanelRt.anchoredPosition = new Vector2(-10f, 10f);

            var header = MakeLabel(_attrPanelRt, "Header", "Attraction Status", 16, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(header.rectTransform, 0f, panelH - 4f, panelW, 24f, new Vector2(0f, 1f));
        }

        // ================================================================
        // 来場者個別情報パネル（画面中央左 280x340）
        // ================================================================

        private void BuildVisitorInfoPanel(RectTransform root)
        {
            float panelW = 280f;
            float panelH = 450f;

            var bg = MakePanel(root, "VisitorInfoPanel", panelW, panelH, BgDark);
            _visitorInfoPanel = bg;
            _visitorInfoRt = bg.GetComponent<RectTransform>();
            _visitorInfoRt.anchorMin = _visitorInfoRt.anchorMax = new Vector2(0f, 0.5f);
            _visitorInfoRt.pivot = new Vector2(0f, 0.5f);
            _visitorInfoRt.anchoredPosition = new Vector2(10f, 0f);

            var rt = _visitorInfoRt;

            var header = MakeLabel(rt, "VIHeader", "来場者情報", 16, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(header.rectTransform, 0f, panelH - 4f, panelW, 24f, new Vector2(0f, 1f));

            // 閉じるボタン
            var closeGo = MakePanel(rt, "CloseBtn", 28f, 28f, new Color(0.8f, 0.2f, 0.2f, 0.9f));
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-4f, -4f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.raycastTarget = true;
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(HideVisitorInfo);
            var closeLabel = MakeLabel(closeRt, "X", "X", 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLabel.rectTransform);

            float lineH = 22f;
            float y = panelH - 34f;
            float lx = 12f;
            float lw = panelW - 24f;

            _viName    = MakeInfoLine(rt, "Name",    ref y, lineH, lx, lw, Color.white, FontStyle.Bold, 16);
            _viType    = MakeInfoLine(rt, "Type",    ref y, lineH, lx, lw, Cyan, FontStyle.Normal, 14);
            _viState   = MakeInfoLine(rt, "State",   ref y, lineH, lx, lw, Muted, FontStyle.Normal, 14);
            y -= 6f;

            // ---- 満足度メーター ----
            _viSatisfaction = MakeInfoLine(rt, "Satisfaction", ref y, lineH, lx, lw, Green, FontStyle.Bold, 14);
            // 満足度バー
            float barW = lw;
            float barH = 10f;
            float barX = lx;
            var satBarBg = MakePanel(rt, "SatBarBg", barW, barH, new Color(0.15f, 0.18f, 0.25f));
            PlaceInParent(satBarBg.GetComponent<RectTransform>(), barX, y, barW, barH, new Vector2(0f, 1f));
            _viSatBar = satBarBg;

            var satBarFill = MakePanel(rt, "SatBarFill", barW, barH, Green);
            PlaceInParent(satBarFill.GetComponent<RectTransform>(), barX, y, barW, barH, new Vector2(0f, 1f));
            _viSatBarFill = satBarFill.GetComponent<Image>();
            y -= barH + 6f;

            _viHappiness  = MakeInfoLine(rt, "Happy",   ref y, lineH, lx, lw, Green, FontStyle.Normal, 14);
            _viExcitement = MakeInfoLine(rt, "Excite",  ref y, lineH, lx, lw, Gold, FontStyle.Normal, 14);
            _viCash       = MakeInfoLine(rt, "Cash",    ref y, lineH, lx, lw, Gold, FontStyle.Normal, 14);
            y -= 6f;
            _viHunger  = MakeInfoLine(rt, "Hunger",  ref y, lineH, lx, lw, Muted, FontStyle.Normal, 13);
            _viThirst  = MakeInfoLine(rt, "Thirst",  ref y, lineH, lx, lw, Muted, FontStyle.Normal, 13);
            _viNausea  = MakeInfoLine(rt, "Nausea",  ref y, lineH, lx, lw, Muted, FontStyle.Normal, 13);
            _viToilet  = MakeInfoLine(rt, "Toilet",  ref y, lineH, lx, lw, Muted, FontStyle.Normal, 13);
            y -= 6f;
            _viRides   = MakeInfoLine(rt, "Rides",   ref y, lineH, lx, lw, Cyan, FontStyle.Normal, 13);
            y -= 6f;
            _viLifecycle     = MakeInfoLine(rt, "Lifecycle",     ref y, lineH, lx, lw, Cyan, FontStyle.Bold, 13);
            _viLifecycleStats = MakeInfoLine(rt, "LifecycleStats", ref y, lineH, lx, lw, Muted, FontStyle.Normal, 12);

            // ファーストパーソンビュー「搭乗」ボタン
            y -= 8f;
            var fpBtnGo = MakePanel(rt, "FirstPersonBtn", 120f, 30f, new Color(0.2f, 0.5f, 0.7f));
            var fpBtnRt = fpBtnGo.GetComponent<RectTransform>();
            fpBtnRt.anchorMin = fpBtnRt.anchorMax = new Vector2(0.5f, 0f);
            fpBtnRt.pivot = new Vector2(0.5f, 1f);
            fpBtnRt.anchoredPosition = new Vector2(0f, y);

            _viFirstPersonButton = fpBtnGo.AddComponent<Button>();
            _viFirstPersonButton.targetGraphic = fpBtnGo.GetComponent<Image>();
            var fpBtnColors = _viFirstPersonButton.colors;
            fpBtnColors.highlightedColor = new Color(0.25f, 0.6f, 0.82f);
            fpBtnColors.pressedColor = new Color(0.15f, 0.38f, 0.55f);
            _viFirstPersonButton.colors = fpBtnColors;

            var fpLabel = MakeLabel(fpBtnRt, "FPLabel", "搭乗", 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            var fpLabelRt = fpLabel.rectTransform;
            fpLabelRt.anchorMin = Vector2.zero;
            fpLabelRt.anchorMax = Vector2.one;
            fpLabelRt.offsetMin = Vector2.zero;
            fpLabelRt.offsetMax = Vector2.zero;

            _viFirstPersonButton.onClick.AddListener(OnFirstPersonButtonClicked);

            _visitorInfoPanel.SetActive(false);
        }

        private Text MakeInfoLine(RectTransform parent, string name, ref float y, float h,
            float x, float w, Color color, FontStyle style, int fontSize)
        {
            var t = MakeLabel(parent, name, "", fontSize, color, style, TextAnchor.MiddleLeft);
            PlaceInParent(t.rectTransform, x, y, w, h, new Vector2(0f, 1f));
            y -= h;
            return t;
        }

        // ================================================================
        // メニューボタン（画面左上）
        // ================================================================

        private void BuildMenuButton(RectTransform root)
        {
            var btnGo = MakePanel(root, "MenuBtn", 90f, 36f, new Color(0.3f, 0.35f, 0.5f, 0.9f));
            _menuBtn = btnGo;
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, -10f);

            var img = btnGo.GetComponent<Image>();
            img.raycastTarget = true;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.4f, 0.45f, 0.6f);
            colors.pressedColor = new Color(0.2f, 0.25f, 0.4f);
            btn.colors = colors;
            btn.onClick.AddListener(OnMenuClicked);

            var label = MakeLabel(rt, "Label", "MENU", 18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(label.rectTransform);

            // 通知バッジ（未読数）
            _notifBadge = MakePanel(rt, "NotifBadge", 24f, 24f, new Color(0.9f, 0.25f, 0.2f));
            var badgeRt = _notifBadge.GetComponent<RectTransform>();
            badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(4f, 4f);
            _notifBadgeText = MakeLabel(badgeRt, "Count", "0", 12, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(_notifBadgeText.rectTransform);
            _notifBadge.SetActive(false);
        }

        // ================================================================
        // ポーズオーバーレイ
        // ================================================================

        private void BuildPauseOverlay(RectTransform root)
        {
            _pauseOverlay = new GameObject("PauseOverlay");
            _pauseOverlay.transform.SetParent(root, false);
            var rt = _pauseOverlay.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bgImg = _pauseOverlay.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.7f);
            bgImg.raycastTarget = true;

            // タイトル
            var title = MakeLabel(rt, "PauseTitle", "PAUSED", 56, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, 120f);
            titleRt.sizeDelta = new Vector2(400f, 70f);

            // 「続ける」ボタン
            MakeCenterButton(rt, "ResumeBtn", "続ける",
                new Color(0.18f, 0.55f, 0.34f), new Color(0.22f, 0.65f, 0.40f), new Color(0.14f, 0.45f, 0.28f),
                new Vector2(0f, 120f), OnResumeClicked);

            // 「セーブ/ロード」ボタン
            MakeCenterButton(rt, "SaveLoadBtn", "SAVE / LOAD",
                new Color(0.3f, 0.4f, 0.6f), new Color(0.38f, 0.5f, 0.72f), new Color(0.22f, 0.3f, 0.48f),
                new Vector2(0f, 45f), OnSaveLoadClicked);

            // 「実績」ボタン
            MakeCenterButton(rt, "AchievementBtn", "ACHIEVEMENTS",
                new Color(0.55f, 0.45f, 0.2f), new Color(0.65f, 0.55f, 0.28f), new Color(0.42f, 0.34f, 0.15f),
                new Vector2(0f, -30f), OnAchievementClicked);

            // 「イベントログ」ボタン
            MakeCenterButton(rt, "EventLogBtn", "EVENT LOG",
                new Color(0.3f, 0.45f, 0.55f), new Color(0.38f, 0.55f, 0.65f), new Color(0.22f, 0.35f, 0.44f),
                new Vector2(0f, -105f), OnEventLogClicked);

            // 「ゲーム終了」ボタン
            MakeCenterButton(rt, "EndGameBtn", "ゲーム終了",
                new Color(0.65f, 0.2f, 0.2f), new Color(0.75f, 0.3f, 0.3f), new Color(0.5f, 0.15f, 0.15f),
                new Vector2(0f, -180f), OnEndGameClicked);

            _pauseOverlay.SetActive(false);
        }

        // ================================================================
        // ゲームオーバー画面（結果・最終スコア・リスタート）
        // ================================================================

        private void BuildResultsOverlay(RectTransform root)
        {
            _resultsOverlay = new GameObject("ResultsOverlay");
            _resultsOverlay.transform.SetParent(root, false);
            var rt = _resultsOverlay.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bgImg = _resultsOverlay.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.05f, 0.12f, 0.96f);
            bgImg.raycastTarget = true;

            // タイトル
            var title = MakeLabel(rt, "ResultsTitle", "GAME OVER", 60, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, 280f);
            titleRt.sizeDelta = new Vector2(600f, 70f);

            // サブタイトル
            var sub = MakeLabel(rt, "ResultsSub", "- Final Score Report -", 22, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            var subRt = sub.rectTransform;
            subRt.anchorMin = subRt.anchorMax = new Vector2(0.5f, 0.5f);
            subRt.anchoredPosition = new Vector2(0f, 230f);
            subRt.sizeDelta = new Vector2(400f, 30f);

            // ---- 最終スコア（巨大表示） ----
            var scoreLbl = MakeLabel(rt, "ScoreLabel", "TOTAL SCORE", 18, new Color(0.5f, 0.6f, 0.7f),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var scoreLblRt = scoreLbl.rectTransform;
            scoreLblRt.anchorMin = scoreLblRt.anchorMax = new Vector2(0.5f, 0.5f);
            scoreLblRt.anchoredPosition = new Vector2(0f, 185f);
            scoreLblRt.sizeDelta = new Vector2(400f, 26f);

            _resultsFinalScore = MakeLabel(rt, "FinalScore", "0", 72, new Color(1f, 0.95f, 0.5f),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var fsRt = _resultsFinalScore.rectTransform;
            fsRt.anchorMin = fsRt.anchorMax = new Vector2(0.5f, 0.5f);
            fsRt.anchoredPosition = new Vector2(0f, 135f);
            fsRt.sizeDelta = new Vector2(500f, 80f);

            // ---- 区切り線 ----
            var sepGo = MakePanel(rt, "Separator", 400f, 2f, new Color(0.3f, 0.35f, 0.45f, 0.5f));
            var sepRt = sepGo.GetComponent<RectTransform>();
            sepRt.anchorMin = sepRt.anchorMax = new Vector2(0.5f, 0.5f);
            sepRt.anchoredPosition = new Vector2(0f, 88f);

            // ---- 詳細スコアボディ ----
            _resultsBody = MakeLabel(rt, "ResultsBody", "", 20, Color.white, FontStyle.Normal, TextAnchor.UpperCenter);
            _resultsBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _resultsBody.verticalOverflow = VerticalWrapMode.Overflow;
            _resultsBody.lineSpacing = 1.3f;
            var bodyRt = _resultsBody.rectTransform;
            bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRt.anchoredPosition = new Vector2(0f, -20f);
            bodyRt.sizeDelta = new Vector2(520f, 200f);

            // ---- リスタートボタン ----
            MakeCenterButton(rt, "RestartBtn", "RESTART",
                new Color(0.18f, 0.55f, 0.34f), new Color(0.22f, 0.65f, 0.40f), new Color(0.14f, 0.45f, 0.28f),
                new Vector2(0f, -175f), OnRestartClicked);

            // ---- メインメニューに戻るボタン ----
            MakeCenterButton(rt, "ReturnMenuBtn", "TITLE MENU",
                new Color(0.3f, 0.35f, 0.45f), new Color(0.4f, 0.45f, 0.55f), new Color(0.2f, 0.25f, 0.35f),
                new Vector2(0f, -250f), OnReturnToMenuClicked);

            _resultsOverlay.SetActive(false);
        }

        // ================================================================
        // シナリオ目標パネル（右上、シナリオモード時のみ表示）
        // ================================================================

        private void BuildScenarioPanel(RectTransform root)
        {
            float panelW = 300f;
            float panelH = 180f;

            var bg = MakePanel(root, "ScenarioPanel", panelW, panelH, BgDark);
            _scenarioPanel = bg;
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -10f);

            _scenarioTitle = MakeLabel(rt, "ScTitle", "SCENARIO", 16, Gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_scenarioTitle.rectTransform, 0f, panelH - 4f, panelW, 24f, new Vector2(0f, 1f));

            _scenarioObjectives = MakeLabel(rt, "ScObjectives", "", 13, Color.white,
                FontStyle.Normal, TextAnchor.UpperLeft);
            _scenarioObjectives.horizontalOverflow = HorizontalWrapMode.Wrap;
            _scenarioObjectives.verticalOverflow = VerticalWrapMode.Overflow;
            _scenarioObjectives.lineSpacing = 1.3f;
            PlaceInParent(_scenarioObjectives.rectTransform, 10f, panelH - 32f,
                panelW - 20f, panelH - 40f, new Vector2(0f, 1f));

            _scenarioPanel.SetActive(false);
        }

        // ================================================================
        // セーブ/ロードパネル（ポーズ時に表示）
        // ================================================================

        private void BuildSaveLoadPanel(RectTransform root)
        {
            float panelW = 500f;
            float panelH = 340f;

            _saveLoadPanel = new GameObject("SaveLoadPanel");
            _saveLoadPanel.transform.SetParent(root, false);
            var rt = _saveLoadPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(panelW, panelH);

            var bgImg = _saveLoadPanel.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.08f, 0.16f, 0.98f);
            bgImg.raycastTarget = true;

            // タイトル
            var title = MakeLabel(rt, "SLTitle", "SAVE / LOAD", 28, Gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -10f);
            titleRt.sizeDelta = new Vector2(panelW, 36f);

            // 3スロット
            _slotTexts = new Text[SaveSystem.MaxSlots];
            float slotH = 60f;
            float slotY = panelH - 55f;

            for (int s = 0; s < SaveSystem.MaxSlots; s++)
            {
                float y = slotY - s * (slotH + 8f);
                CreateSaveSlotRow(rt, s, y, panelW, slotH);
            }

            // メッセージ表示
            _saveLoadMessage = MakeLabel(rt, "SLMsg", "", 16, Green,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            var msgRt = _saveLoadMessage.rectTransform;
            msgRt.anchorMin = msgRt.anchorMax = new Vector2(0.5f, 0f);
            msgRt.pivot = new Vector2(0.5f, 0f);
            msgRt.anchoredPosition = new Vector2(0f, 42f);
            msgRt.sizeDelta = new Vector2(panelW - 20f, 24f);

            // 閉じるボタン
            var closeGo = MakePanel(rt, "SLClose", 140f, 36f, new Color(0.4f, 0.42f, 0.5f));
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 8f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.raycastTarget = true;
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() => _saveLoadPanel.SetActive(false));
            var closeLabel = MakeLabel(closeRt, "Label", "CLOSE", 18, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLabel.rectTransform);

            _saveLoadPanel.SetActive(false);
        }

        private void CreateSaveSlotRow(RectTransform parent, int slot, float y, float panelW, float h)
        {
            // スロット情報
            var infoBg = MakePanel(parent, $"Slot{slot}Bg", panelW - 20f, h,
                new Color(0.1f, 0.12f, 0.2f, 0.9f));
            var infoRt = infoBg.GetComponent<RectTransform>();
            infoRt.anchorMin = infoRt.anchorMax = new Vector2(0.5f, 1f);
            infoRt.pivot = new Vector2(0.5f, 1f);
            infoRt.anchoredPosition = new Vector2(0f, -y + h);

            // スロット番号
            var slotLabel = MakeLabel(infoRt, "SlotNum", $"Slot {slot + 1}", 14, Muted,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(slotLabel.rectTransform, 10f, h - 2f, 80f, 20f, new Vector2(0f, 1f));

            // スロット概要
            _slotTexts[slot] = MakeLabel(infoRt, "SlotInfo", SaveSystem.GetSlotSummary(slot),
                13, Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_slotTexts[slot].rectTransform, 10f, h - 22f,
                panelW - 180f, 20f, new Vector2(0f, 1f));

            float btnW = 65f;
            float btnH = 32f;
            float btnY = (h - btnH) / 2f;

            // SAVEボタン
            var saveGo = MakePanel(infoRt, $"SaveBtn{slot}", btnW, btnH,
                new Color(0.2f, 0.5f, 0.35f));
            var saveRt = saveGo.GetComponent<RectTransform>();
            saveRt.anchorMin = saveRt.anchorMax = new Vector2(1f, 0.5f);
            saveRt.pivot = new Vector2(1f, 0.5f);
            saveRt.anchoredPosition = new Vector2(-btnW - 12f, 0f);
            var saveImg = saveGo.GetComponent<Image>();
            saveImg.raycastTarget = true;
            var saveBtn = saveGo.AddComponent<Button>();
            saveBtn.targetGraphic = saveImg;
            var sc = saveBtn.colors;
            sc.highlightedColor = new Color(0.25f, 0.6f, 0.42f);
            sc.pressedColor = new Color(0.15f, 0.38f, 0.25f);
            saveBtn.colors = sc;
            var saveTxt = MakeLabel(saveRt, "L", "SAVE", 14, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(saveTxt.rectTransform);

            int s = slot;
            saveBtn.onClick.AddListener(() => OnSaveSlot(s));

            // LOADボタン
            var loadGo = MakePanel(infoRt, $"LoadBtn{slot}", btnW, btnH,
                new Color(0.3f, 0.4f, 0.6f));
            var loadRt = loadGo.GetComponent<RectTransform>();
            loadRt.anchorMin = loadRt.anchorMax = new Vector2(1f, 0.5f);
            loadRt.pivot = new Vector2(1f, 0.5f);
            loadRt.anchoredPosition = new Vector2(-6f, 0f);
            var loadImg = loadGo.GetComponent<Image>();
            loadImg.raycastTarget = true;
            var loadBtn = loadGo.AddComponent<Button>();
            loadBtn.targetGraphic = loadImg;
            var lc = loadBtn.colors;
            lc.highlightedColor = new Color(0.38f, 0.5f, 0.72f);
            lc.pressedColor = new Color(0.22f, 0.3f, 0.48f);
            loadBtn.colors = lc;
            var loadTxt = MakeLabel(loadRt, "L", "LOAD", 14, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(loadTxt.rectTransform);

            loadBtn.onClick.AddListener(() => OnLoadSlot(s));
        }

        private void OnSaveLoadClicked()
        {
            if (_saveLoadPanel == null) return;
            RefreshSaveSlots();
            _saveLoadMessage.text = "";
            _saveLoadPanel.SetActive(true);
        }

        private void OnSaveSlot(int slot)
        {
            bool success = SaveSystem.Save(slot);
            if (_saveLoadMessage != null)
            {
                _saveLoadMessage.text = success
                    ? $"Slot {slot + 1} にセーブしました"
                    : "セーブに失敗しました";
                _saveLoadMessage.color = success ? Green : Red;
            }
            RefreshSaveSlots();
        }

        private void OnLoadSlot(int slot)
        {
            if (!SaveSystem.HasSaveData(slot))
            {
                if (_saveLoadMessage != null)
                {
                    _saveLoadMessage.text = $"Slot {slot + 1} にデータがありません";
                    _saveLoadMessage.color = Yellow;
                }
                return;
            }

            bool success = SaveSystem.Load(slot);
            if (success)
            {
                // ロード成功 → ポーズ解除してセーブロードパネルを閉じる
                _saveLoadPanel.SetActive(false);
                if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
            }
            else if (_saveLoadMessage != null)
            {
                _saveLoadMessage.text = "ロードに失敗しました";
                _saveLoadMessage.color = Red;
            }
        }

        private void RefreshSaveSlots()
        {
            if (_slotTexts == null) return;
            for (int i = 0; i < _slotTexts.Length; i++)
            {
                if (_slotTexts[i] != null)
                    _slotTexts[i].text = SaveSystem.GetSlotSummary(i);
            }
        }

        /// <summary>中央配置のボタンを作成するヘルパー</summary>
        private void MakeCenterButton(RectTransform parent, string name, string label,
            Color normal, Color highlight, Color pressed, Vector2 pos,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = MakePanel(parent, name, 320f, 60f, normal);
            var goRt = go.GetComponent<RectTransform>();
            goRt.anchorMin = goRt.anchorMax = new Vector2(0.5f, 0.5f);
            goRt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            c.highlightedColor = highlight;
            c.pressedColor = pressed;
            btn.colors = c;
            btn.onClick.AddListener(onClick);
            var txt = MakeLabel(goRt, "Label", label, 30, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(txt.rectTransform);
        }

        // ================================================================
        // メニュー/ポーズ/結果のイベントハンドラ
        // ================================================================

        private void OnMenuClicked()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.PauseGame();
        }

        private void OnResumeClicked()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.ResumeGame();
        }

        private void OnAchievementClicked()
        {
            if (AchievementSystem.Instance != null)
                AchievementSystem.Instance.ShowAchievementList();
        }

        private void OnEventLogClicked()
        {
            if (NotificationSystem.Instance != null)
                NotificationSystem.Instance.ShowLogPanel();
        }

        private void OnEndGameClicked()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.EndGame();
        }

        private void OnRestartClicked()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.RestartGame();
        }

        private void OnReturnToMenuClicked()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.ReturnToMainMenu();
        }

        /// <summary>
        /// スコアを計算する。
        /// = (来場者数 × 10) + (総収益 / 100) + (平均満足度 × 50) + (ゴールデンチケット × 500)
        /// </summary>
        private int CalculateFinalScore()
        {
            var gm = GameManager.Instance;
            if (gm == null) return 0;

            int visitorScore = (gm.VisitorManager != null) ? gm.VisitorManager.TotalVisitorsToday * 10 : 0;
            int revenueScore = (gm.EconomyManager != null) ? (int)(gm.EconomyManager.TotalRevenueEarned / 100f) : 0;
            int satisfactionScore = (gm.VisitorManager != null) ? (int)(gm.VisitorManager.AverageHappiness * 50f) : 0;
            int ticketScore = gm.GoldenTickets * 500;

            return visitorScore + revenueScore + satisfactionScore + ticketScore;
        }

        private void RefreshResults()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // シナリオモードの場合はクリア/失敗表示を切替
            bool isScenario = ScenarioManager.Instance != null && ScenarioManager.Instance.IsScenarioActive;

            // 最終スコア
            int score = CalculateFinalScore();
            if (_resultsFinalScore != null)
            {
                if (isScenario && ScenarioManager.Instance.IsScenarioCleared)
                    _resultsFinalScore.text = "CLEAR!";
                else if (isScenario && ScenarioManager.Instance.IsScenarioFailed)
                    _resultsFinalScore.text = "FAILED";
                else
                    _resultsFinalScore.text = score.ToString("N0");
            }

            if (_resultsBody == null) return;

            string money = gm.EconomyManager != null ? $"${gm.EconomyManager.CurrentMoney:N0}" : "---";
            string revenue = gm.EconomyManager != null ? $"${gm.EconomyManager.TotalRevenueEarned:N0}" : "---";
            string expenses = gm.EconomyManager != null ? $"${gm.EconomyManager.TotalExpensesPaid:N0}" : "---";
            string visitors = gm.VisitorManager != null ? $"{gm.VisitorManager.TotalVisitorsToday}" : "---";
            string peak = gm.VisitorManager != null ? $"{gm.VisitorManager.PeakVisitorCount:F0}" : "---";
            float avgSatisfaction = gm.VisitorManager != null ? gm.VisitorManager.AverageSatisfaction : 0f;
            float avgHappiness = gm.VisitorManager != null ? gm.VisitorManager.AverageHappiness : 0f;
            string tickets = $"{gm.GoldenTickets}";

            string time = "---";
            if (gm.TimeManager != null)
            {
                var tm = gm.TimeManager;
                time = $"Y{tm.CurrentYear} M{tm.CurrentMonth} D{tm.CurrentDay}";
            }

            int attrCount = (_attractions != null) ? _attractions.Length : 0;

            string achText = AchievementSystem.Instance != null
                ? $"{AchievementSystem.Instance.UnlockedCount}/{AchievementSystem.Instance.TotalCount}"
                : "---";

            // ライフサイクル統計
            string lcText = "";
            if (gm.VisitorManager != null)
            {
                float enjoyRatio = gm.VisitorManager.OverallEnjoymentRatio * 100f;
                int expStarts = gm.VisitorManager.TotalExperienceStarts;
                int expDone = gm.VisitorManager.TotalExperienceCompletions;
                lcText = $"  Lifecycle: Enjoy {enjoyRatio:F0}%  Starts:{expStarts}  Done:{expDone}\n";
            }

            // パーク評価
            string ratingText = "";
            if (gm.ParkManager != null && gm.ParkManager.Rating != null)
            {
                float overall = gm.ParkManager.Rating.OverallRating;
                float stars = ParkRatingEvaluator.ScoreToStars(overall);
                string starsStr = ParkRatingEvaluator.StarsToText(stars);
                string label = ParkRatingEvaluator.GetRatingLabel(overall);
                ratingText = $"  Park Rating: [{starsStr}] {overall:F1} - {label}\n";
            }

            _resultsBody.text =
                $"  Visitors: {visitors}  (Peak: {peak})\n" +
                $"  Satisfaction: {avgSatisfaction:F0}%  Happiness: {avgHappiness:F0}%\n" +
                ratingText +
                lcText +
                $"  Revenue: {revenue}         Expenses: {expenses}\n" +
                $"  Final Balance: {money}\n" +
                $"  Attractions: {attrCount}         Golden Tickets: {tickets}\n" +
                $"  Date: {time}         Achievements: {achText}\n" +
                $"\n" +
                $"  Score Breakdown:\n" +
                $"    Visitors x10 = {(gm.VisitorManager != null ? gm.VisitorManager.TotalVisitorsToday * 10 : 0):N0}\n" +
                $"    Revenue / 100 = {(gm.EconomyManager != null ? (int)(gm.EconomyManager.TotalRevenueEarned / 100f) : 0):N0}\n" +
                $"    Satisfaction x50 = {(int)(avgSatisfaction * 50f):N0}\n" +
                $"    Golden Tickets x500 = {gm.GoldenTickets * 500:N0}";
        }

        // ================================================================
        // イベントパネル（InfoBar下 左寄せ）
        // ================================================================

        private void BuildEventPanel(RectTransform root)
        {
            float panelW = 400f;
            float panelH = 24f;

            _eventPanel = MakePanel(root, "EventPanel", panelW, panelH,
                new Color(0.12f, 0.08f, 0.22f, 0.88f));
            var rt = _eventPanel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, -122f);

            _eventText = MakeLabel(rt, "EventText", "", 12,
                new Color(1f, 0.85f, 0.4f), FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(_eventText.rectTransform, 8f, panelH, panelW - 16f, panelH, new Vector2(0f, 1f));

            _eventPanel.SetActive(false);
        }

        private void UpdateEventPanel()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ParkEventSystem == null)
            {
                if (_eventPanel != null) _eventPanel.SetActive(false);
                return;
            }

            var eventSys = gm.ParkEventSystem;
            if (eventSys.ActiveEventCount == 0)
            {
                if (_eventPanel != null) _eventPanel.SetActive(false);
                return;
            }

            _eventPanel.SetActive(true);
            _eventText.text = eventSys.GetActiveEventsSummary();

            // ショー開催中は点滅エフェクト
            _eventBlinkTimer += Time.deltaTime;
            bool hasShow = false;
            foreach (var ae in eventSys.CurrentEvents)
            {
                if (ae.Data.Type == ParkEventType.SpecialShow)
                {
                    hasShow = true;
                    break;
                }
            }

            if (hasShow)
            {
                float blink = (Mathf.Sin(_eventBlinkTimer * 4f) + 1f) * 0.5f;
                _eventText.color = Color.Lerp(
                    new Color(1f, 0.85f, 0.4f),
                    new Color(1f, 0.5f, 0.2f),
                    blink);
            }
            else
            {
                _eventText.color = new Color(1f, 0.85f, 0.4f);
            }
        }

        // ================================================================
        // ライフサイクルフェーズ分布パネル（来場者パネルの上 220x90）
        // ================================================================

        private void BuildLifecyclePanel(RectTransform root)
        {
            float panelW = 220f;
            float panelH = 90f;

            var bg = MakePanel(root, "LifecyclePanel", panelW, panelH, BgDark);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(10f, 216f);

            var header = MakeLabel(rt, "LCHeader", "ライフサイクル", 14, Cyan, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(header.rectTransform, 0f, panelH - 2f, panelW, 20f, new Vector2(0f, 1f));

            float lineH = 20f;
            float barW = 80f;
            float barH = 10f;
            float y = panelH - 24f;

            // Waiting
            var waitDot = MakePanel(rt, "WaitDot", 10f, 10f, new Color(0.3f, 0.7f, 1.0f));
            var waitDotRt = waitDot.GetComponent<RectTransform>();
            waitDotRt.anchorMin = waitDotRt.anchorMax = new Vector2(0f, 0f);
            waitDotRt.pivot = new Vector2(0f, 0.5f);
            waitDotRt.anchoredPosition = new Vector2(8f, y - lineH * 0.5f);

            _lcWaitingText = MakeLabel(rt, "LCWait", "待機: 0", 12, new Color(0.3f, 0.7f, 1.0f),
                FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_lcWaitingText.rectTransform, 22f, y, 90f, lineH, new Vector2(0f, 1f));

            var wBarBg = MakePanel(rt, "WBarBg", barW, barH, new Color(0.15f, 0.15f, 0.2f));
            var wBarBgRt = wBarBg.GetComponent<RectTransform>();
            wBarBgRt.anchorMin = wBarBgRt.anchorMax = new Vector2(0f, 0f);
            wBarBgRt.pivot = new Vector2(0f, 0.5f);
            wBarBgRt.anchoredPosition = new Vector2(panelW - barW - 10f, y - lineH * 0.5f);

            var wFill = MakePanel(wBarBgRt, "WBarFill", 0f, barH, new Color(0.3f, 0.7f, 1.0f));
            var wFillRt = wFill.GetComponent<RectTransform>();
            wFillRt.anchorMin = new Vector2(0f, 0f);
            wFillRt.anchorMax = new Vector2(0f, 1f);
            wFillRt.pivot = new Vector2(0f, 0.5f);
            wFillRt.anchoredPosition = Vector2.zero;
            wFillRt.sizeDelta = new Vector2(0f, 0f);
            _lcWaitingBar = wFill.GetComponent<Image>();

            y -= lineH;

            // Enjoying
            var enjoyDot = MakePanel(rt, "EnjoyDot", 10f, 10f, new Color(1.0f, 0.6f, 0.1f));
            var enjoyDotRt = enjoyDot.GetComponent<RectTransform>();
            enjoyDotRt.anchorMin = enjoyDotRt.anchorMax = new Vector2(0f, 0f);
            enjoyDotRt.pivot = new Vector2(0f, 0.5f);
            enjoyDotRt.anchoredPosition = new Vector2(8f, y - lineH * 0.5f);

            _lcEnjoyingText = MakeLabel(rt, "LCEnjoy", "体験: 0", 12, new Color(1.0f, 0.6f, 0.1f),
                FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_lcEnjoyingText.rectTransform, 22f, y, 90f, lineH, new Vector2(0f, 1f));

            var eBarBg = MakePanel(rt, "EBarBg", barW, barH, new Color(0.15f, 0.15f, 0.2f));
            var eBarBgRt = eBarBg.GetComponent<RectTransform>();
            eBarBgRt.anchorMin = eBarBgRt.anchorMax = new Vector2(0f, 0f);
            eBarBgRt.pivot = new Vector2(0f, 0.5f);
            eBarBgRt.anchoredPosition = new Vector2(panelW - barW - 10f, y - lineH * 0.5f);

            var eFill = MakePanel(eBarBgRt, "EBarFill", 0f, barH, new Color(1.0f, 0.6f, 0.1f));
            var eFillRt = eFill.GetComponent<RectTransform>();
            eFillRt.anchorMin = new Vector2(0f, 0f);
            eFillRt.anchorMax = new Vector2(0f, 1f);
            eFillRt.pivot = new Vector2(0f, 0.5f);
            eFillRt.anchoredPosition = Vector2.zero;
            eFillRt.sizeDelta = new Vector2(0f, 0f);
            _lcEnjoyingBar = eFill.GetComponent<Image>();

            y -= lineH;

            // Leaving
            var leaveDot = MakePanel(rt, "LeaveDot", 10f, 10f, new Color(0.5f, 0.5f, 0.5f));
            var leaveDotRt = leaveDot.GetComponent<RectTransform>();
            leaveDotRt.anchorMin = leaveDotRt.anchorMax = new Vector2(0f, 0f);
            leaveDotRt.pivot = new Vector2(0f, 0.5f);
            leaveDotRt.anchoredPosition = new Vector2(8f, y - lineH * 0.5f);

            _lcLeavingText = MakeLabel(rt, "LCLeave", "退園: 0", 12, new Color(0.5f, 0.5f, 0.5f),
                FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_lcLeavingText.rectTransform, 22f, y, 90f, lineH, new Vector2(0f, 1f));

            var lBarBg = MakePanel(rt, "LBarBg", barW, barH, new Color(0.15f, 0.15f, 0.2f));
            var lBarBgRt = lBarBg.GetComponent<RectTransform>();
            lBarBgRt.anchorMin = lBarBgRt.anchorMax = new Vector2(0f, 0f);
            lBarBgRt.pivot = new Vector2(0f, 0.5f);
            lBarBgRt.anchoredPosition = new Vector2(panelW - barW - 10f, y - lineH * 0.5f);

            var lFill = MakePanel(lBarBgRt, "LBarFill", 0f, barH, new Color(0.5f, 0.5f, 0.5f));
            var lFillRt = lFill.GetComponent<RectTransform>();
            lFillRt.anchorMin = new Vector2(0f, 0f);
            lFillRt.anchorMax = new Vector2(0f, 1f);
            lFillRt.pivot = new Vector2(0f, 0.5f);
            lFillRt.anchoredPosition = Vector2.zero;
            lFillRt.sizeDelta = new Vector2(0f, 0f);
            _lcLeavingBar = lFill.GetComponent<Image>();
        }

        private void UpdateLifecyclePanel()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.VisitorManager == null) return;

            var visitors = gm.VisitorManager.GetAllActiveVisitors();
            int total = visitors.Count;
            if (total == 0)
            {
                if (_lcWaitingText != null) _lcWaitingText.text = "待機: 0";
                if (_lcEnjoyingText != null) _lcEnjoyingText.text = "体験: 0";
                if (_lcLeavingText != null) _lcLeavingText.text = "退園: 0";
                SetBarWidth(_lcWaitingBar, 0f, 80f);
                SetBarWidth(_lcEnjoyingBar, 0f, 80f);
                SetBarWidth(_lcLeavingBar, 0f, 80f);
                return;
            }

            int waitCount = 0, enjoyCount = 0, leaveCount = 0;
            for (int i = 0; i < visitors.Count; i++)
            {
                var sm = visitors[i].StateMachine;
                if (sm == null)
                {
                    // StateMachine未設定の場合はBehaviorStateから推定
                    var phase = VisitorStateMachine.MapBehaviorToPhase(visitors[i].CurrentState);
                    switch (phase)
                    {
                        case VisitorLifecyclePhase.Waiting:  waitCount++; break;
                        case VisitorLifecyclePhase.Enjoying: enjoyCount++; break;
                        case VisitorLifecyclePhase.Leaving:  leaveCount++; break;
                    }
                }
                else
                {
                    switch (sm.CurrentPhase)
                    {
                        case VisitorLifecyclePhase.Waiting:  waitCount++; break;
                        case VisitorLifecyclePhase.Enjoying: enjoyCount++; break;
                        case VisitorLifecyclePhase.Leaving:  leaveCount++; break;
                    }
                }
            }

            float barMaxW = 80f;

            if (_lcWaitingText != null)
                _lcWaitingText.text = $"待機: {waitCount}";
            SetBarWidth(_lcWaitingBar, (float)waitCount / total, barMaxW);

            if (_lcEnjoyingText != null)
                _lcEnjoyingText.text = $"体験: {enjoyCount}";
            SetBarWidth(_lcEnjoyingBar, (float)enjoyCount / total, barMaxW);

            if (_lcLeavingText != null)
                _lcLeavingText.text = $"退園: {leaveCount}";
            SetBarWidth(_lcLeavingBar, (float)leaveCount / total, barMaxW);
        }

        private static void SetBarWidth(Image bar, float ratio, float maxW)
        {
            if (bar == null) return;
            var rt = bar.rectTransform;
            rt.sizeDelta = new Vector2(maxW * Mathf.Clamp01(ratio), 0f);
        }

        // ================================================================
        // パーク評価パネル（右上 240x170）
        // ================================================================

        private void BuildRatingPanel(RectTransform root)
        {
            float panelW = 240f;
            float panelH = 170f;

            var bg = MakePanel(root, "RatingPanel", panelW, panelH, BgDark);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -52f);

            // ヘッダー
            var header = MakeLabel(rt, "RHeader", "パーク評価", 14, Cyan, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(header.rectTransform, 0f, panelH - 2f, panelW, 18f, new Vector2(0f, 1f));

            // 星テキスト（大きめ）
            _ratingStarsText = MakeLabel(rt, "Stars", "-----", 22, new Color(1f, 0.85f, 0.3f),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_ratingStarsText.rectTransform, 0f, panelH - 22f, panelW, 24f, new Vector2(0f, 1f));

            // スコア + ラベル
            _ratingScoreText = MakeLabel(rt, "Score", "0.0", 13, Color.white,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_ratingScoreText.rectTransform, 8f, panelH - 48f, 80f, 16f, new Vector2(0f, 1f));

            _ratingLabelText = MakeLabel(rt, "Label", "", 13, Muted,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceInParent(_ratingLabelText.rectTransform, 85f, panelH - 48f, 100f, 16f, new Vector2(0f, 1f));

            _ratingTrendText = MakeLabel(rt, "Trend", "-", 13, Muted,
                FontStyle.Bold, TextAnchor.MiddleRight);
            PlaceInParent(_ratingTrendText.rectTransform, panelW - 40f, panelH - 48f, 30f, 16f, new Vector2(0f, 1f));

            // カテゴリバー（5行）
            var categories = new[] {
                CertificateCategory.Fame, CertificateCategory.Safety,
                CertificateCategory.Comfort, CertificateCategory.Excitement,
                CertificateCategory.Mood
            };
            _ratingCatTexts = new Text[5];
            _ratingCatBars = new Image[5];

            float barMaxW = 100f;
            float barH = 8f;
            float lineH = 18f;
            float startY = panelH - 68f;

            for (int i = 0; i < 5; i++)
            {
                float y = startY - i * lineH;
                string catName = ParkRatingEvaluator.GetCategoryLabel(categories[i]);

                _ratingCatTexts[i] = MakeLabel(rt, $"Cat{i}", $"{catName}: 0", 11, Muted,
                    FontStyle.Normal, TextAnchor.MiddleLeft);
                PlaceInParent(_ratingCatTexts[i].rectTransform, 8f, y, 90f, lineH, new Vector2(0f, 1f));

                // バー背景
                var barBg = MakePanel(rt, $"CatBarBg{i}", barMaxW, barH, new Color(0.15f, 0.15f, 0.2f));
                var barBgRt = barBg.GetComponent<RectTransform>();
                barBgRt.anchorMin = barBgRt.anchorMax = new Vector2(0f, 0f);
                barBgRt.pivot = new Vector2(0f, 0.5f);
                barBgRt.anchoredPosition = new Vector2(panelW - barMaxW - 10f, y - lineH * 0.5f);

                // バーフィル
                var fill = MakePanel(barBgRt, $"CatBarFill{i}", 0f, barH, GetCategoryColor(i));
                var fillRt = fill.GetComponent<RectTransform>();
                fillRt.anchorMin = new Vector2(0f, 0f);
                fillRt.anchorMax = new Vector2(0f, 1f);
                fillRt.pivot = new Vector2(0f, 0.5f);
                fillRt.anchoredPosition = Vector2.zero;
                fillRt.sizeDelta = new Vector2(0f, 0f);
                _ratingCatBars[i] = fill.GetComponent<Image>();
            }
        }

        private static Color GetCategoryColor(int idx)
        {
            switch (idx)
            {
                case 0: return new Color(0.9f, 0.7f, 0.2f);  // Fame: 金
                case 1: return new Color(0.3f, 0.8f, 0.4f);  // Safety: 緑
                case 2: return new Color(0.4f, 0.7f, 0.9f);  // Comfort: 水色
                case 3: return new Color(0.9f, 0.4f, 0.3f);  // Excitement: 赤
                case 4: return new Color(0.8f, 0.5f, 0.9f);  // Mood: 紫
                default: return Color.white;
            }
        }

        private void UpdateRatingPanel()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ParkManager == null || gm.ParkManager.Rating == null) return;

            var evaluator = gm.ParkRatingEvaluator;
            var rating = gm.ParkManager.Rating;
            float overall = rating.OverallRating;

            // 星表示
            if (_ratingStarsText != null)
            {
                float stars = evaluator != null ? evaluator.StarRating : ParkRatingEvaluator.ScoreToStars(overall);
                _ratingStarsText.text = ParkRatingEvaluator.StarsToText(stars);
            }

            // スコア
            if (_ratingScoreText != null)
                _ratingScoreText.text = $"{overall:F1}";

            // ラベル
            if (_ratingLabelText != null)
                _ratingLabelText.text = ParkRatingEvaluator.GetRatingLabel(overall);

            // トレンド
            if (_ratingTrendText != null && evaluator != null)
            {
                string arrow = ParkRatingEvaluator.GetTrendArrow(evaluator.Trend);
                _ratingTrendText.text = arrow;
                _ratingTrendText.color = evaluator.Trend > 0.5f ? new Color(0.4f, 0.95f, 0.5f)
                    : evaluator.Trend < -0.5f ? new Color(0.95f, 0.4f, 0.4f) : Muted;
            }

            // カテゴリバー
            var categories = new[] {
                CertificateCategory.Fame, CertificateCategory.Safety,
                CertificateCategory.Comfort, CertificateCategory.Excitement,
                CertificateCategory.Mood
            };

            float barMaxW = 100f;
            for (int i = 0; i < 5; i++)
            {
                float score = rating.GetCategoryScore(categories[i]);
                bool cert = rating.IsCertificateAwarded(categories[i]);
                string certMark = cert ? " [C]" : "";
                string catName = ParkRatingEvaluator.GetCategoryLabel(categories[i]);

                if (_ratingCatTexts[i] != null)
                {
                    _ratingCatTexts[i].text = $"{catName}: {score:F0}{certMark}";
                    _ratingCatTexts[i].color = cert ? new Color(0.95f, 0.88f, 0.45f) : Muted;
                }

                if (_ratingCatBars[i] != null)
                {
                    var barRt = _ratingCatBars[i].rectTransform;
                    barRt.sizeDelta = new Vector2(barMaxW * Mathf.Clamp01(score / 100f), 0f);
                }
            }
        }

        // ================================================================
        // ファーストパーソンビューオーバーレイ
        // ================================================================

        private void BuildFirstPersonOverlay(RectTransform root)
        {
            _fpvOverlay = new GameObject("FPVOverlay");
            _fpvOverlay.transform.SetParent(root, false);
            var overlayRt = _fpvOverlay.AddComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;

            // 上部ステータスバー
            var topBar = MakePanel(overlayRt, "FPVTopBar", 600f, 50f, new Color(0f, 0f, 0f, 0.6f));
            var topBarRt = topBar.GetComponent<RectTransform>();
            topBarRt.anchorMin = topBarRt.anchorMax = new Vector2(0.5f, 1f);
            topBarRt.pivot = new Vector2(0.5f, 1f);
            topBarRt.anchoredPosition = new Vector2(0f, -6f);

            _fpvStatusText = MakeLabel(topBarRt, "FPVStatus", "", 20, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_fpvStatusText.rectTransform, 0f, 50f, 580f, 28f, new Vector2(0f, 1f));

            _fpvHappinessText = MakeLabel(topBarRt, "FPVHappy", "", 14, new Color(0.5f, 1f, 0.5f),
                FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_fpvHappinessText.rectTransform, 10f, 22f, 200f, 18f, new Vector2(0f, 1f));

            _fpvPhaseText = MakeLabel(topBarRt, "FPVPhase", "", 14, new Color(0.3f, 0.7f, 1f),
                FontStyle.Normal, TextAnchor.MiddleRight);
            PlaceInParent(_fpvPhaseText.rectTransform, 590f, 22f, 200f, 18f, new Vector2(1f, 1f));

            // 下部ヒント
            _fpvHintText = MakeLabel(overlayRt, "FPVHint",
                "ドラッグで見回し | ESCで終了", 16,
                new Color(1f, 1f, 1f, 0.5f), FontStyle.Normal, TextAnchor.MiddleCenter);
            var hintRt = _fpvHintText.rectTransform;
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 20f);
            hintRt.sizeDelta = new Vector2(500f, 30f);

            // 「終了」ボタン（右上）
            var exitBtnGo = MakePanel(overlayRt, "FPVExitBtn", 100f, 36f, new Color(0.7f, 0.2f, 0.2f, 0.85f));
            var exitBtnRt = exitBtnGo.GetComponent<RectTransform>();
            exitBtnRt.anchorMin = exitBtnRt.anchorMax = new Vector2(1f, 1f);
            exitBtnRt.pivot = new Vector2(1f, 1f);
            exitBtnRt.anchoredPosition = new Vector2(-10f, -10f);

            _fpvExitButton = exitBtnGo.AddComponent<Button>();
            _fpvExitButton.targetGraphic = exitBtnGo.GetComponent<Image>();
            var exitColors = _fpvExitButton.colors;
            exitColors.highlightedColor = new Color(0.85f, 0.3f, 0.3f);
            exitColors.pressedColor = new Color(0.55f, 0.15f, 0.15f);
            _fpvExitButton.colors = exitColors;

            var exitLabel = MakeLabel(exitBtnRt, "ExitLabel", "終了", 18,
                Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            var exitLabelRt = exitLabel.rectTransform;
            exitLabelRt.anchorMin = Vector2.zero;
            exitLabelRt.anchorMax = Vector2.one;
            exitLabelRt.offsetMin = Vector2.zero;
            exitLabelRt.offsetMax = Vector2.zero;

            _fpvExitButton.onClick.AddListener(OnFPVExitClicked);

            // 初期状態は非表示
            _fpvOverlay.SetActive(false);
        }

        private void OnFirstPersonButtonClicked()
        {
            if (_selectedVisitor == null) return;

            var fpCam = Camera.main != null ? Camera.main.GetComponent<FirstPersonCamera>() : null;
            if (fpCam == null) return;

            fpCam.EnterFirstPerson(_selectedVisitor);

            // 来場者パネルを閉じる
            if (_visitorInfoPanel != null)
                _visitorInfoPanel.SetActive(false);
        }

        private void OnFPVExitClicked()
        {
            var fpCam = Camera.main != null ? Camera.main.GetComponent<FirstPersonCamera>() : null;
            if (fpCam != null)
                fpCam.ExitFirstPerson();
        }

        private void UpdateFirstPersonOverlay()
        {
            var fpCam = Camera.main != null ? Camera.main.GetComponent<FirstPersonCamera>() : null;
            bool fpvActive = fpCam != null && fpCam.IsActive;

            if (_fpvOverlay != null && _fpvOverlay.activeSelf != fpvActive)
                _fpvOverlay.SetActive(fpvActive);

            if (!fpvActive) return;

            // ステータス更新
            if (_fpvStatusText != null)
                _fpvStatusText.text = fpCam.GetStatusLabel();

            var visitor = fpCam.TargetVisitor;
            if (visitor != null)
            {
                if (_fpvHappinessText != null)
                {
                    float hp = visitor.Happiness;
                    _fpvHappinessText.text = $"幸福度: {hp:F0}%";
                    _fpvHappinessText.color = hp >= 70f ? new Color(0.5f, 1f, 0.5f)
                        : hp >= 40f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.4f, 0.3f);
                }

                if (_fpvPhaseText != null)
                {
                    var sm = visitor.StateMachine;
                    if (sm != null)
                    {
                        string phase = VisitorStateMachine.GetPhaseLabel(sm.CurrentPhase);
                        Color phaseCol = VisitorStateMachine.GetPhaseColor(sm.CurrentPhase);
                        _fpvPhaseText.text = $"[{phase}]";
                        _fpvPhaseText.color = phaseCol;
                    }
                }
            }
        }

        // ================================================================
        // Update
        // ================================================================

        private void Update()
        {
            if (GameManager.Instance == null) return;

            var state = GameManager.Instance.CurrentState;

            // メインメニュー時はHUD非表示
            if (state == GameState.MainMenu)
            {
                if (_canvas != null) _canvas.gameObject.SetActive(false);
                return;
            }
            if (_canvas != null && !_canvas.gameObject.activeSelf)
                _canvas.gameObject.SetActive(true);

            // オーバーレイ表示制御
            if (_pauseOverlay != null)
                _pauseOverlay.SetActive(state == GameState.Paused);
            if (_resultsOverlay != null)
                _resultsOverlay.SetActive(state == GameState.GameOver);

            // メニューボタンはPlaying中のみ
            if (_menuBtn != null)
                _menuBtn.SetActive(state == GameState.Playing);

            // GameOver表示
            if (state == GameState.GameOver)
            {
                RefreshResults();
                return;
            }

            if (state != GameState.Playing) return;

            HandleVisitorClick();

            _updateTimer -= Time.unscaledDeltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = UpdateInterval;

            RefreshAll();

            if (_selectedVisitor != null && _visitorInfoPanel.activeSelf)
            {
                RefreshVisitorInfo();
            }

            UpdateFloatingScores();
        }

        // ================================================================
        // 来場者クリック検出
        // ================================================================

        private void HandleVisitorClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                var visitor = hit.collider.GetComponent<VisitorAI>();
                if (visitor != null && visitor.IsActive)
                {
                    ShowVisitorInfo(visitor);
                    return;
                }
            }

            HideVisitorInfo();
        }

        private void ShowVisitorInfo(VisitorAI visitor)
        {
            _selectedVisitor = visitor;
            _visitorInfoPanel.SetActive(true);
            RefreshVisitorInfo();

            // チュートリアル連動: 来場者クリックイベント発火
            GameEvents.FireVisitorSelected(visitor.VisitorId);
        }

        private void HideVisitorInfo()
        {
            _selectedVisitor = null;
            _visitorInfoPanel.SetActive(false);
        }

        private void RefreshVisitorInfo()
        {
            if (_selectedVisitor == null || !_selectedVisitor.IsActive)
            {
                HideVisitorInfo();
                return;
            }

            var ai = _selectedVisitor;
            var p = ai.Parameters;
            var prof = ai.Profile;

            _viName.text = !string.IsNullOrEmpty(prof.VisitorName)
                ? prof.VisitorName
                : $"来場者 #{ai.VisitorId}";

            _viType.text = $"タイプ: {VisitorTypeLabel(ai.Type)}  年齢: {prof.Age}";
            _viState.text = $"状態: {BehaviorStateLabel(ai.CurrentState)}";

            // 満足度メーター
            float sat = p.Satisfaction;
            _viSatisfaction.text = $"満足度: {sat:F0}%  (総合: {p.OverallSatisfaction:F0}%)";
            Color satColor = sat >= 70f ? Green : sat >= 40f ? Yellow : Red;
            _viSatisfaction.color = satColor;

            if (_viSatBarFill != null && _viSatBar != null)
            {
                float ratio = Mathf.Clamp01(sat / 100f);
                var fillRt = _viSatBarFill.rectTransform;
                var parentRt = _viSatBar.GetComponent<RectTransform>();
                fillRt.sizeDelta = new Vector2(parentRt.sizeDelta.x * ratio, parentRt.sizeDelta.y);
                _viSatBarFill.color = satColor;
            }

            _viHappiness.text = $"幸福度: {p.Happiness:F0}%";
            _viHappiness.color = p.Happiness >= 70f ? Green :
                                 p.Happiness >= 40f ? Yellow : Red;

            _viExcitement.text = $"興奮度: {p.Excitement:F0}%";
            _viCash.text = $"所持金: ${p.Cash:F0}";

            _viHunger.text = $"空腹: {p.Hunger:F0}%";
            _viHunger.color = p.Hunger >= 60f ? Yellow : Muted;

            _viThirst.text = $"渇き: {p.Thirst:F0}%";
            _viThirst.color = p.Thirst >= 60f ? Yellow : Muted;

            _viNausea.text = $"吐き気: {p.Nausea:F0}%";
            _viNausea.color = p.Nausea >= 60f ? Red : Muted;

            _viToilet.text = $"トイレ: {p.ToiletNeed:F0}%";
            _viToilet.color = p.ToiletNeed >= 70f ? Red : Muted;

            _viRides.text = $"搭乗回数: {prof.RidesExperienced}回";
            if (prof.FavoriteAttraction.HasValue)
            {
                _viRides.text += $"  Best: {prof.FavoriteAttraction.Value.AttractionName}";
            }

            // ライフサイクルFSM情報
            var sm = ai.StateMachine;
            if (sm != null)
            {
                string phaseLabel = VisitorStateMachine.GetPhaseLabel(sm.CurrentPhase);
                Color phaseColor = VisitorStateMachine.GetPhaseColor(sm.CurrentPhase);
                _viLifecycle.text = $"フェーズ: {phaseLabel}  ({sm.CurrentPhaseElapsed:F0}s)";
                _viLifecycle.color = phaseColor;
                _viLifecycleStats.text = sm.GetStatsSummary();
            }
            else
            {
                _viLifecycle.text = "";
                _viLifecycleStats.text = "";
            }
        }

        // ================================================================
        // 満足度フローティングスコア（搭乗後に頭上に表示）
        // ================================================================

        private void OnVisitorSatisfactionChanged(int visitorId, float newScore, float delta)
        {
            if (Mathf.Abs(delta) < 0.5f) return;
            if (_canvas == null || Camera.main == null) return;

            // 来場者のワールド位置を取得
            var gm = GameManager.Instance;
            if (gm == null || gm.VisitorManager == null) return;

            var visitors = gm.VisitorManager.GetAllActiveVisitors();
            if (visitors == null) return;

            Vector3 worldPos = Vector3.zero;
            bool found = false;
            for (int i = 0; i < visitors.Count; i++)
            {
                if (visitors[i].VisitorId == visitorId)
                {
                    worldPos = visitors[i].transform.position + Vector3.up * 2.8f;
                    found = true;
                    break;
                }
            }
            if (!found) return;

            // フローティングテキスト生成
            var go = new GameObject("FloatScore");
            go.transform.SetParent(_canvasRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 30f);

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            if (delta > 0f)
            {
                label.text = $"+{delta:F0}";
                label.color = Green;
            }
            else
            {
                label.text = $"{delta:F0}";
                label.color = Red;
            }

            // 画面位置に変換
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            rt.position = screenPos;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);

            _floatingScores.Add(new FloatingScore
            {
                Go = go,
                Label = label,
                VisitorId = visitorId,
                Timer = FloatingScoreDuration,
                WorldPos = worldPos
            });
        }

        // ================================================================
        // 通路混雑度表示
        // ================================================================

        private PathwaySystem _cachedPathwaySystem;
        private float _pathwayCacheTimer;

        private void UpdateCongestionDisplay()
        {
            if (_congestionText == null) return;

            // PathwaySystemのキャッシュ（毎フレーム FindObjectOfType は重いので3秒間隔）
            _pathwayCacheTimer -= Time.deltaTime;
            if (_pathwayCacheTimer <= 0f || _cachedPathwaySystem == null)
            {
                _cachedPathwaySystem = FindObjectOfType<PathwaySystem>();
                _pathwayCacheTimer = 3f;
            }

            if (_cachedPathwaySystem == null)
            {
                _congestionText.text = "通路: ---";
                return;
            }

            float avg = _cachedPathwaySystem.AverageCongestion;
            string label = PathwaySystem.GetCongestionLabel(avg);
            Color displayColor = PathwaySystem.GetCongestionDisplayColor(avg);

            _congestionText.text = $"通路: {label}";
            _congestionText.color = displayColor;

            // バーの更新
            if (_congestionBarFill != null)
            {
                var fillRt = _congestionBarFill.rectTransform;
                fillRt.sizeDelta = new Vector2(60f * avg, 0f);
                _congestionBarFill.color = displayColor;
            }
        }

        private void UpdateFloatingScores()
        {
            Camera cam = Camera.main;

            for (int i = _floatingScores.Count - 1; i >= 0; i--)
            {
                var fs = _floatingScores[i];
                fs.Timer -= Time.unscaledDeltaTime;
                fs.WorldPos += Vector3.up * Time.unscaledDeltaTime * 1.5f;

                _floatingScores[i] = fs;

                if (fs.Timer <= 0f || fs.Go == null)
                {
                    if (fs.Go != null) Destroy(fs.Go);
                    _floatingScores.RemoveAt(i);
                    continue;
                }

                // 画面位置更新
                if (cam != null)
                {
                    Vector3 sp = cam.WorldToScreenPoint(fs.WorldPos);
                    if (sp.z > 0f)
                    {
                        fs.Go.SetActive(true);
                        fs.Go.GetComponent<RectTransform>().position = sp;
                    }
                    else
                    {
                        fs.Go.SetActive(false);
                    }
                }

                // フェードアウト
                float alpha = Mathf.Clamp01(fs.Timer / (FloatingScoreDuration * 0.4f));
                if (fs.Label != null)
                {
                    var c = fs.Label.color;
                    c.a = alpha;
                    fs.Label.color = c;
                }
            }
        }

        // ================================================================
        // HUD定期更新
        // ================================================================

        private void RefreshAll()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // ---- スコアボード ----
            if (gm.VisitorManager != null)
            {
                int active = gm.VisitorManager.ActiveVisitorCount;
                int today = gm.VisitorManager.TotalVisitorsToday;
                float avgHappy = gm.VisitorManager.AverageHappiness;

                _sbVisitorValue.text = $"{active}";

                float avgSat = gm.VisitorManager.AverageSatisfaction;
                _sbSatisfactionValue.text = $"{avgSat:F0}%";
                Color satColor = avgSat >= 70f ? Green : avgSat >= 40f ? Yellow : Red;
                _sbSatisfactionValue.color = satColor;

                // 満足度バー
                if (_sbSatisfactionFill != null)
                {
                    float ratio = Mathf.Clamp01(avgSat / 100f);
                    var barRt = _sbSatisfactionFill.rectTransform;
                    // バーの幅をratioで調整（親の幅 × ratio）
                    var parentRt = _sbSatisfactionBar.GetComponent<RectTransform>();
                    barRt.sizeDelta = new Vector2(parentRt.sizeDelta.x * ratio, parentRt.sizeDelta.y);
                    _sbSatisfactionFill.color = satColor;
                }

                RefreshVisitorStates(gm.VisitorManager);
            }

            if (gm.EconomyManager != null)
            {
                _sbRevenueValue.text = $"${gm.EconomyManager.TotalRevenueEarned:N0}";
                _moneyText.text = $"資金: ${gm.EconomyManager.CurrentMoney:N0}";
            }

            // ---- 時間 + 天候 ----
            string time = "---";
            if (gm.TimeManager != null)
            {
                var tm = gm.TimeManager;
                int h = (int)tm.CurrentHour;
                int m = (int)((tm.CurrentHour - h) * 60);
                time = $"Y{tm.CurrentYear} M{tm.CurrentMonth} D{tm.CurrentDay} {h:D2}:{m:D2}";
            }
            string weather = gm.WeatherSystem != null ? WeatherLabel(gm.WeatherSystem.CurrentWeather) : "";
            _timeWeatherText.text = $"{weather}  {time}";

            // ---- スタッフ ----
            if (gm.StaffManager != null)
                _staffText.text = $"Staff: {gm.StaffManager.TotalStaffCount}名";

            // ---- 通路混雑度 ----
            UpdateCongestionDisplay();

            // ---- イベント表示 ----
            UpdateEventPanel();

            // ---- ライフサイクルフェーズ ----
            UpdateLifecyclePanel();

            // ---- パーク評価 ----
            UpdateRatingPanel();

            // ---- ファーストパーソンビュー ----
            UpdateFirstPersonOverlay();

            // FPV中はメインHUD要素を非表示
            bool fpvActive = false;
            {
                var fpCam = Camera.main != null ? Camera.main.GetComponent<FirstPersonCamera>() : null;
                fpvActive = fpCam != null && fpCam.IsActive;
            }

            // ---- 速度 ----
            _currentSpeed = gm.SpeedLevel;
            HighlightActiveSpeed();

            // ---- アトラクション ----
            RefreshAttractions();

            // ---- シナリオ目標 ----
            RefreshScenarioPanel();

            // ---- 通知バッジ ----
            if (_notifBadge != null && NotificationSystem.Instance != null)
            {
                int unread = NotificationSystem.Instance.UnreadCount;
                _notifBadge.SetActive(unread > 0);
                if (_notifBadgeText != null && unread > 0)
                    _notifBadgeText.text = unread > 99 ? "99+" : unread.ToString();
            }
        }

        private void RefreshVisitorStates(VisitorManager vm)
        {
            var counts = vm.GetVisitorCountByState();
            _walkingCount = 0; _waitingCount = 0; _ridingCount = 0;
            _shoppingCount = 0; _idleCount = 0; _leavingCount = 0;

            foreach (var kv in counts)
            {
                switch (kv.Key)
                {
                    case VisitorBehaviorState.WalkingToAttraction:
                        _walkingCount += kv.Value; break;
                    case VisitorBehaviorState.WaitingInQueue:
                        _waitingCount += kv.Value; break;
                    case VisitorBehaviorState.RidingAttraction:
                        _ridingCount += kv.Value; break;
                    case VisitorBehaviorState.Eating:
                    case VisitorBehaviorState.Drinking:
                    case VisitorBehaviorState.WalkingToShop:
                        _shoppingCount += kv.Value; break;
                    case VisitorBehaviorState.LeavingPark:
                        _leavingCount += kv.Value; break;
                    default:
                        _idleCount += kv.Value; break;
                }
            }

            int[] vals = { _walkingCount, _waitingCount, _ridingCount, _shoppingCount, _idleCount, _leavingCount };
            for (int i = 0; i < 6; i++)
            {
                if (_visitorStatTexts[i] != null)
                    _visitorStatTexts[i].text = $"{StatLabels[i]}: {vals[i]}";
            }
        }

        private void RefreshAttractions()
        {
            _attrCacheTimer -= UpdateInterval;
            if (_attrCacheTimer <= 0f)
            {
                _attrCacheTimer = 2f;
                _attractions = FindObjectsOfType<Attraction.Attraction>();
            }

            if (_attractions == null || _attractions.Length == 0)
            {
                if (_attrPanelRt != null) _attrPanelRt.gameObject.SetActive(false);
                return;
            }
            _attrPanelRt.gameObject.SetActive(true);

            int linesPerAttr = 3;
            int needed = _attractions.Length * linesPerAttr;
            while (_attrLines.Count < needed)
            {
                int idx = _attrLines.Count;
                int lineType = idx % linesPerAttr;
                bool isName = (lineType == 0);
                bool isRevenue = (lineType == 2);
                var t = MakeLabel(_attrPanelRt, $"AL{idx}", "",
                    isName ? 14 : 12,
                    isName ? Color.white : (isRevenue ? Gold : Muted),
                    isName ? FontStyle.Bold : FontStyle.Normal,
                    TextAnchor.MiddleLeft);
                _attrLines.Add(t);
            }

            float lineH = 18f;
            float blockH = lineH * linesPerAttr + 6f;
            float panelH = 34f + _attractions.Length * blockH;
            _attrPanelRt.sizeDelta = new Vector2(320f, panelH);

            for (int i = 0; i < _attractions.Length; i++)
            {
                var attr = _attractions[i];
                int nameIdx = i * linesPerAttr;
                int detailIdx = nameIdx + 1;
                int revenueIdx = nameIdx + 2;

                float baseY = panelH - 32f - i * blockH;

                PlaceInParent(_attrLines[nameIdx].rectTransform, 10f, baseY, 300f, lineH, new Vector2(0f, 1f));
                PlaceInParent(_attrLines[detailIdx].rectTransform, 10f, baseY - lineH, 300f, lineH, new Vector2(0f, 1f));
                PlaceInParent(_attrLines[revenueIdx].rectTransform, 10f, baseY - lineH * 2, 300f, lineH, new Vector2(0f, 1f));

                if (attr != null)
                {
                    _attrLines[nameIdx].text = attr.DisplayName;

                    string st = CycleLabel(attr.CurrentCycleState);
                    _attrLines[detailIdx].text = $"  {st}  Q:{attr.QueueLength}/{attr.MaxQueueLength}  乗車:{attr.TotalRiderCount}";
                    _attrLines[detailIdx].color = CycleColor(attr.CurrentCycleState);

                    _attrLines[revenueIdx].text = $"  Ticket:${attr.TicketPrice}  今日:${attr.TodayRevenue:N0}  累計:${attr.TotalRevenue:N0}";
                    _attrLines[revenueIdx].color = attr.TotalRevenue > 0 ? Gold : Muted;
                }
            }
        }

        // ================================================================
        // シナリオ目標の更新
        // ================================================================

        private void RefreshScenarioPanel()
        {
            if (_scenarioPanel == null) return;

            bool active = ScenarioManager.Instance != null && ScenarioManager.Instance.IsScenarioActive;
            _scenarioPanel.SetActive(active);

            if (!active) return;

            var sm = ScenarioManager.Instance;
            var scenario = sm.ActiveScenario;

            _scenarioTitle.text = $"SCENARIO: {ScenarioManager.GetCountryName(scenario.Country)}";

            string objectives = sm.GetObjectiveProgressText();
            if (sm.IsScenarioCleared)
                objectives += "\n*** SCENARIO CLEARED! ***";
            else if (sm.IsScenarioFailed)
                objectives += "\n*** TIME OVER ***";

            if (scenario.TimeLimitYears > 0 && GameManager.Instance.TimeManager != null)
            {
                int yr = GameManager.Instance.TimeManager.CurrentYear;
                objectives += $"\n制限: Year {yr}/{scenario.TimeLimitYears}";
            }

            _scenarioObjectives.text = objectives;
        }

        // ================================================================
        // 速度ボタン
        // ================================================================

        private void OnSpeedClicked(int index)
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.SpeedLevel = _speedLevels[index];
            _currentSpeed = _speedLevels[index];
            HighlightActiveSpeed();
        }

        private void HighlightActiveSpeed()
        {
            for (int i = 0; i < 4; i++)
            {
                if (_speedBtnBgs[i] != null)
                    _speedBtnBgs[i].color = (_speedLevels[i] == _currentSpeed) ? BtnActive : BtnNormal;
            }
        }

        // ================================================================
        // UI構築ヘルパー
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

        private static void PlaceInParent(RectTransform rt, float x, float y, float w, float h, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ================================================================
        // 表示ラベル
        // ================================================================

        private static string WeatherLabel(Weather w)
        {
            switch (w)
            {
                case Weather.Sunny:  return "[Sunny]";
                case Weather.Cloudy: return "[Cloudy]";
                case Weather.Rainy:  return "[Rainy]";
                case Weather.Snowy:  return "[Snowy]";
                case Weather.Hot:    return "[Hot]";
                default: return "";
            }
        }

        private static string CycleLabel(RideCycleState s)
        {
            switch (s)
            {
                case RideCycleState.WaitingForRiders: return "[Waiting]";
                case RideCycleState.Loading:          return "[Loading]";
                case RideCycleState.Running:          return "[Running]";
                case RideCycleState.Unloading:        return "[Unloading]";
                case RideCycleState.BrokenDown:       return "[BROKEN]";
                case RideCycleState.Accident:         return "[ACCIDENT]";
                default: return "[---]";
            }
        }

        private static Color CycleColor(RideCycleState s)
        {
            switch (s)
            {
                case RideCycleState.Running:                return Green;
                case RideCycleState.Loading:
                case RideCycleState.Unloading:              return Yellow;
                case RideCycleState.BrokenDown:
                case RideCycleState.Accident:               return Red;
                default:                                    return Muted;
            }
        }

        private static string VisitorTypeLabel(VisitorType t)
        {
            switch (t)
            {
                case VisitorType.Kids:   return "キッズ";
                case VisitorType.Young:  return "ヤング";
                case VisitorType.Family: return "ファミリー";
                case VisitorType.Couple: return "カップル";
                case VisitorType.Senior: return "シニア";
                case VisitorType.VIP:    return "VIP";
                default: return t.ToString();
            }
        }

        private static string BehaviorStateLabel(VisitorBehaviorState s)
        {
            switch (s)
            {
                case VisitorBehaviorState.Idle:                  return "散策中";
                case VisitorBehaviorState.WalkingToAttraction:   return "アトラクションへ移動中";
                case VisitorBehaviorState.WaitingInQueue:        return "行列待ち";
                case VisitorBehaviorState.RidingAttraction:      return "搭乗中";
                case VisitorBehaviorState.WalkingToShop:         return "ショップへ移動中";
                case VisitorBehaviorState.Eating:                return "食事中";
                case VisitorBehaviorState.Drinking:              return "飲み物中";
                case VisitorBehaviorState.WalkingToToilet:       return "トイレへ移動中";
                case VisitorBehaviorState.UsingToilet:           return "トイレ使用中";
                case VisitorBehaviorState.Resting:               return "休憩中";
                case VisitorBehaviorState.WatchingEntertainment: return "ショー鑑賞中";
                case VisitorBehaviorState.LookingAtMap:          return "マップ確認中";
                case VisitorBehaviorState.Vomiting:              return "嘔吐中";
                case VisitorBehaviorState.LeavingPark:           return "退園中";
                case VisitorBehaviorState.TalkingToPlayer:       return "会話中";
                default: return s.ToString();
            }
        }
    }
}
