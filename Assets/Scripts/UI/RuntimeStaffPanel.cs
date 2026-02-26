// ============================================================
// ThemeParkGame - RuntimeStaffPanel
// コードベースのスタッフ管理パネルUI（プレハブ/TMPro不要）
// スタッフの雇用・解雇・一覧表示を提供する
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;
using ThemeParkGame.Staff;

namespace ThemeParkGame.UI
{
    /// <summary>
    /// ランタイムで完全にコードから構築されるスタッフ管理パネル。
    /// スタッフ種別ごとの雇用ボタンと現在のスタッフ一覧を表示する。
    /// </summary>
    public class RuntimeStaffPanel : MonoBehaviour
    {
        public static RuntimeStaffPanel Instance { get; private set; }

        // ---- Canvas ----
        private Canvas _canvas;
        private GameObject _panelRoot;
        private bool _isOpen;

        // ---- 雇用ボタン ----
        private Text _moneyText;

        // ---- スタッフ一覧 ----
        private RectTransform _staffListContainer;
        private readonly List<GameObject> _staffCards = new List<GameObject>();
        private float _refreshTimer;
        private const float RefreshInterval = 1.0f;

        // ---- カラー ----
        private static readonly Color BgDark = new Color(0.06f, 0.09f, 0.16f, 0.94f);
        private static readonly Color BgMedium = new Color(0.12f, 0.15f, 0.22f, 0.94f);
        private static readonly Color BgLight = new Color(0.18f, 0.22f, 0.3f, 0.9f);
        private static readonly Color AccentBlue = new Color(0.3f, 0.65f, 0.95f);
        private static readonly Color AccentGreen = new Color(0.35f, 0.9f, 0.45f);
        private static readonly Color AccentRed = new Color(0.95f, 0.35f, 0.35f);
        private static readonly Color AccentYellow = new Color(0.95f, 0.88f, 0.45f);
        private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.68f);

        // スタッフ種別情報
        private static readonly StaffType[] StaffTypes = {
            StaffType.Mechanic, StaffType.Cleaner,
            StaffType.Entertainer, StaffType.Guard, StaffType.Scientist
        };
        private static readonly string[] StaffTypeNames = {
            "メカニック", "クリーナー", "エンターテイナー", "ガード", "サイエンティスト"
        };
        private static readonly string[] StaffTypeDescs = {
            "故障したアトラクションを修理する",
            "パーク内を清掃して清潔に保つ",
            "来場者を楽しませて満足度を上げる",
            "パーク内の安全を守り問題に対処する",
            "新アトラクション・施設を研究開発する"
        };
        private static readonly Color[] StaffTypeColors = {
            new Color(1f, 0.5f, 0f),     // Mechanic: orange
            new Color(0.2f, 0.9f, 0.2f), // Cleaner: green
            new Color(0.9f, 0.2f, 0.9f), // Entertainer: purple
            new Color(0.2f, 0.2f, 0.8f), // Guard: blue
            new Color(1f, 1f, 0.3f)      // Scientist: yellow
        };

