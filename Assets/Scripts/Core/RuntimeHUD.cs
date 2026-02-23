// ============================================================
// ThemeParkGame - RuntimeHUD
// uGUI (Canvas + Text) ベースのランタイムHUDオーバーレイ
// プレハブ/シーン配置なしでコードからCanvasを構築し
// 入場者数・収益・満足度・速度ボタンをリアルタイム表示する
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Attraction;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Core
{
    /// <summary>
    /// uGUI Canvas をコードで構築するランタイムHUD。
    /// 画面上部に入場者数・収益・平均満足度を常時リアルタイム表示。
    /// 速度制御ボタン、来場者状態パネル、アトラクション稼働状況パネルを含む。
    /// </summary>
    public class RuntimeHUD : MonoBehaviour
    {
        // ---- Canvas ----
        private Canvas _canvas;

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

            var root = cGo.GetComponent<RectTransform>();

            BuildTopBar(root);
            BuildSpeedPanel(root);
            BuildVisitorPanel(root);
            BuildAttractionPanel(root);
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
        // アトラクションパネル（右下 300xN）
        // ================================================================

        private void BuildAttractionPanel(RectTransform root)
        {
            float panelW = 300f;
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
        // Update
        // ================================================================

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            _updateTimer -= Time.unscaledDeltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = UpdateInterval;

            RefreshAll();
        }

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

            int needed = _attractions.Length * 2;
            while (_attrLines.Count < needed)
            {
                int idx = _attrLines.Count;
                bool isName = (idx % 2 == 0);
                var t = MakeLabel(_attrPanelRt, $"AL{idx}", "", isName ? 14 : 13,
                    isName ? Color.white : Muted,
                    isName ? FontStyle.Bold : FontStyle.Normal,
                    TextAnchor.MiddleLeft);
                _attrLines.Add(t);
            }

            // パネルサイズ更新
            float panelH = 34f + _attractions.Length * 40f;
            _attrPanelRt.sizeDelta = new Vector2(300f, panelH);

            // ライン位置更新 + テキスト
            for (int i = 0; i < _attractions.Length; i++)
            {
                var attr = _attractions[i];
                int ni = i * 2;
                int di = i * 2 + 1;

                float nameY = panelH - 32f - i * 40f;
                float detailY = nameY - 20f;

                PlaceInParent(_attrLines[ni].rectTransform, 10f, nameY, 280f, 20f, new Vector2(0f, 1f));
                PlaceInParent(_attrLines[di].rectTransform, 10f, detailY, 280f, 18f, new Vector2(0f, 1f));

                if (attr != null)
                {
                    _attrLines[ni].text = attr.DisplayName;
                    string st = CycleLabel(attr.CurrentCycleState);
                    _attrLines[di].text = $"  {st}  Q:{attr.QueueLength}/{attr.MaxQueueLength}  乗車:{attr.TotalRiderCount}";
                    _attrLines[di].color = CycleColor(attr.CurrentCycleState);
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
    }
}
