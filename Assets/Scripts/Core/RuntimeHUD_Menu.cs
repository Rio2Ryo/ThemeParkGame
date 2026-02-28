// ============================================================
// ThemeParkGame - RuntimeHUD (Menu partial)
// メニュー、ポーズ、結果画面、シナリオ、セーブ/ロード、サウンド設定
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.AI;
using ThemeParkGame.Economy;
using ThemeParkGame.Park;

namespace ThemeParkGame.Core
{
    public partial class RuntimeHUD
    {
        // ---- メニュー/ポーズ/結果画面 ----
        private GameObject _menuBtn;
        private GameObject _pauseOverlay;
        private GameObject _resultsOverlay;
        private Text _resultsFinalScore;
        private Text _resultsBody;

        // ---- サウンド設定パネル ----
        private GameObject _soundPanel;
        private Slider _sliderMaster;
        private Slider _sliderBGM;
        private Slider _sliderSE;
        private Slider _sliderAmbient;

        // ---- シナリオ目標パネル ----
        private GameObject _scenarioPanel;
        private Text _scenarioTitle;
        private Text _scenarioObjectives;

        // ---- セーブ/ロードUI ----
        private GameObject _saveLoadPanel;
        private Text[] _slotTexts;
        private Text _saveLoadMessage;

        // ---- 通知バッジ ----
        private GameObject _notifBadge;
        private Text _notifBadgeText;

        // ---- 建設/スタッフ/研究/ローンボタン ----
        private GameObject _buildBtn;
        private GameObject _staffBtn;
        private GameObject _researchBtn;
        private GameObject _loanBtn;

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

