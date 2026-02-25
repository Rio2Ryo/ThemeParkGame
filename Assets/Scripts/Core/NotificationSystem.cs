// ============================================================
// ThemeParkGame - Notification & Event Log System
// ポップアップ通知 + 履歴ログ（GameEvents連動）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ThemeParkGame.Core
{
    /// <summary>通知の重要度</summary>
    public enum NotifLevel
    {
        Info,      // 通常（青系）
        Success,   // 成功（緑系）
        Warning,   // 警告（黄系）
        Danger     // 危険（赤系）
    }

    /// <summary>通知ログエントリ</summary>
    public class NotifEntry
    {
        public string Message;
        public NotifLevel Level;
        public float Timestamp; // Time.unscaledTime
        public string GameTime; // ゲーム内時間文字列
    }

    /// <summary>
    /// ゲームイベントをポップアップ通知として画面右に表示し、
    /// 履歴ログとして保持するシステム。
    /// </summary>
    public class NotificationSystem : MonoBehaviour
    {
        public static NotificationSystem Instance { get; private set; }

        private const int MAX_LOG_ENTRIES = 100;
        private const int MAX_VISIBLE_POPUPS = 5;
        private const float POPUP_LIFETIME = 4.5f;
        private const float POPUP_FADE_TIME = 0.8f;
        private const float POPUP_SLIDE_SPEED = 400f;

        // ログ
        private readonly List<NotifEntry> _log = new List<NotifEntry>();
        public IReadOnlyList<NotifEntry> Log => _log;
        public int UnreadCount { get; private set; }

        // ポップアップUI
        private Canvas _popupCanvas;
        private RectTransform _popupContainer;
        private readonly List<PopupInstance> _activePopups = new List<PopupInstance>();
        private readonly Queue<NotifEntry> _pendingPopups = new Queue<NotifEntry>();

        // ログパネルUI
        private GameObject _logPanel;
        private RectTransform _logContent;
        private Text _logHeaderCount;
        private readonly List<GameObject> _logItems = new List<GameObject>();

        // カラー
        private static readonly Color InfoColor = new Color(0.3f, 0.6f, 0.85f);
        private static readonly Color SuccessColor = new Color(0.3f, 0.78f, 0.45f);
        private static readonly Color WarningColor = new Color(0.9f, 0.78f, 0.25f);
        private static readonly Color DangerColor = new Color(0.9f, 0.3f, 0.25f);
        private static readonly Color BgDark = new Color(0.04f, 0.06f, 0.14f, 0.94f);
        private static readonly Color TextMuted = new Color(0.55f, 0.58f, 0.65f);

        // スロットル（同種イベントの連続通知を防ぐ）
        private readonly Dictionary<string, float> _throttle = new Dictionary<string, float>();
        private const float THROTTLE_INTERVAL = 5f;

        private class PopupInstance
        {
            public GameObject Go;
            public RectTransform Rt;
            public Image Bg;
            public Image Accent;
            public Text MessageText;
            public CanvasGroup CGroup;
            public float Timer;
            public float TargetY;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            BuildPopupUI();
            BuildLogPanel();
        }

        private void Start()
        {
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeFromEvents();
        }

        // ================================================================
        // 公開API
        // ================================================================

        /// <summary>通知を追加する</summary>
        public void Notify(string message, NotifLevel level = NotifLevel.Info)
        {
            // ゲーム内時間
            string gameTime = "";
            if (GameManager.Instance?.TimeManager != null)
            {
                var tm = GameManager.Instance.TimeManager;
                int h = (int)tm.CurrentHour;
                gameTime = $"Y{tm.CurrentYear} M{tm.CurrentMonth} D{tm.CurrentDay} {h:D2}:00";
            }

            var entry = new NotifEntry
            {
                Message = message,
                Level = level,
                Timestamp = Time.unscaledTime,
                GameTime = gameTime
            };

            _log.Add(entry);
            if (_log.Count > MAX_LOG_ENTRIES)
                _log.RemoveAt(0);

            UnreadCount++;
            _pendingPopups.Enqueue(entry);
        }

        /// <summary>スロットル付き通知（同じkeyは一定時間内に1回のみ）</summary>
        public void NotifyThrottled(string key, string message, NotifLevel level = NotifLevel.Info)
        {
            float now = Time.unscaledTime;
            if (_throttle.TryGetValue(key, out float lastTime) && now - lastTime < THROTTLE_INTERVAL)
                return;
            _throttle[key] = now;
            Notify(message, level);
        }

        /// <summary>未読カウントをリセット</summary>
        public void MarkAllRead() { UnreadCount = 0; }

        /// <summary>ログをクリア</summary>
        public void ClearLog()
        {
            _log.Clear();
            UnreadCount = 0;
        }

        /// <summary>ログパネル表示</summary>
        public void ShowLogPanel()
        {
            if (_logPanel == null) return;
            RefreshLogPanel();
            UnreadCount = 0;
            _logPanel.SetActive(true);
            var dim = _logPanel.transform.parent.Find("LogDim");
            if (dim != null) dim.gameObject.SetActive(true);
        }

        /// <summary>ログパネル非表示</summary>
        public void HideLogPanel()
        {
            if (_logPanel != null) _logPanel.SetActive(false);
            var dim = _logPanel?.transform.parent?.Find("LogDim");
            if (dim != null) dim.gameObject.SetActive(false);
        }

        public bool IsLogVisible => _logPanel != null && _logPanel.activeSelf;

        // ================================================================
        // GameEvents購読
        // ================================================================

        private void SubscribeToEvents()
        {
            GameEvents.OnAttractionBuilt += OnAttractionBuilt;
            GameEvents.OnAttractionBrokenDown += OnAttractionBrokenDown;
            GameEvents.OnAttractionRepaired += OnAttractionRepaired;
            GameEvents.OnAttractionAccident += OnAttractionAccident;
            GameEvents.OnAttractionUpgraded += OnAttractionUpgraded;
            GameEvents.OnStaffHired += OnStaffHired;
            GameEvents.OnStaffFired += OnStaffFired;
            GameEvents.OnStaffWentOnStrike += OnStaffOnStrike;
            GameEvents.OnStaffFinishedTask += OnStaffFinishedTask;
            GameEvents.OnVisitorVomited += OnVisitorVomited;
            GameEvents.OnVisitorHadAccident += OnVisitorAccident;
            GameEvents.OnWeatherChanged += OnWeatherChanged;
            GameEvents.OnResearchStarted += OnResearchStarted;
            GameEvents.OnResearchCompleted += OnResearchCompleted;
            GameEvents.OnGoldenTicketEarned += OnGoldenTicket;
            GameEvents.OnCertificateAwarded += OnCertificate;
            GameEvents.OnVIPArrived += OnVIPArrived;
            GameEvents.OnVIPRequestCompleted += OnVIPRequestCompleted;
            GameEvents.OnParkYearPassed += OnYearPassed;
            GameEvents.OnThemeZoneUnlocked += OnZoneUnlocked;
            GameEvents.OnGameSaved += OnGameSaved;
            GameEvents.OnGameLoaded += OnGameLoaded;
            GameEvents.OnParkRatingChanged += OnParkRatingChanged;
            GameEvents.OnParkEventStarted += OnParkEventStarted;
            GameEvents.OnParkEventEnded += OnParkEventEnded;
            GameEvents.OnPathwayCongestionChanged += OnPathwayCongestion;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnAttractionBuilt -= OnAttractionBuilt;
            GameEvents.OnAttractionBrokenDown -= OnAttractionBrokenDown;
            GameEvents.OnAttractionRepaired -= OnAttractionRepaired;
            GameEvents.OnAttractionAccident -= OnAttractionAccident;
            GameEvents.OnAttractionUpgraded -= OnAttractionUpgraded;
            GameEvents.OnStaffHired -= OnStaffHired;
            GameEvents.OnStaffFired -= OnStaffFired;
            GameEvents.OnStaffWentOnStrike -= OnStaffOnStrike;
            GameEvents.OnStaffFinishedTask -= OnStaffFinishedTask;
            GameEvents.OnVisitorVomited -= OnVisitorVomited;
            GameEvents.OnVisitorHadAccident -= OnVisitorAccident;
            GameEvents.OnWeatherChanged -= OnWeatherChanged;
            GameEvents.OnResearchStarted -= OnResearchStarted;
            GameEvents.OnResearchCompleted -= OnResearchCompleted;
            GameEvents.OnGoldenTicketEarned -= OnGoldenTicket;
            GameEvents.OnCertificateAwarded -= OnCertificate;
            GameEvents.OnVIPArrived -= OnVIPArrived;
            GameEvents.OnVIPRequestCompleted -= OnVIPRequestCompleted;
            GameEvents.OnParkYearPassed -= OnYearPassed;
            GameEvents.OnThemeZoneUnlocked -= OnZoneUnlocked;
            GameEvents.OnGameSaved -= OnGameSaved;
            GameEvents.OnGameLoaded -= OnGameLoaded;
            GameEvents.OnParkRatingChanged -= OnParkRatingChanged;
            GameEvents.OnParkEventStarted -= OnParkEventStarted;
            GameEvents.OnParkEventEnded -= OnParkEventEnded;
            GameEvents.OnPathwayCongestionChanged -= OnPathwayCongestion;
        }

        // イベントハンドラ - アトラクション
        private void OnAttractionBuilt(int id) =>
            Notify($"アトラクション #{id} が建設されました", NotifLevel.Success);
        private void OnAttractionBrokenDown(int id) =>
            NotifyThrottled($"breakdown_{id}", $"アトラクション #{id} が故障しました！", NotifLevel.Warning);
        private void OnAttractionRepaired(int id) =>
            Notify($"アトラクション #{id} が修理完了", NotifLevel.Success);
        private void OnAttractionAccident(int id) =>
            Notify($"アトラクション #{id} で事故発生！", NotifLevel.Danger);
        private void OnAttractionUpgraded(int id) =>
            Notify($"アトラクション #{id} をアップグレードしました", NotifLevel.Success);

        // イベントハンドラ - スタッフ
        private void OnStaffHired(int id, StaffType type) =>
            Notify($"{StaffTypeName(type)}を雇用しました (#{id})", NotifLevel.Info);
        private void OnStaffFired(int id, StaffType type) =>
            Notify($"{StaffTypeName(type)} #{id} を解雇しました", NotifLevel.Info);
        private void OnStaffOnStrike(int id) =>
            Notify($"スタッフ #{id} がストライキ中！", NotifLevel.Warning);
        private void OnStaffFinishedTask(int id) =>
            NotifyThrottled("staff_task", $"スタッフ #{id} がタスク完了", NotifLevel.Info);

        // イベントハンドラ - 来場者
        private void OnVisitorVomited(int id) =>
            NotifyThrottled("vomit", "来場者が嘔吐しました...", NotifLevel.Warning);
        private void OnVisitorAccident(int id) =>
            NotifyThrottled("visitor_accident", "来場者がトイレ事故を起こしました", NotifLevel.Warning);

        // イベントハンドラ - 天候/研究/パーク
        private void OnWeatherChanged(Weather w) =>
            Notify($"天候が {WeatherName(w)} に変化しました", NotifLevel.Info);
        private void OnResearchStarted(string researchId) =>
            Notify($"研究開始: {researchId}", NotifLevel.Info);
        private void OnResearchCompleted(string researchId) =>
            Notify($"研究完了: {researchId}", NotifLevel.Success);
        private void OnGoldenTicket(int count) =>
            Notify($"ゴールデンチケット獲得！ (計{count}枚)", NotifLevel.Success);
        private void OnCertificate(CertificateCategory cat) =>
            Notify($"{CertName(cat)} 認定証を獲得！", NotifLevel.Success);
        private void OnVIPArrived(int id) =>
            Notify($"VIP来場者 #{id} が到着しました！", NotifLevel.Info);
        private void OnVIPRequestCompleted(int id, bool success) =>
            Notify(success ? $"VIP #{id} のリクエスト完了！" : $"VIP #{id} のリクエスト失敗...",
                   success ? NotifLevel.Success : NotifLevel.Warning);
        private void OnYearPassed(int year) =>
            Notify($"Year {year} に突入！", NotifLevel.Info);
        private void OnZoneUnlocked(ThemeZone zone) =>
            Notify($"新エリア解放: {zone}", NotifLevel.Success);
        private void OnGameSaved() =>
            Notify("ゲームをセーブしました", NotifLevel.Info);
        private void OnGameLoaded() =>
            Notify("セーブデータをロードしました", NotifLevel.Info);

        // イベントハンドラ - パーク評価
        private void OnParkRatingChanged(float newRating, float oldRating)
        {
            float delta = newRating - oldRating;
            if (Mathf.Abs(delta) < 3f) return; // 小さな変動は無視
            if (delta > 0)
                NotifyThrottled("rating_up", $"パーク評価が上昇！ ({oldRating:F0} -> {newRating:F0})", NotifLevel.Success);
            else
                NotifyThrottled("rating_down", $"パーク評価が下落 ({oldRating:F0} -> {newRating:F0})", NotifLevel.Warning);
        }

        // イベントハンドラ - パークイベント
        private void OnParkEventStarted(string eventId, string displayName) =>
            Notify($"イベント開始: {displayName}", NotifLevel.Info);
        private void OnParkEventEnded(string eventId, string displayName) =>
            Notify($"イベント終了: {displayName}", NotifLevel.Info);

        // イベントハンドラ - 通路混雑
        private void OnPathwayCongestion(float avg)
        {
            if (avg > 0.8f)
                NotifyThrottled("congestion", $"通路が非常に混雑しています (混雑度{avg * 100f:F0}%)", NotifLevel.Warning);
        }

        // ================================================================
        // ポップアップUI構築
        // ================================================================

        private void BuildPopupUI()
        {
            var canvasGo = new GameObject("NotificationCanvas");
            canvasGo.transform.SetParent(transform, false);
            _popupCanvas = canvasGo.AddComponent<Canvas>();
            _popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _popupCanvas.sortingOrder = 70;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // ポップアップコンテナ（右端中央寄り）
            var containerGo = new GameObject("PopupContainer");
            containerGo.transform.SetParent(canvasGo.transform, false);
            _popupContainer = containerGo.AddComponent<RectTransform>();
            _popupContainer.anchorMin = _popupContainer.anchorMax = new Vector2(1f, 0.5f);
            _popupContainer.pivot = new Vector2(1f, 0.5f);
            _popupContainer.anchoredPosition = new Vector2(-12f, 100f);
            _popupContainer.sizeDelta = new Vector2(360f, 500f);
        }

        private PopupInstance CreatePopup(NotifEntry entry)
        {
            float popW = 350f;
            float popH = 56f;

            var go = new GameObject("Popup");
            go.transform.SetParent(_popupContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(popW, popH);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.16f, 0.94f);
            bg.raycastTarget = false;

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f; // フェードインで開始

            // アクセントライン（左端）
            var accentGo = MakePanel(rt, "Accent", 3f, popH, LevelColor(entry.Level));
            var accentRt = accentGo.GetComponent<RectTransform>();
            accentRt.anchorMin = accentRt.anchorMax = new Vector2(0f, 0.5f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;

            // レベルアイコン
            var icon = MakeLabel(rt, "Icon", LevelIcon(entry.Level), 18,
                LevelColor(entry.Level), FontStyle.Bold, TextAnchor.MiddleCenter);
            var iRt = icon.rectTransform;
            iRt.anchorMin = iRt.anchorMax = new Vector2(0f, 0.5f);
            iRt.pivot = new Vector2(0f, 0.5f);
            iRt.anchoredPosition = new Vector2(10f, 0f);
            iRt.sizeDelta = new Vector2(28f, 28f);

            // メッセージ
            var msg = MakeLabel(rt, "Msg", entry.Message, 15,
                Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            msg.horizontalOverflow = HorizontalWrapMode.Wrap;
            var mRt = msg.rectTransform;
            mRt.anchorMin = mRt.anchorMax = new Vector2(0f, 0.5f);
            mRt.pivot = new Vector2(0f, 0.5f);
            mRt.anchoredPosition = new Vector2(42f, 4f);
            mRt.sizeDelta = new Vector2(popW - 54f, 30f);

            // 時間
            var time = MakeLabel(rt, "Time", entry.GameTime, 11,
                TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            var tRt = time.rectTransform;
            tRt.anchorMin = tRt.anchorMax = new Vector2(0f, 0f);
            tRt.pivot = new Vector2(0f, 0f);
            tRt.anchoredPosition = new Vector2(42f, 4f);
            tRt.sizeDelta = new Vector2(popW - 54f, 16f);

            return new PopupInstance
            {
                Go = go,
                Rt = rt,
                Bg = bg,
                Accent = accentGo.GetComponent<Image>(),
                MessageText = msg,
                CGroup = cg,
                Timer = POPUP_LIFETIME,
                TargetY = 0f
            };
        }

        // ================================================================
        // Update: ポップアップアニメーション
        // ================================================================

        private void Update()
        {
            if (GameManager.Instance == null) return;

            var state = GameManager.Instance.CurrentState;
            bool visible = state == GameState.Playing || state == GameState.Paused;
            if (_popupCanvas != null && _popupCanvas.gameObject.activeSelf != visible)
                _popupCanvas.gameObject.SetActive(visible);

            if (!visible) return;

            // 新しいポップアップを生成
            while (_pendingPopups.Count > 0 && _activePopups.Count < MAX_VISIBLE_POPUPS)
            {
                var entry = _pendingPopups.Dequeue();
                var popup = CreatePopup(entry);
                _activePopups.Add(popup);
                RecalculatePositions();
            }

            // 超過分は破棄（古い順）
            while (_pendingPopups.Count > 0 && _activePopups.Count >= MAX_VISIBLE_POPUPS)
            {
                _pendingPopups.Dequeue();
            }

            float dt = Time.unscaledDeltaTime;

            // ポップアップ更新
            for (int i = _activePopups.Count - 1; i >= 0; i--)
            {
                var p = _activePopups[i];
                p.Timer -= dt;

                // フェードイン（最初の0.3秒）
                float age = POPUP_LIFETIME - p.Timer;
                if (age < 0.3f)
                    p.CGroup.alpha = Mathf.Clamp01(age / 0.3f);
                // フェードアウト
                else if (p.Timer <= POPUP_FADE_TIME)
                    p.CGroup.alpha = Mathf.Clamp01(p.Timer / POPUP_FADE_TIME);
                else
                    p.CGroup.alpha = 1f;

                // スライドアニメーション
                var pos = p.Rt.anchoredPosition;
                pos.y = Mathf.MoveTowards(pos.y, p.TargetY, POPUP_SLIDE_SPEED * dt);
                p.Rt.anchoredPosition = pos;

                // 消滅
                if (p.Timer <= 0f)
                {
                    Destroy(p.Go);
                    _activePopups.RemoveAt(i);
                    RecalculatePositions();
                }
            }
        }

        private void RecalculatePositions()
        {
            float popH = 56f;
            float gap = 6f;
            for (int i = 0; i < _activePopups.Count; i++)
            {
                // 新しいものが上、古いものが下に押し出される
                int reverseIdx = _activePopups.Count - 1 - i;
                _activePopups[i].TargetY = -(reverseIdx * (popH + gap));
            }
        }

        // ================================================================
        // ログパネルUI構築
        // ================================================================

        private void BuildLogPanel()
        {
            var canvasRt = _popupCanvas.GetComponent<RectTransform>();

            // 半透明背景
            var dimGo = new GameObject("LogDim");
            dimGo.transform.SetParent(canvasRt, false);
            var dimRt = dimGo.AddComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.5f);
            dimImg.raycastTarget = true;
            var dimBtn = dimGo.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            var dc = dimBtn.colors;
            dc.highlightedColor = dimImg.color;
            dc.pressedColor = dimImg.color;
            dimBtn.colors = dc;
            dimBtn.onClick.AddListener(HideLogPanel);
            dimGo.SetActive(false);

            // メインパネル
            float panelW = 520f;
            float panelH = 580f;

            _logPanel = new GameObject("EventLogPanel");
            _logPanel.transform.SetParent(canvasRt, false);
            var panelRt = _logPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(panelW, panelH);

            var bgImg = _logPanel.AddComponent<Image>();
            bgImg.color = BgDark;
            bgImg.raycastTarget = true;

            // ヘッダー
            var title = MakeLabel(panelRt, "Title", "EVENT LOG", 28,
                new Color(0.95f, 0.88f, 0.45f), FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -10f);
            titleRt.sizeDelta = new Vector2(panelW, 36f);

            // エントリ数
            _logHeaderCount = MakeLabel(panelRt, "Count", "", 14,
                TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            var cRt = _logHeaderCount.rectTransform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.anchoredPosition = new Vector2(0f, -46f);
            cRt.sizeDelta = new Vector2(panelW, 20f);

            // スクロールビュー
            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(panelRt, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(8f, 52f);
            scrollRt.offsetMax = new Vector2(-8f, -70f);

            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.2f);
            scrollImg.raycastTarget = true;
            var scrollMask = scrollGo.AddComponent<Mask>();
            scrollMask.showMaskGraphic = true;

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            // コンテンツ
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            _logContent = contentGo.AddComponent<RectTransform>();
            _logContent.anchorMin = new Vector2(0f, 1f);
            _logContent.anchorMax = new Vector2(1f, 1f);
            _logContent.pivot = new Vector2(0.5f, 1f);
            _logContent.anchoredPosition = Vector2.zero;
            _logContent.sizeDelta = new Vector2(0f, 0f);

            scrollRect.content = _logContent;

            // 閉じるボタン
            var closeGo = MakePanel(panelRt, "CloseBtn", 140f, 38f, new Color(0.35f, 0.38f, 0.48f));
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 8f);
            var closeImg2 = closeGo.GetComponent<Image>();
            closeImg2.raycastTarget = true;
            var closeBtn2 = closeGo.AddComponent<Button>();
            closeBtn2.targetGraphic = closeImg2;
            var cc = closeBtn2.colors;
            cc.highlightedColor = new Color(0.45f, 0.48f, 0.58f);
            cc.pressedColor = new Color(0.25f, 0.28f, 0.38f);
            closeBtn2.colors = cc;
            closeBtn2.onClick.AddListener(HideLogPanel);
            var closeLabel = MakeLabel(closeRt, "Label", "CLOSE", 18,
                Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLabel.rectTransform);

            _logPanel.SetActive(false);
        }

        private void RefreshLogPanel()
        {
            // 既存アイテム破棄
            foreach (var item in _logItems)
                Destroy(item);
            _logItems.Clear();

            if (_logHeaderCount != null)
                _logHeaderCount.text = $"{_log.Count} entries";

            float itemH = 48f;
            float gap = 2f;
            float y = 0f;

            // 新しい順に表示
            for (int i = _log.Count - 1; i >= 0; i--)
            {
                var entry = _log[i];
                var item = CreateLogItem(entry, y);
                _logItems.Add(item);
                y += itemH + gap;
            }

            // コンテンツの高さ更新
            _logContent.sizeDelta = new Vector2(0f, y);
        }

        private GameObject CreateLogItem(NotifEntry entry, float y)
        {
            float itemW = 500f;
            float itemH = 48f;

            var go = new GameObject("LogEntry");
            go.transform.SetParent(_logContent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(2f, -y);
            rt.sizeDelta = new Vector2(itemW, itemH);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.1f, 0.18f, 0.7f);
            bg.raycastTarget = false;

            // アクセント
            var accentGo = MakePanel(rt, "Accent", 3f, itemH, LevelColor(entry.Level));
            var accentRt = accentGo.GetComponent<RectTransform>();
            accentRt.anchorMin = accentRt.anchorMax = new Vector2(0f, 0.5f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;

            // アイコン
            var icon = MakeLabel(rt, "Icon", LevelIcon(entry.Level), 14,
                LevelColor(entry.Level), FontStyle.Bold, TextAnchor.MiddleCenter);
            var iRt = icon.rectTransform;
            iRt.anchorMin = iRt.anchorMax = new Vector2(0f, 0.5f);
            iRt.pivot = new Vector2(0f, 0.5f);
            iRt.anchoredPosition = new Vector2(8f, 0f);
            iRt.sizeDelta = new Vector2(22f, 22f);

            // メッセージ
            var msg = MakeLabel(rt, "Msg", entry.Message, 14,
                Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            msg.horizontalOverflow = HorizontalWrapMode.Wrap;
            var mRt = msg.rectTransform;
            mRt.anchorMin = mRt.anchorMax = new Vector2(0f, 1f);
            mRt.pivot = new Vector2(0f, 1f);
            mRt.anchoredPosition = new Vector2(34f, -2f);
            mRt.sizeDelta = new Vector2(itemW - 44f, 24f);

            // 時間
            var time = MakeLabel(rt, "Time", entry.GameTime, 11,
                TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            var tRt = time.rectTransform;
            tRt.anchorMin = tRt.anchorMax = new Vector2(0f, 0f);
            tRt.pivot = new Vector2(0f, 0f);
            tRt.anchoredPosition = new Vector2(34f, 2f);
            tRt.sizeDelta = new Vector2(itemW - 44f, 18f);

            return go;
        }

        // ================================================================
        // ヘルパー
        // ================================================================

        private static Color LevelColor(NotifLevel level)
        {
            switch (level)
            {
                case NotifLevel.Success: return SuccessColor;
                case NotifLevel.Warning: return WarningColor;
                case NotifLevel.Danger:  return DangerColor;
                default:                 return InfoColor;
            }
        }

        private static string LevelIcon(NotifLevel level)
        {
            switch (level)
            {
                case NotifLevel.Success: return "[+]";
                case NotifLevel.Warning: return "[!]";
                case NotifLevel.Danger:  return "[X]";
                default:                 return "[i]";
            }
        }

        private static string StaffTypeName(StaffType type)
        {
            switch (type)
            {
                case StaffType.Mechanic:     return "メカニック";
                case StaffType.Cleaner:      return "スイーパー";
                case StaffType.Entertainer:  return "エンターテイナー";
                case StaffType.Guard:        return "ガード";
                case StaffType.Scientist:    return "サイエンティスト";
                default:                     return type.ToString();
            }
        }

        private static string WeatherName(Weather w)
        {
            switch (w)
            {
                case Weather.Sunny:  return "晴れ";
                case Weather.Cloudy: return "曇り";
                case Weather.Rainy:  return "雨";
                case Weather.Snowy:  return "雪";
                case Weather.Hot:    return "猛暑";
                default:             return w.ToString();
            }
        }

        private static string CertName(CertificateCategory cat)
        {
            switch (cat)
            {
                case CertificateCategory.Fame:       return "名声";
                case CertificateCategory.Safety:     return "安全";
                case CertificateCategory.Comfort:    return "快適";
                case CertificateCategory.Excitement: return "興奮";
                case CertificateCategory.Mood:       return "ムード";
                default:                             return cat.ToString();
            }
        }

        // ================================================================
        // UIヘルパー
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
