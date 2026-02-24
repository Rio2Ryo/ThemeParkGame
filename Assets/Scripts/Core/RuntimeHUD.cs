// ============================================================
// ThemeParkGame - RuntimeHUD
// uGUI (Canvas + Text) ベースのランタイムHUDオーバーレイ
// プレハブ/シーン配置なしでコードからCanvasを構築し
// 入場者数・収益・満足度・速度ボタンをリアルタイム表示する
// 来場者クリックで個別情報パネルを表示
// アトラクションごとの収益集計をUIに反映
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ThemeParkGame.Attraction;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// uGUI Canvas をコードで構築するランタイムHUD。
    /// 画面上部に入場者数・収益・平均満足度を常時リアルタイム表示。
    /// 速度制御ボタン、来場者状態パネル、アトラクション稼働状況パネルを含む。
    /// 来場者をクリックすると個別情報パネルを表示する。
    /// </summary>
    public class RuntimeHUD : MonoBehaviour
    {
        // ---- Canvas ----
        private Canvas _canvas;
        private RectTransform _canvasRoot;

        // ---- トップバー ----
        private Text _moneyText;
        private Text _visitorText;
        private Text _satisfactionText;
        private Text _totalRevenueText;
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
            // Canvas
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

            BuildTopBar(_canvasRoot);
            BuildSpeedPanel(_canvasRoot);
            BuildVisitorPanel(_canvasRoot);
            BuildAttractionPanel(_canvasRoot);
            BuildVisitorInfoPanel(_canvasRoot);
        }

        // ================================================================
        // トップバー（画面上部中央 720x90）
        // ================================================================

        private void BuildTopBar(RectTransform root)
        {
            float barW = 720f;
            float barH = 90f;

            var bg = MakePanel(root, "TopBar", barW, barH, BgDark);
            var rt = bg.GetComponent<RectTransform>();
            // 上端中央
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -4f);

            // 行1: 時間+天候 (上から 4px, 中央)
            _timeWeatherText = MakeLabel(rt, "TimeWeather", "", 18, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_timeWeatherText.rectTransform, 0f, barH - 4f, barW, 24f, new Vector2(0f, 1f));

            // 行2: 資金 | 入場者 | 満足度 (3列)
            float row2Y = barH - 32f;
            float colW = barW / 3f;

            _moneyText = MakeLabel(rt, "Money", "", 17, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(_moneyText.rectTransform, 12f, row2Y, colW - 12f, 24f, new Vector2(0f, 1f));

            _visitorText = MakeLabel(rt, "Visitor", "", 17, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(_visitorText.rectTransform, colW, row2Y, colW, 24f, new Vector2(0f, 1f));

            _satisfactionText = MakeLabel(rt, "Satisfaction", "", 17, Color.white, FontStyle.Bold, TextAnchor.MiddleRight);
            PlaceInParent(_satisfactionText.rectTransform, colW * 2f, row2Y, colW - 12f, 24f, new Vector2(0f, 1f));

            // 行3: 総収益 | スタッフ
            float row3Y = barH - 60f;
            _totalRevenueText = MakeLabel(rt, "TotalRevenue", "", 14, Green, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_totalRevenueText.rectTransform, 12f, row3Y, barW * 0.5f, 20f, new Vector2(0f, 1f));

            _staffText = MakeLabel(rt, "Staff", "", 14, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(_staffText.rectTransform, barW * 0.55f, row3Y, barW * 0.25f, 20f, new Vector2(0f, 1f));
        }

        // ================================================================
        // 速度ボタンパネル（右上、トップバー下）
        // ================================================================

        private void BuildSpeedPanel(RectTransform root)
        {
            float panelW = 230f;
            float panelH = 44f;

            var bg = MakePanel(root, "SpeedPanel", panelW, panelH, BgDark);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -100f);

            _speedBtnBgs = new Image[4];
            float btnW = 50f;
            float gap = 4f;
            float startX = 8f;

            for (int i = 0; i < 4; i++)
            {
                float bx = startX + i * (btnW + gap);

                // ボタン背景
                var btnBg = MakePanel(rt, $"SpeedBtn{i}", btnW, 34f, BtnNormal);
                var btnRt = btnBg.GetComponent<RectTransform>();
                btnRt.anchorMin = btnRt.anchorMax = new Vector2(0f, 0.5f);
                btnRt.pivot = new Vector2(0f, 0.5f);
                btnRt.anchoredPosition = new Vector2(bx, 0f);

                // ボタンコンポーネント（raycastTargetを有効にしてクリック受付）
                var btnImg = btnBg.GetComponent<Image>();
                btnImg.raycastTarget = true;
                var btn = btnBg.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.35f, 0.4f, 0.5f);
                colors.pressedColor = BtnActive;
                btn.colors = colors;
                btn.targetGraphic = btnImg;

                // ラベル
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

            // ヘッダー
            var header = MakeLabel(rt, "Header", "来場者状況", 16, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(header.rectTransform, 0f, panelH - 4f, panelW, 24f, new Vector2(0f, 1f));

            _visitorStatTexts = new Text[6];
            float lineH = 24f;
            float startY = panelH - 34f;

            for (int i = 0; i < 6; i++)
            {
                float y = startY - i * lineH;

                // カラードット
                var dot = MakePanel(rt, $"Dot{i}", 12f, 12f, StatDotColors[i]);
                var dotRt = dot.GetComponent<RectTransform>();
                dotRt.anchorMin = dotRt.anchorMax = new Vector2(0f, 0f);
                dotRt.pivot = new Vector2(0f, 0.5f);
                dotRt.anchoredPosition = new Vector2(10f, y - lineH * 0.5f);

                // テキスト
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
            float panelH = 40f; // 初期値、後で動的に調整

            var bg = MakePanel(root, "AttractionPanel", panelW, panelH, BgDark);
            _attrPanelRt = bg.GetComponent<RectTransform>();
            _attrPanelRt.anchorMin = _attrPanelRt.anchorMax = new Vector2(1f, 0f);
            _attrPanelRt.pivot = new Vector2(1f, 0f);
            _attrPanelRt.anchoredPosition = new Vector2(-10f, 10f);

            var header = MakeLabel(_attrPanelRt, "Header", "Attraction Status", 16, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(header.rectTransform, 0f, panelH - 4f, panelW, 24f, new Vector2(0f, 1f));
        }

        // ================================================================
        // 来場者個別情報パネル（画面中央左 280x320）
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

            // ヘッダー
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

            // 情報ラベル群
            float lineH = 22f;
            float y = panelH - 34f;
            float lx = 12f;
            float lw = panelW - 24f;

            _viName    = MakeInfoLine(rt, "Name",    ref y, lineH, lx, lw, Color.white, FontStyle.Bold, 16);
            _viType    = MakeInfoLine(rt, "Type",    ref y, lineH, lx, lw, Cyan, FontStyle.Normal, 14);
            _viState   = MakeInfoLine(rt, "State",   ref y, lineH, lx, lw, Muted, FontStyle.Normal, 14);

            y -= 6f; // セパレータ

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
        // Update
        // ================================================================

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            // クリック検出（来場者選択）
            HandleVisitorClick();

            // 定期UI更新
            _updateTimer -= Time.unscaledDeltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = UpdateInterval;

            RefreshAll();

            // 選択中の来場者パネル更新
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

            // UI上のクリックは無視
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

            // 何もヒットしなかった場合はパネルを閉じる
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

            // 幸福度（色分け）
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

            // ---- 資金 ----
            if (gm.EconomyManager != null)
            {
                _moneyText.text = $"資金: ${gm.EconomyManager.CurrentMoney:N0}";
                _totalRevenueText.text = $"総収益: ${gm.EconomyManager.TotalRevenueEarned:N0}";
            }

            // ---- 来場者 ----
            if (gm.VisitorManager != null)
            {
                int active = gm.VisitorManager.ActiveVisitorCount;
                int today = gm.VisitorManager.TotalVisitorsToday;
                float avgHappy = gm.VisitorManager.AverageHappiness;

                _visitorText.text = $"入場者: {active}人 (今日{today})";

                _satisfactionText.text = $"満足度: {avgHappy:F0}%";
                _satisfactionText.color = avgHappy >= 70f ? Green :
                                          avgHappy >= 40f ? Yellow : Red;

                RefreshVisitorStates(gm.VisitorManager);
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

            // 各アトラクション: 名前行 + 状態行 + 収益行 = 3行
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

            // パネルサイズ更新（3行 × 各lineH + ヘッダー + マージン）
            float lineH = 18f;
            float blockH = lineH * linesPerAttr + 6f; // 3行 + 余白
            float panelH = 34f + _attractions.Length * blockH;
            _attrPanelRt.sizeDelta = new Vector2(320f, panelH);

            // ライン位置更新 + テキスト
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

                    // 収益行: チケット価格 × 乗車回数 → 総収益
                    _attrLines[revenueIdx].text = $"  Ticket:${attr.TicketPrice}  今日:${attr.TodayRevenue:N0}  累計:${attr.TotalRevenue:N0}";
                    _attrLines[revenueIdx].color = attr.TotalRevenue > 0 ? Gold : Muted;
                }
            }
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

        /// <summary>背景Imageつきパネルを作成する</summary>
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

        /// <summary>Textコンポーネントを作成する</summary>
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

        /// <summary>
        /// 親パネル内でのローカル位置（左下原点）とサイズを設定する。
        /// pivot指定で配置の基準を制御。
        /// </summary>
        private static void PlaceInParent(RectTransform rt, float x, float y, float w, float h, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero; // 左下基準
            rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>親いっぱいに広げる</summary>
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
