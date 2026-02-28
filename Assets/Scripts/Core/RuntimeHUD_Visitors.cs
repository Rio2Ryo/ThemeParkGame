// ============================================================
// RuntimeHUD - Visitors partial
// 来場者状態パネル、来場者情報、フローティングスコア、
// ライフサイクル、ファーストパーソンビュー
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ThemeParkGame.AI;
using ThemeParkGame.Visitor;

namespace ThemeParkGame.Core
{
    public partial class RuntimeHUD
    {
        // ---- 来場者状態パネル ----
        private Text[] _visitorStatTexts;

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

        // 来場者状態カウント
        private int _walkingCount, _waitingCount, _ridingCount;
        private int _shoppingCount, _idleCount, _leavingCount;

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

        // ---- ライフサイクルフェーズ表示 ----
        private Text _lcWaitingText;
        private Text _lcEnjoyingText;
        private Text _lcLeavingText;
        private Image _lcWaitingBar;
        private Image _lcEnjoyingBar;
        private Image _lcLeavingBar;

        // ---- ファーストパーソンビュー ----
        private GameObject _fpvOverlay;
        private Text _fpvStatusText;
        private Text _fpvHappinessText;
        private Text _fpvPhaseText;
        private Text _fpvHintText;
        private Button _fpvExitButton;
        private Button _viFirstPersonButton;

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
        // 来場者個別情報パネル（画面中央左 280x450）
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

            // ファーストパーソンビュー「搭乗」ボタン + 「話しかける」ボタン
            y -= 8f;
            var fpBtnGo = MakePanel(rt, "FirstPersonBtn", 110f, 30f, new Color(0.2f, 0.5f, 0.7f));
            var fpBtnRt = fpBtnGo.GetComponent<RectTransform>();
            fpBtnRt.anchorMin = fpBtnRt.anchorMax = new Vector2(0.5f, 0f);
            fpBtnRt.pivot = new Vector2(1f, 1f);
            fpBtnRt.anchoredPosition = new Vector2(-4f, y);

            _viFirstPersonButton = fpBtnGo.AddComponent<Button>();
            _viFirstPersonButton.targetGraphic = fpBtnGo.GetComponent<Image>();
            var fpBtnColors = _viFirstPersonButton.colors;
            fpBtnColors.highlightedColor = new Color(0.25f, 0.6f, 0.82f);
            fpBtnColors.pressedColor = new Color(0.15f, 0.38f, 0.55f);
            _viFirstPersonButton.colors = fpBtnColors;

            var fpLabel = MakeLabel(fpBtnRt, "FPLabel", "搭乗", 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(fpLabel.rectTransform);

            _viFirstPersonButton.onClick.AddListener(OnFirstPersonButtonClicked);

            // 「話しかける」ボタン
            var talkBtnGo = MakePanel(rt, "TalkBtn", 110f, 30f, new Color(0.5f, 0.35f, 0.6f));
            var talkBtnRt = talkBtnGo.GetComponent<RectTransform>();
            talkBtnRt.anchorMin = talkBtnRt.anchorMax = new Vector2(0.5f, 0f);
            talkBtnRt.pivot = new Vector2(0f, 1f);
            talkBtnRt.anchoredPosition = new Vector2(4f, y);

            var talkImg = talkBtnGo.GetComponent<Image>();
            talkImg.raycastTarget = true;
            var talkBtn = talkBtnGo.AddComponent<Button>();
            talkBtn.targetGraphic = talkImg;
            var talkColors = talkBtn.colors;
            talkColors.highlightedColor = new Color(0.6f, 0.45f, 0.72f);
            talkColors.pressedColor = new Color(0.35f, 0.25f, 0.45f);
            talkBtn.colors = talkColors;
            talkBtn.onClick.AddListener(OnTalkToVisitorClicked);

            var talkLabel = MakeLabel(talkBtnRt, "TalkLabel", "話しかける", 13, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(talkLabel.rectTransform);

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
                // 来場者クリック
                var visitor = hit.collider.GetComponent<VisitorAI>();
                if (visitor != null && visitor.IsActive)
                {
                    HideFacilityInfo();
                    ShowVisitorInfo(visitor);
                    return;
                }

                // 施設クリック
                var facility = hit.collider.GetComponent<FacilityBase>();
                if (facility != null)
                {
                    HideVisitorInfo();
                    ShowFacilityInfo(facility);
                    return;
                }
            }

            HideVisitorInfo();
            HideFacilityInfo();
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
                _viRides.text += $"  お気に入り: {prof.FavoriteAttraction.Value.AttractionName}";
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
            label.font = FontManager.Regular;
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
        // 来場者状態更新
        // ================================================================

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

        private void OnTalkToVisitorClicked()
        {
            if (_selectedVisitor == null) return;
            int visitorId = _selectedVisitor.VisitorId;
            GameEvents.FireNPCConversationStarted(visitorId, "player_initiated");
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
    }
}