        // ================================================================
        // ライフサイクル
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            BuildUI();
            _panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_isOpen) return;

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = RefreshInterval;

            RefreshStaffList();
            UpdateMoney();
        }

        // ================================================================
        // 公開API
        // ================================================================

        public bool IsOpen => _isOpen;

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _panelRoot.SetActive(true);
            RefreshStaffList();
            UpdateMoney();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _panelRoot.SetActive(false);
        }

        // ================================================================
        // UI構築
        // ================================================================

        private void BuildUI()
        {
            var cGo = new GameObject("StaffPanel_Canvas");
            cGo.transform.SetParent(transform, false);
            _canvas = cGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 56;

            var scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            cGo.AddComponent<GraphicRaycaster>();
            var canvasRoot = cGo.GetComponent<RectTransform>();

            // パネルルート（画面右側）
            float panelW = 420f;
            _panelRoot = MakePanel(canvasRoot, "StaffPanelRoot", panelW, 0f, BgDark);
            var panelRt = _panelRoot.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(1f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(panelW, 0f);

            // ヘッダー
            BuildHeader(panelRt, panelW);

            // 雇用ボタン領域
            BuildHireButtons(panelRt, panelW);

            // スタッフ一覧（スクロール）
            BuildStaffList(panelRt, panelW);
        }

        private void BuildHeader(RectTransform parent, float panelW)
        {
            var header = MakePanel(parent, "Header", 0f, 44f, new Color(0.1f, 0.13f, 0.2f, 0.98f));
            var hrt = header.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = Vector2.zero;
            hrt.sizeDelta = new Vector2(0f, 44f);

            var title = MakeLabel(hrt, "Title", "スタッフ管理", 18,
                AccentBlue, FontStyle.Bold, TextAnchor.MiddleLeft);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(0.6f, 1f);
            trt.offsetMin = new Vector2(14f, 0f);
            trt.offsetMax = Vector2.zero;

            _moneyText = MakeLabel(hrt, "Money", "", 14,
                AccentYellow, FontStyle.Bold, TextAnchor.MiddleRight);
            var mrt = _moneyText.rectTransform;
            mrt.anchorMin = new Vector2(0.5f, 0f);
            mrt.anchorMax = new Vector2(0.8f, 1f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;

            // 閉じるボタン
            var closeGo = MakePanel(hrt, "CloseBtn", 70f, 30f, AccentRed);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-8f, 0f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.raycastTarget = true;
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(Close);
            var closeLabel = MakeLabel(closeRt, "X", "閉じる", 13,
                Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLabel.rectTransform);
        }

        private void BuildHireButtons(RectTransform parent, float panelW)
        {
            float topOffset = 48f;
            float btnH = 50f;
            float spacing = 2f;

            var hireArea = MakePanel(parent, "HireArea", 0f, 0f, BgMedium);
            var hart = hireArea.GetComponent<RectTransform>();
            hart.anchorMin = new Vector2(0f, 1f);
            hart.anchorMax = new Vector2(1f, 1f);
            hart.pivot = new Vector2(0.5f, 1f);
            hart.anchoredPosition = new Vector2(0f, -topOffset);
            float totalH = StaffTypes.Length * (btnH + spacing) + 8f;
            hart.sizeDelta = new Vector2(0f, totalH);

            var sectionLabel = MakeLabel(hart, "SectionLabel", "-- 雇用 --", 13,
                TextMuted, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceInParent(sectionLabel.rectTransform, 0f, -2f, panelW, 16f);

            for (int i = 0; i < StaffTypes.Length; i++)
            {
                float y = -(20f + i * (btnH + spacing));
                CreateHireButton(hart, StaffTypes[i], StaffTypeNames[i],
                    StaffTypeDescs[i], StaffTypeColors[i], panelW, y, btnH);
            }
        }

        private void CreateHireButton(RectTransform parent, StaffType type,
            string typeName, string desc, Color typeColor, float panelW, float y, float btnH)
        {
            var btnGo = MakePanel(parent, $"Hire_{type}", panelW - 16f, btnH, BgLight);
            var brt = btnGo.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, y);

            var img = btnGo.GetComponent<Image>();
            img.raycastTarget = true;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.25f, 0.4f, 0.55f, 0.95f);
            colors.pressedColor = new Color(0.15f, 0.25f, 0.35f, 0.95f);
            btn.colors = colors;

            StaffType capturedType = type;
            btn.onClick.AddListener(() => OnHireClicked(capturedType));

            // カラーバー
            var colorBar = MakePanel(brt, "ColorBar", 6f, btnH - 4f, typeColor);
            PlaceInParent(colorBar.GetComponent<RectTransform>(), 2f, -2f, 6f, btnH - 4f);

            // 名前
            var nameText = MakeLabel(brt, "Name", typeName, 15,
                TextWhite, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(nameText.rectTransform, 14f, -2f, 200f, 22f);

            // 説明
            var descText = MakeLabel(brt, "Desc", desc, 11,
                TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(descText.rectTransform, 14f, -24f, 260f, 18f);

            // コスト表示
            var sm = GameManager.Instance?.StaffManager;
            float cost = sm != null ? sm.GetHiringCost(type) : 500f;
            float salary = sm != null ? sm.GetBaseSalary(type) : 300f;
            var costText = MakeLabel(brt, "Cost", $"${cost:N0}\n月給${salary:N0}", 11,
                AccentYellow, FontStyle.Normal, TextAnchor.MiddleRight);
            var crt = costText.rectTransform;
            crt.anchorMin = new Vector2(1f, 0f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-8f, 0f);
            crt.sizeDelta = new Vector2(90f, 0f);
        }

        private void BuildStaffList(RectTransform parent, float panelW)
        {
            float topOffset = 48f + StaffTypes.Length * 52f + 20f;

            // セクションラベル
            var secGo = MakePanel(parent, "ListHeader", 0f, 24f, new Color(0.1f, 0.12f, 0.18f, 0.9f));
            var secRt = secGo.GetComponent<RectTransform>();
            secRt.anchorMin = new Vector2(0f, 1f);
            secRt.anchorMax = new Vector2(1f, 1f);
            secRt.pivot = new Vector2(0.5f, 1f);
            secRt.anchoredPosition = new Vector2(0f, -topOffset);
            secRt.sizeDelta = new Vector2(0f, 24f);

            var secLabel = MakeLabel(secRt, "Label", "-- 現在のスタッフ --", 13,
                TextMuted, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(secLabel.rectTransform);

            // ScrollRect
            float listTop = topOffset + 28f;

            var scrollArea = new GameObject("StaffScrollArea");
            scrollArea.transform.SetParent(parent, false);
            var scrollRt = scrollArea.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(4f, 4f);
            scrollRt.offsetMax = new Vector2(-4f, -listTop);

            var scrollImg = scrollArea.AddComponent<Image>();
            scrollImg.color = new Color(0.08f, 0.1f, 0.15f, 0.8f);

            var scrollRect = scrollArea.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            scrollArea.AddComponent<Mask>().showMaskGraphic = true;

            var content = new GameObject("Content");
            content.transform.SetParent(scrollArea.transform, false);
            _staffListContainer = content.AddComponent<RectTransform>();
            _staffListContainer.anchorMin = new Vector2(0f, 1f);
            _staffListContainer.anchorMax = new Vector2(1f, 1f);
            _staffListContainer.pivot = new Vector2(0.5f, 1f);
            _staffListContainer.anchoredPosition = Vector2.zero;
            _staffListContainer.sizeDelta = new Vector2(0f, 0f);

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            scrollRect.content = _staffListContainer;
        }

        // ================================================================
        // 雇用
        // ================================================================

        private void OnHireClicked(StaffType type)
        {
            var sm = GameManager.Instance?.StaffManager;
            if (sm == null) return;

            // パーク中央付近にスポーン
            Vector3 spawnPos = new Vector3(
                UnityEngine.Random.Range(-10f, 10f), 0f,
                UnityEngine.Random.Range(-5f, 5f));

            var staff = sm.HireStaff(type, spawnPos);
            if (staff != null)
            {
                // パトロール範囲設定
                Bounds parkBounds = new Bounds(Vector3.zero, new Vector3(60f, 10f, 60f));
                if (!staff.HasPatrolArea)
                    staff.SetPatrolArea(parkBounds);

                NotificationSystem.Instance?.Notify(
                    $"{StaffTypeNameFor(type)} {staff.Name} を雇用しました！",
                    NotifLevel.Success);

                RefreshStaffList();
                UpdateMoney();
            }
            else
            {
                NotificationSystem.Instance?.Notify(
                    "雇用に失敗しました（資金不足または上限）",
                    NotifLevel.Warning);
            }
        }

        // ================================================================
        // スタッフ一覧更新
        // ================================================================

        private void RefreshStaffList()
        {
            foreach (var card in _staffCards)
                if (card != null) Destroy(card);
            _staffCards.Clear();

            var sm = GameManager.Instance?.StaffManager;
            if (sm == null) return;

            var allStaff = sm.GetAllStaff();
            if (allStaff == null) return;

            foreach (var staff in allStaff)
            {
                if (staff == null) continue;
                var card = CreateStaffCard(staff);
                _staffCards.Add(card);
            }
        }

        private GameObject CreateStaffCard(StaffMember staff)
        {
            float cardH = 48f;
            var cardGo = new GameObject($"Staff_{staff.Id}");
            cardGo.transform.SetParent(_staffListContainer, false);
            var cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(0f, cardH);

            var le = cardGo.AddComponent<LayoutElement>();
            le.minHeight = cardH;
            le.preferredHeight = cardH;

            var bgImg = cardGo.AddComponent<Image>();
            bgImg.color = staff.IsOnStrike
                ? new Color(0.3f, 0.15f, 0.15f, 0.9f)
                : BgLight;
            bgImg.raycastTarget = true;

            // カラーバー（スタッフ種別）
            int typeIdx = System.Array.IndexOf(StaffTypes, staff.StaffType);
            Color typeColor = typeIdx >= 0 ? StaffTypeColors[typeIdx] : Color.gray;

            var colorBar = MakePanel(cardRt, "Color", 5f, cardH - 4f, typeColor);
            PlaceInParent(colorBar.GetComponent<RectTransform>(), 2f, -2f, 5f, cardH - 4f);

            // 名前 + 種別
            string typeName = typeIdx >= 0 ? StaffTypeNames[typeIdx] : "???";
            var nameText = MakeLabel(cardRt, "Name",
                $"{staff.Name}  [{typeName}]", 13,
                TextWhite, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(nameText.rectTransform, 12f, -2f, 250f, 20f);

            // ステータス
            string stateStr = GetStaffStateLabel(staff);
            Color stateColor = staff.IsOnStrike ? AccentRed : TextMuted;
            var stateText = MakeLabel(cardRt, "State", stateStr, 11,
                stateColor, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(stateText.rectTransform, 12f, -22f, 200f, 18f);

            // 疲労度バー
            float fatigue = Mathf.Clamp01(staff.Fatigue / 100f);
            var fatBg = MakePanel(cardRt, "FatBg", 60f, 8f, new Color(0.2f, 0.2f, 0.2f));
            PlaceInParent(fatBg.GetComponent<RectTransform>(), 260f, -28f, 60f, 8f);
            var fatFill = MakePanel(cardRt, "FatFill", 60f * fatigue, 8f,
                fatigue > 0.7f ? AccentRed : AccentGreen);
            PlaceInParent(fatFill.GetComponent<RectTransform>(), 260f, -28f, 60f * fatigue, 8f);

            var fatLabel = MakeLabel(cardRt, "FatLabel", $"疲労{staff.Fatigue:F0}%", 10,
                TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceInParent(fatLabel.rectTransform, 260f, -12f, 60f, 14f);

            // 解雇ボタン
            var fireGo = MakePanel(cardRt, "FireBtn", 50f, 28f, new Color(0.7f, 0.2f, 0.2f, 0.9f));
            var frt = fireGo.GetComponent<RectTransform>();
            frt.anchorMin = frt.anchorMax = new Vector2(1f, 0.5f);
            frt.pivot = new Vector2(1f, 0.5f);
            frt.anchoredPosition = new Vector2(-6f, 0f);
            var fireImg = fireGo.GetComponent<Image>();
            fireImg.raycastTarget = true;
            var fireBtn = fireGo.AddComponent<Button>();
            fireBtn.targetGraphic = fireImg;
            int capturedId = staff.Id;
            fireBtn.onClick.AddListener(() => OnFireClicked(capturedId));
            var fireLabel = MakeLabel(frt, "X", "解雇", 12,
                Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(fireLabel.rectTransform);

            return cardGo;
        }

        private void OnFireClicked(int staffId)
        {
            var sm = GameManager.Instance?.StaffManager;
            if (sm == null) return;

            if (sm.FireStaff(staffId))
            {
                NotificationSystem.Instance?.Notify("スタッフを解雇しました", NotifLevel.Info);
                RefreshStaffList();
            }
        }

        private void UpdateMoney()
        {
            if (_moneyText == null) return;
            float balance = GameManager.Instance?.EconomyManager?.CurrentBalance ?? 0f;
            _moneyText.text = $"${balance:N0}";
        }

        // ================================================================
        // ユーティリティ
        // ================================================================

        private static string StaffTypeNameFor(StaffType type)
        {
            switch (type)
            {
                case StaffType.Mechanic: return "メカニック";
                case StaffType.Cleaner: return "クリーナー";
                case StaffType.Entertainer: return "エンターテイナー";
                case StaffType.Guard: return "ガード";
                case StaffType.Scientist: return "サイエンティスト";
                default: return type.ToString();
            }
        }

        private static string GetStaffStateLabel(StaffMember staff)
        {
            if (staff.IsOnStrike)
                return "ストライキ中！";

            switch (staff.CurrentState)
            {
                case StaffBehaviorState.Idle: return "待機中";
                case StaffBehaviorState.Working: return "作業中";
                case StaffBehaviorState.Resting: return "休憩中";
                case StaffBehaviorState.MovingToTask: return "移動中";
                default: return staff.CurrentState.ToString();
            }
        }

        // ---- UI構築ヘルパー ----

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

        private static void PlaceInParent(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
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
    }
}
