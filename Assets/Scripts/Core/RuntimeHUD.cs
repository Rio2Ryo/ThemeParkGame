// ============================================================
// ThemeParkGame - RuntimeHUD (Core)
// uGUI (Canvas + Text) ベースのランタイムHUDオーバーレイ
// プレハブ/シーン配置なしでコードからCanvasを構築し
// ①スコアボード: 入場者数・総収益・満足度をリアルタイム表示
// ②来場者クリック個別情報パネル
// ③アトラクション収益パネル
// ④メニューボタン・ポーズ画面
// ⑤ゲームオーバー画面（最終スコア・リスタート）
//
// partial class で機能別に分割:
//   RuntimeHUD.cs            - Core (Canvas基盤, ライフサイクル, 共有ヘルパー)
//   RuntimeHUD_Scoreboard.cs - スコアボード, 情報バー, 速度パネル
//   RuntimeHUD_Visitors.cs   - 来場者状態, 来場者情報, FPV
//   RuntimeHUD_Attractions.cs- アトラクション, 施設詳細
//   RuntimeHUD_Economy.cs    - 研究, ローン, 月次レポート
//   RuntimeHUD_Menu.cs       - メニュー, ポーズ, 結果, セーブ/ロード
//   RuntimeHUD_ParkInfo.cs   - 評価, SNS, アラート, イベント, ゾーン
//   RuntimeHUD_CoasterDesign.cs - カスタムコースター設計パネル
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ThemeParkGame.AI;
using ThemeParkGame.Attraction;
using ThemeParkGame.Economy;
using ThemeParkGame.Park;
using ThemeParkGame.UI;
using ThemeParkGame.Staff;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Core
{
    public partial class RuntimeHUD : MonoBehaviour
    {
        // ---- Canvas ----
        private Canvas _canvas;
        private RectTransform _canvasRoot;

        // ---- データキャッシュ ----
        private float _updateTimer;
        private const float UpdateInterval = 0.3f;
        private Attraction.Attraction[] _attractions;
        private float _attrCacheTimer;
        private float _currentSpeed = 1f;

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

        // ================================================================
        // 初期化
        // ================================================================

        private void Awake()
        {
            BuildCanvas();
            GameEvents.OnVisitorSatisfactionChanged += OnVisitorSatisfactionChanged;
            GameEvents.OnGoldenTicketEarned += OnGoldenTicketCelebration;
            GameEvents.OnCertificateAwarded += OnCertificateCelebration;

            // ツールチップシステムの初期化
            if (ThemeParkGame.UI.TooltipSystem.Instance == null)
                gameObject.AddComponent<ThemeParkGame.UI.TooltipSystem>();

            // モバイルUI最適化の初期化
            if (ThemeParkGame.UI.MobileUIOptimizer.Instance == null)
                gameObject.AddComponent<ThemeParkGame.UI.MobileUIOptimizer>();
        }

        private void Start()
        {
            // TimeManagerの月変更・年変更イベントを購読してレポートを表示
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnMonthChanged += ShowMonthlyReport;
                GameManager.Instance.TimeManager.OnYearChanged += ShowAnnualReport;
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnVisitorSatisfactionChanged -= OnVisitorSatisfactionChanged;
            GameEvents.OnGoldenTicketEarned -= OnGoldenTicketCelebration;
            GameEvents.OnCertificateAwarded -= OnCertificateCelebration;
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnMonthChanged -= ShowMonthlyReport;
                GameManager.Instance.TimeManager.OnYearChanged -= ShowAnnualReport;
            }
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
            BuildFacilityInfoPanel(_canvasRoot);
            BuildMenuButton(_canvasRoot);
            BuildActionButtons(_canvasRoot);
            BuildPauseOverlay(_canvasRoot);
            BuildResultsOverlay(_canvasRoot);
            BuildScenarioPanel(_canvasRoot);
            BuildSaveLoadPanel(_canvasRoot);
            BuildEventPanel(_canvasRoot);
            BuildLifecyclePanel(_canvasRoot);
            BuildRatingPanel(_canvasRoot);
            BuildAlertBar(_canvasRoot);
            BuildSNSPanel(_canvasRoot);
            BuildResearchPanel(_canvasRoot);
            BuildLoanPanel(_canvasRoot);
            BuildZoneBar(_canvasRoot);
            BuildMonthlyReportPanel(_canvasRoot);
            BuildAnnualReportPanel(_canvasRoot);
            BuildFirstPersonOverlay(_canvasRoot);
            BuildCoasterDesignPanel(_canvasRoot);
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

            // メニューボタン群はPlaying中のみ
            bool isPlaying = (state == GameState.Playing);
            if (_menuBtn != null) _menuBtn.SetActive(isPlaying);
            if (_buildBtn != null) _buildBtn.SetActive(isPlaying);
            if (_staffBtn != null) _staffBtn.SetActive(isPlaying);
            if (_researchBtn != null) _researchBtn.SetActive(isPlaying);
            if (_loanBtn != null) _loanBtn.SetActive(isPlaying);
            if (_coasterBtn != null) _coasterBtn.SetActive(isPlaying);
            if (_zoneBar != null) _zoneBar.SetActive(isPlaying);

            // GameOver表示
            if (state == GameState.GameOver)
            {
                RefreshResults();
                return;
            }

            if (state != GameState.Playing) return;

            HandleVisitorClick();

            // 月次レポート自動クローズ
            if (_monthlyReportPanel != null && _monthlyReportPanel.activeSelf)
            {
                _monthlyReportAutoClose -= Time.unscaledDeltaTime;
                if (_monthlyReportAutoClose <= 0f)
                    _monthlyReportPanel.SetActive(false);
            }

            _updateTimer -= Time.unscaledDeltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = UpdateInterval;

            RefreshAll();

            if (_selectedVisitor != null && _visitorInfoPanel.activeSelf)
            {
                RefreshVisitorInfo();
            }

            if (_selectedFacility != null && _facilityInfoPanel.activeSelf)
            {
                RefreshFacilityInfo();
            }

            UpdateFloatingScores();
            UpdateScreenFlash();
        }

        // ================================================================
        // HUD定期更新
        // ================================================================

        private void RefreshAll()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // ---- PS1ステータスバー更新 ----
            RefreshPS1StatusBar();

            // ---- スコアボード ----
            if (gm.VisitorManager != null)
            {
                int active = gm.VisitorManager.ActiveVisitorCount;
                int today = gm.VisitorManager.TotalVisitorsToday;
                float avgHappy = gm.VisitorManager.AverageHappiness;

                _sbVisitorValue.text = $"{active}";

                float avgSat = gm.VisitorManager.AverageSatisfaction;
                _sbSatisfactionValue.text = $"{avgSat:F0}% (幸福:{avgHappy:F0}%)";
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
                float fee = gm.EconomyManager.Pricing?.EntranceFee ?? 0f;
                if (_moneyText != null)
                    _moneyText.text = $"資金: ${gm.EconomyManager.CurrentMoney:N0}  入場料: ${fee:N0}";
            }

            // ---- 時間 + 天候 ----
            string time = "---";
            if (gm.TimeManager != null)
            {
                var tm = gm.TimeManager;
                int h = (int)tm.CurrentHour;
                int m = (int)((tm.CurrentHour - h) * 60);
                time = $"{tm.CurrentYear}年{tm.CurrentMonth}月{tm.CurrentDay}日 {h:D2}:{m:D2}";
            }
            string weather = gm.WeatherSystem != null ? WeatherLabel(gm.WeatherSystem.CurrentWeather) : "";
            if (_timeWeatherText != null)
                _timeWeatherText.text = $"{weather}  {time}";

            // ---- スタッフ ----
            if (gm.StaffManager != null)
            {
                var sm = gm.StaffManager;
                int mech = sm.GetStaffCount(StaffType.Mechanic);
                int cln = sm.GetStaffCount(StaffType.Cleaner);
                int ent = sm.GetStaffCount(StaffType.Entertainer);
                int grd = sm.GetStaffCount(StaffType.Guard);
                int sci = sm.GetStaffCount(StaffType.Scientist);
                int striking = sm.StrikingStaffCount;
                string strikeWarn = striking > 0 ? $" <color=#FF4444>スト:{striking}</color>" : "";
                if (_staffText != null)
                    _staffText.text = $"修:{mech} 掃:{cln} 芸:{ent} 警:{grd} 研:{sci}{strikeWarn}";
            }

            // ---- 通路混雑度 ----
            UpdateCongestionDisplay();

            // ---- イベント表示 ----
            UpdateEventPanel();

            // ---- ライフサイクルフェーズ ----
            UpdateLifecyclePanel();

            // ---- パーク評価 ----
            UpdateRatingPanel();

            // ---- アラートバー ----
            UpdateAlertBar();

            // ---- SNSフィード ----
            UpdateSNSPanel();

            // ---- 研究パネル ----
            UpdateResearchPanel();

            // ---- ローンパネル ----
            UpdateLoanPanel();

            // ---- ゾーンバー ----
            UpdateZoneBar();

            // ---- ファーストパーソンビュー ----
            UpdateFirstPersonOverlay();

            // FPV中はメインHUD要素を非表示
            bool fpvActive = false;
            {
                var fpCam = Camera.main != null ? Camera.main.GetComponent<FirstPersonCamera>() : null;
                fpvActive = fpCam != null && fpCam.IsActive;
            }

            // ---- 速度 ----
            _currentSpeed = Time.timeScale;
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

        // ================================================================
        // UI構築ヘルパー（共有）
        // ================================================================

        private static Font CachedFont()
        {
            return FontManager.Regular;
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

        private static void SetBarWidth(Image bar, float ratio, float maxW)
        {
            if (bar == null) return;
            var rt = bar.rectTransform;
            rt.sizeDelta = new Vector2(maxW * Mathf.Clamp01(ratio), 0f);
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
        // 表示ラベル（共有）
        // ================================================================

        private static string WeatherLabel(Weather w)
        {
            switch (w)
            {
                case Weather.Sunny:       return "[晴れ]";
                case Weather.Cloudy:      return "[曇り]";
                case Weather.Rainy:       return "[雨]";
                case Weather.Snowy:       return "[雪]";
                case Weather.Hot:         return "[猛暑]";
                case Weather.Typhoon:     return "[台風]";
                case Weather.Thunderstorm:return "[雷雨]";
                default: return "";
            }
        }

        private static string CycleLabel(RideCycleState s)
        {
            switch (s)
            {
                case RideCycleState.WaitingForRiders: return "[待機中]";
                case RideCycleState.Loading:          return "[乗車中]";
                case RideCycleState.Running:          return "[運行中]";
                case RideCycleState.Unloading:        return "[降車中]";
                case RideCycleState.BrokenDown:       return "[故障中]";
                case RideCycleState.Accident:         return "[事故発生]";
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
                case VisitorType.Kids:   return LocalizationData.VisitorKids;
                case VisitorType.Young:  return LocalizationData.VisitorYoung;
                case VisitorType.Family: return LocalizationData.VisitorFamily;
                case VisitorType.Couple: return LocalizationData.VisitorCouple;
                case VisitorType.Senior: return LocalizationData.VisitorSenior;
                case VisitorType.VIP:    return LocalizationData.VisitorVIP;
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
