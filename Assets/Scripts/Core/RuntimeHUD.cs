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
        private VisitorAI _selectedVisitor;

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
            PlaceInParent(_moneyText.rectTransform, 12f, barH, barW * 0.3f, barH, new Vector2(0f, 1f));

            _timeWeatherText = MakeLabel(rt, "TimeWeather", "", 14, Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceInParent(_timeWeatherText.rectTransform, barW * 0.3f, barH, barW * 0.4f, barH, new Vector2(0f, 1f));

            _staffText = MakeLabel(rt, "Staff", "", 14, Muted, FontStyle.Normal, TextAnchor.MiddleRight);
            PlaceInParent(_staffText.rectTransform, barW * 0.7f, barH, barW * 0.28f, barH, new Vector2(0f, 1f));
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
            float panelH = 340f;

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
            _viHappiness  = MakeInfoLine(rt, "Happy",   ref y, lineH, lx, lw, Green, FontStyle.Bold, 14);
            _viExcitement = MakeInfoLine(rt, "Excite",  ref y, lineH, lx, lw, Gold, FontStyle.Normal, 14);
            _viCash       = MakeInfoLine(rt, "Cash",    ref y, lineH, lx, lw, Gold, FontStyle.Normal, 14);
            y -= 6f;
            _viHunger  = MakeInfoLine(rt, "Hunger",  ref y, lineH, lx, lw, Muted, FontStyle.Normal, 13);
            _viThirst  = MakeInfoLine(rt, "Thirst",  ref y, lineH, lx, lw, Muted, FontStyle.Normal, 13);
            _viNausea  = MakeInfoLine(rt, "Nausea",  ref y, lineH, lx, lw, Muted, FontStyle.Normal, 13);
            _viToilet  = MakeInfoLine(rt, "Toilet",  ref y, lineH, lx, lw, Muted, FontStyle.Normal, 13);
            y -= 6f;
            _viRides   = MakeInfoLine(rt, "Rides",   ref y, lineH, lx, lw, Cyan, FontStyle.Normal, 13);

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
                new Vector2(0f, 50f), OnResumeClicked);

            // 「セーブ/ロード」ボタン
            MakeCenterButton(rt, "SaveLoadBtn", "SAVE / LOAD",
                new Color(0.3f, 0.4f, 0.6f), new Color(0.38f, 0.5f, 0.72f), new Color(0.22f, 0.3f, 0.48f),
                new Vector2(0f, -30f), OnSaveLoadClicked);

            // 「ゲーム終了」ボタン
            MakeCenterButton(rt, "EndGameBtn", "ゲーム終了",
                new Color(0.65f, 0.2f, 0.2f), new Color(0.75f, 0.3f, 0.3f), new Color(0.5f, 0.15f, 0.15f),
                new Vector2(0f, -110f), OnEndGameClicked);

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
            float avgHappy = gm.VisitorManager != null ? gm.VisitorManager.AverageHappiness : 0f;
            string tickets = $"{gm.GoldenTickets}";

            string time = "---";
            if (gm.TimeManager != null)
            {
                var tm = gm.TimeManager;
                time = $"Y{tm.CurrentYear} M{tm.CurrentMonth} D{tm.CurrentDay}";
            }

            int attrCount = (_attractions != null) ? _attractions.Length : 0;

            _resultsBody.text =
                $"  Visitors: {visitors}  (Peak: {peak})         Satisfaction: {avgHappy:F0}%\n" +
                $"  Revenue: {revenue}         Expenses: {expenses}\n" +
                $"  Final Balance: {money}\n" +
                $"  Attractions: {attrCount}         Golden Tickets: {tickets}\n" +
                $"  Date: {time}\n" +
                $"\n" +
                $"  Score Breakdown:\n" +
                $"    Visitors x10 = {(gm.VisitorManager != null ? gm.VisitorManager.TotalVisitorsToday * 10 : 0):N0}\n" +
                $"    Revenue / 100 = {(gm.EconomyManager != null ? (int)(gm.EconomyManager.TotalRevenueEarned / 100f) : 0):N0}\n" +
                $"    Satisfaction x50 = {(int)(avgHappy * 50f):N0}\n" +
                $"    Golden Tickets x500 = {gm.GoldenTickets * 500:N0}";
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

            _viHappiness.text = $"満足度: {p.Happiness:F0}%";
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

                _sbSatisfactionValue.text = $"{avgHappy:F0}%";
                Color satColor = avgHappy >= 70f ? Green : avgHappy >= 40f ? Yellow : Red;
                _sbSatisfactionValue.color = satColor;

                // 満足度バー
                if (_sbSatisfactionFill != null)
                {
                    float ratio = Mathf.Clamp01(avgHappy / 100f);
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

            // ---- 速度 ----
            _currentSpeed = gm.SpeedLevel;
            HighlightActiveSpeed();

            // ---- アトラクション ----
            RefreshAttractions();

            // ---- シナリオ目標 ----
            RefreshScenarioPanel();
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