            var label = MakeLabel(rt, "Label", LocalizationData.BtnMenu, 18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
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
        // 建設/スタッフ アクションボタン
        // ================================================================

        private void BuildActionButtons(RectTransform root)
        {
            // BUILD ボタン（MENUの右隣）
            _buildBtn = MakePanel(root, "BuildBtn", 90f, 36f, new Color(0.2f, 0.55f, 0.3f, 0.9f));
            var brt = _buildBtn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(110f, -10f);

            var bImg = _buildBtn.GetComponent<Image>();
            bImg.raycastTarget = true;
            var bBtn = _buildBtn.AddComponent<Button>();
            bBtn.targetGraphic = bImg;
            var bc = bBtn.colors;
            bc.highlightedColor = new Color(0.3f, 0.65f, 0.4f);
            bc.pressedColor = new Color(0.15f, 0.4f, 0.2f);
            bBtn.colors = bc;
            bBtn.onClick.AddListener(OnBuildClicked);

            var bLabel = MakeLabel(brt, "Label", LocalizationData.BtnBuild, 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(bLabel.rectTransform);

            // STAFF ボタン（BUILDの右隣）
            _staffBtn = MakePanel(root, "StaffBtn", 90f, 36f, new Color(0.4f, 0.3f, 0.55f, 0.9f));
            var srt = _staffBtn.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 1f);
            srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(210f, -10f);

            var sImg = _staffBtn.GetComponent<Image>();
            sImg.raycastTarget = true;
            var sBtn = _staffBtn.AddComponent<Button>();
            sBtn.targetGraphic = sImg;
            var sc = sBtn.colors;
            sc.highlightedColor = new Color(0.5f, 0.4f, 0.65f);
            sc.pressedColor = new Color(0.3f, 0.2f, 0.4f);
            sBtn.colors = sc;
            sBtn.onClick.AddListener(OnStaffClicked);

            var sLabel = MakeLabel(srt, "Label", LocalizationData.BtnStaff, 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(sLabel.rectTransform);

            // RESEARCH ボタン（STAFFの右隣）
            _researchBtn = MakePanel(root, "ResearchBtn", 100f, 36f, new Color(0.55f, 0.45f, 0.2f, 0.9f));
            var rrt = _researchBtn.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 1f);
            rrt.anchoredPosition = new Vector2(310f, -10f);

            var rImg = _researchBtn.GetComponent<Image>();
            rImg.raycastTarget = true;
            var rBtn = _researchBtn.AddComponent<Button>();
            rBtn.targetGraphic = rImg;
            var rc = rBtn.colors;
            rc.highlightedColor = new Color(0.65f, 0.55f, 0.3f);
            rc.pressedColor = new Color(0.4f, 0.32f, 0.15f);
            rBtn.colors = rc;
            rBtn.onClick.AddListener(OnResearchClicked);

            var rLabel = MakeLabel(rrt, "Label", LocalizationData.BtnResearch, 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(rLabel.rectTransform);

            // LOAN ボタン（RESEARCHの右隣）
            _loanBtn = MakePanel(root, "LoanBtn", 80f, 36f, new Color(0.2f, 0.45f, 0.55f, 0.9f));
            var lrt = _loanBtn.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 1f);
            lrt.anchoredPosition = new Vector2(420f, -10f);

            var lImg = _loanBtn.GetComponent<Image>();
            lImg.raycastTarget = true;
            var lBtn = _loanBtn.AddComponent<Button>();
            lBtn.targetGraphic = lImg;
            var lc = lBtn.colors;
            lc.highlightedColor = new Color(0.3f, 0.55f, 0.65f);
            lc.pressedColor = new Color(0.15f, 0.32f, 0.4f);
            lBtn.colors = lc;
            lBtn.onClick.AddListener(OnLoanClicked);

            var lLabel = MakeLabel(lrt, "Label", LocalizationData.BtnLoan, 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(lLabel.rectTransform);
        }

        private void OnBuildClicked()
        {
            // RuntimeBuildPanelがなければ追加
            var bp = RuntimeBuildPanel.Instance;
            if (bp == null)
            {
                bp = gameObject.AddComponent<RuntimeBuildPanel>();
            }
            bp.Toggle();
        }

        private void OnStaffClicked()
        {
            // RuntimeStaffPanelがなければ追加
            var sp = RuntimeStaffPanel.Instance;
            if (sp == null)
            {
                sp = gameObject.AddComponent<RuntimeStaffPanel>();
            }
            sp.Toggle();
        }

        private void OnResearchClicked()
        {
            if (_researchPanel == null) return;
            bool show = !_researchPanel.activeSelf;
            _researchPanel.SetActive(show);
            if (show) RefreshResearchPanel();
        }

        private void OnLoanClicked()
        {
            if (_loanPanel == null) return;
            bool show = !_loanPanel.activeSelf;
            _loanPanel.SetActive(show);
            if (show) RefreshLoanPanel();
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
            var title = MakeLabel(rt, "PauseTitle", LocalizationData.PauseTitle, 56, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, 240f);
            titleRt.sizeDelta = new Vector2(400f, 70f);

            // 「続ける」ボタン
            MakeCenterButton(rt, "ResumeBtn", "続ける",
                new Color(0.18f, 0.55f, 0.34f), new Color(0.22f, 0.65f, 0.40f), new Color(0.14f, 0.45f, 0.28f),
                new Vector2(0f, 270f), OnResumeClicked);

            // 「セーブ/ロード」ボタン
            MakeCenterButton(rt, "SaveLoadBtn", "セーブ / ロード",
                new Color(0.3f, 0.4f, 0.6f), new Color(0.38f, 0.5f, 0.72f), new Color(0.22f, 0.3f, 0.48f),
                new Vector2(0f, 215f), OnSaveLoadClicked);

            // 「クラウドセーブ」ボタン
            MakeCenterButton(rt, "CloudSaveBtn", "クラウドセーブ",
                new Color(0.2f, 0.4f, 0.6f), new Color(0.28f, 0.5f, 0.72f), new Color(0.15f, 0.3f, 0.48f),
                new Vector2(0f, 160f), OnCloudSaveClicked);

            // 「リーダーボード」ボタン
            MakeCenterButton(rt, "LeaderboardBtn", "ランキング",
                new Color(0.55f, 0.45f, 0.15f), new Color(0.65f, 0.55f, 0.22f), new Color(0.42f, 0.34f, 0.1f),
                new Vector2(0f, 105f), OnLeaderboardClicked);

            // 「チャレンジ」ボタン
            MakeCenterButton(rt, "ChallengeBtn", LocalizationData.BtnChallenges,
                new Color(0.6f, 0.35f, 0.15f), new Color(0.72f, 0.45f, 0.22f), new Color(0.48f, 0.28f, 0.1f),
                new Vector2(0f, 50f), OnChallengeClicked);

            // 「パーク拡張」ボタン
            MakeCenterButton(rt, "ExpansionBtn", "パーク拡張",
                new Color(0.2f, 0.5f, 0.3f), new Color(0.28f, 0.6f, 0.38f), new Color(0.15f, 0.4f, 0.22f),
                new Vector2(0f, -5f), OnExpansionClicked);

            // 「融資/投資」ボタン
            MakeCenterButton(rt, "FinanceBtn", "融資/投資",
                new Color(0.45f, 0.4f, 0.2f), new Color(0.55f, 0.5f, 0.28f), new Color(0.35f, 0.3f, 0.15f),
                new Vector2(0f, -60f), OnFinanceClicked);

            // 「Co-op」ボタン
            MakeCenterButton(rt, "CoopBtn", "協力プレイ",
                new Color(0.35f, 0.2f, 0.55f), new Color(0.45f, 0.28f, 0.65f), new Color(0.25f, 0.15f, 0.42f),
                new Vector2(0f, -115f), OnCoopClicked);

            // Phase 9: 「天気予報」ボタン
            MakeCenterButton(rt, "WeatherBtn", "天気予報",
                new Color(0.2f, 0.45f, 0.6f), new Color(0.28f, 0.55f, 0.72f), new Color(0.15f, 0.35f, 0.48f),
                new Vector2(0f, -170f), OnWeatherClicked);

            // Phase 9: 「イベント」ボタン
            MakeCenterButton(rt, "EventsBtn", "イベント",
                new Color(0.55f, 0.3f, 0.5f), new Color(0.65f, 0.38f, 0.6f), new Color(0.42f, 0.22f, 0.38f),
                new Vector2(0f, -225f), OnEventsClicked);

            // Phase 9: 「シェア」ボタン
            MakeCenterButton(rt, "ShareBtn", "シェア",
                new Color(0.15f, 0.15f, 0.2f), new Color(0.25f, 0.25f, 0.32f), new Color(0.1f, 0.1f, 0.15f),
                new Vector2(0f, -280f), OnShareClicked);

            // Phase 9: 「アクセシビリティ」ボタン
            MakeCenterButton(rt, "AccessibilityBtn", "アクセシビリティ",
                new Color(0.3f, 0.5f, 0.45f), new Color(0.38f, 0.6f, 0.55f), new Color(0.22f, 0.4f, 0.35f),
                new Vector2(0f, -335f), OnAccessibilityClicked);

            // Phase 10: 「アクシデント」ボタン
            MakeCenterButton(rt, "AccidentsBtn", LocalizationData.BtnAccidents,
                new Color(0.65f, 0.25f, 0.15f), new Color(0.75f, 0.35f, 0.22f), new Color(0.52f, 0.18f, 0.1f),
                new Vector2(0f, -390f), OnAccidentsClicked);

            // Phase 10: 「口コミ」ボタン
            MakeCenterButton(rt, "ReviewsBtn", LocalizationData.BtnReviews,
                new Color(0.2f, 0.5f, 0.55f), new Color(0.28f, 0.6f, 0.65f), new Color(0.15f, 0.4f, 0.44f),
                new Vector2(0f, -445f), OnReviewsClicked);

            // Phase 10: 「ライバル」ボタン
            MakeCenterButton(rt, "RivalsBtn", LocalizationData.BtnRivals,
                new Color(0.55f, 0.2f, 0.45f), new Color(0.65f, 0.28f, 0.55f), new Color(0.42f, 0.15f, 0.35f),
                new Vector2(0f, -500f), OnRivalsClicked);

            // Phase 10: 「セール」ボタン
            MakeCenterButton(rt, "SalesBtn", LocalizationData.BtnSales,
                new Color(0.6f, 0.5f, 0.15f), new Color(0.72f, 0.6f, 0.22f), new Color(0.48f, 0.38f, 0.1f),
                new Vector2(0f, -555f), OnSalesClicked);

            // 「実績」ボタン
            MakeCenterButton(rt, "AchievementBtn", LocalizationData.BtnAchievements,
                new Color(0.55f, 0.45f, 0.2f), new Color(0.65f, 0.55f, 0.28f), new Color(0.42f, 0.34f, 0.15f),
                new Vector2(0f, -610f), OnAchievementClicked);

            // 「サウンド設定」ボタン
            MakeCenterButton(rt, "SoundBtn", LocalizationData.BtnSoundSettings,
                new Color(0.35f, 0.4f, 0.52f), new Color(0.45f, 0.5f, 0.62f), new Color(0.25f, 0.3f, 0.42f),
                new Vector2(0f, -665f), OnSoundSettingsClicked);

            // 「イベントログ」ボタン
            MakeCenterButton(rt, "EventLogBtn", LocalizationData.BtnEventLog,
                new Color(0.3f, 0.45f, 0.55f), new Color(0.38f, 0.55f, 0.65f), new Color(0.22f, 0.35f, 0.44f),
                new Vector2(0f, -720f), OnEventLogClicked);

            // 「ゲーム終了」ボタン
            MakeCenterButton(rt, "EndGameBtn", "ゲーム終了",
                new Color(0.65f, 0.2f, 0.2f), new Color(0.75f, 0.3f, 0.3f), new Color(0.5f, 0.15f, 0.15f),
                new Vector2(0f, -775f), OnEndGameClicked);

            // サウンド設定パネル（初期非表示）
            BuildSoundSettingsPanel(rt);

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
            var title = MakeLabel(rt, "ResultsTitle", LocalizationData.ResultTitle, 60, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, 280f);
            titleRt.sizeDelta = new Vector2(600f, 70f);

            // サブタイトル
            var sub = MakeLabel(rt, "ResultsSub", "- 最終スコアレポート -", 22, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            var subRt = sub.rectTransform;
            subRt.anchorMin = subRt.anchorMax = new Vector2(0.5f, 0.5f);
            subRt.anchoredPosition = new Vector2(0f, 230f);
            subRt.sizeDelta = new Vector2(400f, 30f);

            // ---- 最終スコア（巨大表示） ----
            var scoreLbl = MakeLabel(rt, "ScoreLabel", LocalizationData.ResultFinalScore, 18, new Color(0.5f, 0.6f, 0.7f),
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
            MakeCenterButton(rt, "RestartBtn", LocalizationData.BtnRestart,
                new Color(0.18f, 0.55f, 0.34f), new Color(0.22f, 0.65f, 0.40f), new Color(0.14f, 0.45f, 0.28f),
                new Vector2(0f, -175f), OnRestartClicked);

            // ---- メインメニューに戻るボタン ----
            MakeCenterButton(rt, "ReturnMenuBtn", LocalizationData.BtnBackToTitle,
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

            _scenarioTitle = MakeLabel(rt, "ScTitle", "シナリオ", 16, Gold,
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
            var title = MakeLabel(rt, "SLTitle", "セーブ / ロード", 28, Gold,
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
            var closeLabel = MakeLabel(closeRt, "Label", LocalizationData.BtnClose, 18, Color.white,
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
            var slotLabel = MakeLabel(infoRt, "SlotNum", $"スロット {slot + 1}", 14, Muted,
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
            var saveTxt = MakeLabel(saveRt, "L", LocalizationData.BtnSave, 14, Color.white,
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
            var loadTxt = MakeLabel(loadRt, "L", LocalizationData.BtnLoad, 14, Color.white,
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
                    ? $"スロット {slot + 1} にセーブしました"
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
                    _saveLoadMessage.text = $"スロット {slot + 1} にデータがありません";
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
            if (_soundPanel != null) _soundPanel.SetActive(false);
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

        private void OnLeaderboardClicked()
        {
            if (LeaderboardManager.Instance != null)
                LeaderboardManager.Instance.FetchAndShowLeaderboard();
        }

        private void OnCloudSaveClicked()
        {
            if (CloudSaveManager.Instance != null)
                CloudSaveManager.Instance.ShowCloudSaveUI();
        }

        private void OnChallengeClicked()
        {
            if (ChallengeSystem.Instance != null)
                ChallengeSystem.Instance.ToggleUI();
        }

        private void OnExpansionClicked()
        {
            if (Park.ParkExpansionSystem.Instance != null)
                Park.ParkExpansionSystem.Instance.ToggleUI();
        }

        private void OnFinanceClicked()
        {
            if (Economy.LoanInvestmentUI.Instance != null)
                Economy.LoanInvestmentUI.Instance.ToggleUI();
        }

        private void OnCoopClicked()
        {
            if (CoopManager.Instance != null)
                CoopManager.Instance.ToggleUI();
        }

        private void OnWeatherClicked()
        {
            if (UI.WeatherForecastUI.Instance != null)
                UI.WeatherForecastUI.Instance.ToggleUI();
        }

        private void OnEventsClicked()
        {
            if (UI.SpecialEventUI.Instance != null)
                UI.SpecialEventUI.Instance.ToggleUI();
        }

        private void OnShareClicked()
        {
            if (SocialShareSystem.Instance != null)
                SocialShareSystem.Instance.ToggleUI();
        }

        private void OnAccessibilityClicked()
        {
            if (AccessibilitySystem.Instance != null)
                AccessibilitySystem.Instance.ToggleUI();
        }

        private void OnAccidentsClicked()
        {
            if (AccidentEventSystem.Instance != null)
                AccidentEventSystem.Instance.ToggleUI();
        }

        private void OnReviewsClicked()
        {
            if (AI.WordOfMouthSystem.Instance != null)
                AI.WordOfMouthSystem.Instance.ToggleUI();
        }

        private void OnRivalsClicked()
        {
            if (Park.RivalParkSystem.Instance != null)
                Park.RivalParkSystem.Instance.ToggleUI();
        }

        private void OnSalesClicked()
        {
            if (Economy.SaleCampaignSystem.Instance != null)
                Economy.SaleCampaignSystem.Instance.ToggleUI();
        }

        private void OnSoundSettingsClicked()
        {
            if (_soundPanel != null)
                _soundPanel.SetActive(!_soundPanel.activeSelf);
        }

        private void BuildSoundSettingsPanel(RectTransform pauseRoot)
        {
            float panelW = 340f;
            float panelH = 260f;

            _soundPanel = new GameObject("SoundPanel");
            _soundPanel.transform.SetParent(pauseRoot, false);
            var pRt = _soundPanel.AddComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.anchoredPosition = new Vector2(380f, 0f);
            pRt.sizeDelta = new Vector2(panelW, panelH);

            var bgImg = _soundPanel.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.08f, 0.16f, 0.96f);
            bgImg.raycastTarget = true;

            // タイトル
            var title = MakeLabel(pRt, "SndTitle", LocalizationData.SoundTitle, 20,
                new Color(0.95f, 0.88f, 0.45f), FontStyle.Bold, TextAnchor.MiddleCenter);
            var tRt = title.rectTransform;
            tRt.anchorMin = tRt.anchorMax = new Vector2(0.5f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0f, -8f);
            tRt.sizeDelta = new Vector2(panelW, 30f);

            float y = -44f;
            _sliderMaster = MakeSoundSlider(pRt, "Master", LocalizationData.LabelMasterVolume, y, 1f);
            y -= 50f;
            _sliderBGM = MakeSoundSlider(pRt, "BGM", LocalizationData.LabelBGMVolume, y, 0.5f);
            y -= 50f;
            _sliderSE = MakeSoundSlider(pRt, "SE", LocalizationData.LabelSEVolume, y, 0.8f);
            y -= 50f;
            _sliderAmbient = MakeSoundSlider(pRt, "Ambient", LocalizationData.LabelAmbientVolume, y, 0.3f);

            // AudioManagerから現在値を取得
            if (AudioManager.Instance != null)
            {
                _sliderMaster.value = AudioManager.Instance.MasterVolume;
                _sliderBGM.value = AudioManager.Instance.BGMVolume;
                _sliderSE.value = AudioManager.Instance.SEVolume;
                _sliderAmbient.value = AudioManager.Instance.AmbientVolume;
            }

            // リスナー登録
            _sliderMaster.onValueChanged.AddListener(v => {
                if (AudioManager.Instance != null) AudioManager.Instance.MasterVolume = v;
            });
            _sliderBGM.onValueChanged.AddListener(v => {
                if (AudioManager.Instance != null) AudioManager.Instance.BGMVolume = v;
            });
            _sliderSE.onValueChanged.AddListener(v => {
                if (AudioManager.Instance != null) AudioManager.Instance.SEVolume = v;
            });
            _sliderAmbient.onValueChanged.AddListener(v => {
                if (AudioManager.Instance != null) AudioManager.Instance.AmbientVolume = v;
            });

            _soundPanel.SetActive(false);
        }

        private Slider MakeSoundSlider(RectTransform parent, string id, string label, float yPos, float defaultVal)
        {
            var row = new GameObject($"Snd_{id}");
            row.transform.SetParent(parent, false);
            var rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, yPos);
            rowRt.sizeDelta = new Vector2(300f, 44f);

            // ラベル
            var lbl = MakeLabel(rowRt, "Label", label, 14, new Color(0.8f, 0.82f, 0.9f),
                FontStyle.Bold, TextAnchor.MiddleLeft);
            var lblRt = lbl.rectTransform;
            lblRt.anchorMin = lblRt.anchorMax = new Vector2(0f, 1f);
            lblRt.pivot = new Vector2(0f, 1f);
            lblRt.anchoredPosition = new Vector2(0f, 0f);
            lblRt.sizeDelta = new Vector2(80f, 20f);

            // スライダー
            var sliderGo = new GameObject($"Slider_{id}");
            sliderGo.transform.SetParent(rowRt, false);
            var sliderRt = sliderGo.AddComponent<RectTransform>();
            sliderRt.anchorMin = sliderRt.anchorMax = new Vector2(0.5f, 0f);
            sliderRt.pivot = new Vector2(0.5f, 0f);
            sliderRt.anchoredPosition = new Vector2(10f, 2f);
            sliderRt.sizeDelta = new Vector2(290f, 20f);

            // スライダー背景
            var bgGo = MakePanel(sliderRt, "Background", 290f, 8f, new Color(0.15f, 0.18f, 0.28f));
            var bgRtS = bgGo.GetComponent<RectTransform>();
            bgRtS.anchorMin = new Vector2(0f, 0.5f);
            bgRtS.anchorMax = new Vector2(1f, 0.5f);
            bgRtS.offsetMin = new Vector2(0f, -4f);
            bgRtS.offsetMax = new Vector2(0f, 4f);

            // Fill領域
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderRt, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.offsetMin = new Vector2(5f, 0f);
            fillAreaRt.offsetMax = new Vector2(-5f, 0f);

            var fill = MakePanel(fillAreaRt, "Fill", 0f, 0f, new Color(0.35f, 0.65f, 0.9f));
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().raycastTarget = false;

            // ハンドル領域
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderRt, false);
            var handleAreaRt = handleArea.AddComponent<RectTransform>();
            handleAreaRt.anchorMin = new Vector2(0f, 0f);
            handleAreaRt.anchorMax = new Vector2(1f, 1f);
            handleAreaRt.offsetMin = new Vector2(5f, 0f);
            handleAreaRt.offsetMax = new Vector2(-5f, 0f);

            var handle = MakePanel(handleAreaRt, "Handle", 16f, 16f, Color.white);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(16f, 16f);
            handle.GetComponent<Image>().raycastTarget = true;

            // Sliderコンポーネント
            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = defaultVal;
            slider.wholeNumbers = false;

            return slider;
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
        /// スコアを計算する（リバランス済み）。
        /// 基本スコア = 来場者×15 + 収益/50 + 満足度×30 + パーク評価×20 + ゴールデンチケット×300
        /// 最終スコア = 基本スコア × 難易度倍率(Easy:0.8 Normal:1.0 Hard:1.5)
        /// </summary>
        private int CalculateFinalScore()
        {
            var gm = GameManager.Instance;
            if (gm == null) return 0;

            int visitorScore = (gm.VisitorManager != null) ? gm.VisitorManager.TotalVisitorsToday * 15 : 0;
            int revenueScore = (gm.EconomyManager != null) ? (int)(gm.EconomyManager.TotalRevenueEarned / 50f) : 0;
            int satisfactionScore = (gm.VisitorManager != null) ? (int)(gm.VisitorManager.AverageHappiness * 30f) : 0;
            int ratingScore = (gm.ParkManager?.Rating != null) ? (int)(gm.ParkManager.Rating.OverallRating * 20f) : 0;
            int ticketScore = gm.GoldenTickets * 300;

            int baseScore = visitorScore + revenueScore + satisfactionScore + ratingScore + ticketScore;

            // 難易度倍率適用
            float difficultyMult = GameManager.GetScoreMultiplier(gm.CurrentDifficulty);
            return (int)(baseScore * difficultyMult);
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
                    _resultsFinalScore.text = "クリア！";
                else if (isScenario && ScenarioManager.Instance.IsScenarioFailed)
                    _resultsFinalScore.text = "失敗";
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
                time = $"{tm.CurrentYear}年{tm.CurrentMonth}月{tm.CurrentDay}日";
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
                lcText = $"  ライフサイクル: 楽しさ {enjoyRatio:F0}%  開始:{expStarts}  完了:{expDone}\n";
            }

            // パーク評価
            string ratingText = "";
            if (gm.ParkManager != null && gm.ParkManager.Rating != null)
            {
                float overall = gm.ParkManager.Rating.OverallRating;
                float stars = ParkRatingEvaluator.ScoreToStars(overall);
                string starsStr = ParkRatingEvaluator.StarsToText(stars);
                string label = ParkRatingEvaluator.GetRatingLabel(overall);
                ratingText = $"  パーク評価: [{starsStr}] {overall:F1} - {label}\n";
            }

            // 通知・アラート統計
            string alertText = "";
            if (NotificationSystem.Instance != null)
            {
                int logCount = NotificationSystem.Instance.Log.Count;
                int alertCount = AlertMonitor.Instance != null ? AlertMonitor.Instance.TotalAlertCount : 0;
                alertText = $"  通知: {logCount}  アクティブアラート: {alertCount}\n";
            }

            // SNSレピュテーション
            string snsText = "";
            if (gm.AIManager?.SNSSystem != null)
            {
                float rep = gm.AIManager.SNSSystem.Reputation;
                int posts = gm.AIManager.SNSSystem.Feed.Count;
                float spawn = gm.AIManager.SNSSystem.VisitorSpawnMultiplier;
                snsText = $"  SNS評判: {rep:F0}/100  投稿数: {posts}  来場倍率: x{spawn:F2}\n";
            }

            _resultsBody.text =
                $"  来場者数: {visitors}  (ピーク: {peak})\n" +
                $"  満足度: {avgSatisfaction:F0}%  幸福度: {avgHappiness:F0}%\n" +
                ratingText +
                lcText +
                snsText +
                alertText +
                $"  収入: {revenue}         支出: {expenses}\n" +
                $"  最終残高: {money}\n" +
                $"  アトラクション数: {attrCount}         ゴールデンチケット: {tickets}\n" +
                $"  日付: {time}         実績: {achText}\n" +
                $"\n" +
                $"  スコア内訳:\n" +
                $"    来場者 x15 = {(gm.VisitorManager != null ? gm.VisitorManager.TotalVisitorsToday * 15 : 0):N0}\n" +
                $"    収益 / 50 = {(gm.EconomyManager != null ? (int)(gm.EconomyManager.TotalRevenueEarned / 50f) : 0):N0}\n" +
                $"    満足度 x30 = {(int)(avgSatisfaction * 30f):N0}\n" +
                $"    パーク評価 x20 = {(gm.ParkManager?.Rating != null ? (int)(gm.ParkManager.Rating.OverallRating * 20f) : 0):N0}\n" +
                $"    ゴールデンチケット x300 = {gm.GoldenTickets * 300:N0}\n" +
                $"    難易度倍率: x{GameManager.GetScoreMultiplier(gm.CurrentDifficulty):F1} ({gm.CurrentDifficulty})";
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

            _scenarioTitle.text = $"シナリオ: {ScenarioManager.GetCountryName(scenario.Country)}";

            string objectives = sm.GetObjectiveProgressText();
            if (sm.IsScenarioCleared)
                objectives += "\n*** シナリオクリア！ ***";
            else if (sm.IsScenarioFailed)
                objectives += "\n*** タイムオーバー ***";

            if (scenario.TimeLimitYears > 0 && GameManager.Instance.TimeManager != null)
            {
                int yr = GameManager.Instance.TimeManager.CurrentYear;
                objectives += $"\n制限: {yr}年目/{scenario.TimeLimitYears}年";
            }

            _scenarioObjectives.text = objectives;
        }
    }
}
