// ============================================================
// RuntimeHUD - Economy partial
// 研究パネル、ローンパネル、月次レポート
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Economy;

namespace ThemeParkGame.Core
{
    public partial class RuntimeHUD
    {
        // ---- 研究パネル ----
        private GameObject _researchPanel;
        private Text _researchTitle;
        private Text _researchScientistInfo;
        private Text _researchCurrentText;
        private Image _researchProgressFill;
        private Text _researchProgressText;
        private readonly List<GameObject> _researchItemRows = new List<GameObject>();
        private readonly List<Text> _researchItemTexts = new List<Text>();
        private readonly List<Button> _researchItemButtons = new List<Button>();
        private RectTransform _researchListContainer;

        // ---- ローンパネル ----
        private GameObject _loanPanel;
        private Text _loanBalanceText;
        private Text _loanCountText;
        private Text _loanDetailText;
        private Text _loanBorrowAmountText;
        private float _loanBorrowAmount = 10000f;
        private string _lastLoanState;
        private Text _entranceFeeText;

        // ---- 月次レポートポップアップ ----
        private GameObject _monthlyReportPanel;
        private Text _mrTitle;
        private Text _mrRevenue;
        private Text _mrExpenses;
        private Text _mrProfit;
        private Text _mrVisitors;
        private Text _mrDetails;
        private float _monthlyReportAutoClose;
        private int _lastReportMonth = -1;

        // ================================================================
        // 研究パネル
        // ================================================================

        private void BuildResearchPanel(RectTransform root)
        {
            float panelW = 480f;
            float panelH = 520f;

            _researchPanel = new GameObject("ResearchPanel");
            _researchPanel.transform.SetParent(root, false);
            var rt = _researchPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(panelW, panelH);

            var bgImg = _researchPanel.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.08f, 0.14f, 0.97f);
            bgImg.raycastTarget = true;

            // タイトル
            _researchTitle = MakeLabel(rt, "Title", LocalizationData.ResearchTitle, 26, Gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = _researchTitle.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -8f);
            titleRt.sizeDelta = new Vector2(panelW, 32f);

            // サイエンティスト情報
            _researchScientistInfo = MakeLabel(rt, "SciInfo", "サイエンティスト: 0  スキル: --", 14, Muted,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            var sciRt = _researchScientistInfo.rectTransform;
            sciRt.anchorMin = sciRt.anchorMax = new Vector2(0.5f, 1f);
            sciRt.pivot = new Vector2(0.5f, 1f);
            sciRt.anchoredPosition = new Vector2(0f, -42f);
            sciRt.sizeDelta = new Vector2(panelW - 20f, 20f);

            // 現在の研究表示
            var curBg = MakePanel(rt, "CurrentBg", panelW - 20f, 60f, new Color(0.1f, 0.12f, 0.2f, 0.9f));
            var curRt = curBg.GetComponent<RectTransform>();
            curRt.anchorMin = curRt.anchorMax = new Vector2(0.5f, 1f);
            curRt.pivot = new Vector2(0.5f, 1f);
            curRt.anchoredPosition = new Vector2(0f, -66f);

            _researchCurrentText = MakeLabel(curRt, "CurText", "研究中: なし", 15, Color.white,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceInParent(_researchCurrentText.rectTransform, 10f, 58f, panelW - 40f, 24f, new Vector2(0f, 1f));

            // プログレスバー
            var barBg = MakePanel(curRt, "BarBg", panelW - 40f, 14f, new Color(0.15f, 0.17f, 0.25f));
            var barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.anchorMin = barBgRt.anchorMax = new Vector2(0.5f, 0f);
            barBgRt.pivot = new Vector2(0.5f, 0f);
            barBgRt.anchoredPosition = new Vector2(0f, 6f);

            var fillGo = MakePanel(barBgRt, "Fill", 0f, 14f, new Color(0.45f, 0.75f, 0.3f));
            _researchProgressFill = fillGo.GetComponent<Image>();
            var fillRt = _researchProgressFill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, 0f);

            _researchProgressText = MakeLabel(curRt, "ProgText", "", 12, Muted,
                FontStyle.Normal, TextAnchor.MiddleRight);
            PlaceInParent(_researchProgressText.rectTransform, panelW - 110f, 58f, 90f, 24f, new Vector2(0f, 1f));

            // 区切り線
            var sepLabel = MakeLabel(rt, "SepLabel", "研究可能な項目", 13, Muted,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            var sepLabelRt = sepLabel.rectTransform;
            sepLabelRt.anchorMin = sepLabelRt.anchorMax = new Vector2(0f, 1f);
            sepLabelRt.pivot = new Vector2(0f, 1f);
            sepLabelRt.anchoredPosition = new Vector2(12f, -132f);
            sepLabelRt.sizeDelta = new Vector2(panelW - 24f, 20f);

            // 研究リストコンテナ
            var listGo = new GameObject("ResearchList");
            listGo.transform.SetParent(rt, false);
            _researchListContainer = listGo.AddComponent<RectTransform>();
            _researchListContainer.anchorMin = new Vector2(0f, 0f);
            _researchListContainer.anchorMax = new Vector2(1f, 1f);
            _researchListContainer.offsetMin = new Vector2(10f, 46f);
            _researchListContainer.offsetMax = new Vector2(-10f, -156f);

            // 閉じるボタン
            var closeGo = MakePanel(rt, "CloseBtn", 120f, 34f, new Color(0.4f, 0.42f, 0.5f));
            var closeGoRt = closeGo.GetComponent<RectTransform>();
            closeGoRt.anchorMin = closeGoRt.anchorMax = new Vector2(0.5f, 0f);
            closeGoRt.pivot = new Vector2(0.5f, 0f);
            closeGoRt.anchoredPosition = new Vector2(0f, 8f);
            var closeImg2 = closeGo.GetComponent<Image>();
            closeImg2.raycastTarget = true;
            var closeBtn2 = closeGo.AddComponent<Button>();
            closeBtn2.targetGraphic = closeImg2;
            closeBtn2.onClick.AddListener(() => _researchPanel.SetActive(false));
            var closeLbl = MakeLabel(closeGoRt, "Label", LocalizationData.BtnClose, 16, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLbl.rectTransform);

            _researchPanel.SetActive(false);
        }

        private void RefreshResearchPanel()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ResearchManager == null) return;
            var rm = gm.ResearchManager;

            // サイエンティスト情報
            int sciCount = rm.ScientistCount;
            float avgSkill = rm.AverageScientistSkill;
            _researchScientistInfo.text = sciCount > 0
                ? $"サイエンティスト: {sciCount}   平均スキル: {avgSkill:P0}"
                : "サイエンティスト: 0  (雇用するとスタッフで研究が進みます)";

            // 現在の研究
            if (rm.IsResearching)
            {
                var cur = rm.CurrentResearch;
                _researchCurrentText.text = $"研究中: {cur.NameJP}";
                _researchProgressText.text = $"{cur.ProgressRatio:P0}";

                // プログレスバー
                float barW = 440f;
                _researchProgressFill.rectTransform.sizeDelta = new Vector2(barW * cur.ProgressRatio, 0f);
            }
            else
            {
                _researchCurrentText.text = "研究中: なし";
                _researchProgressText.text = "";
                _researchProgressFill.rectTransform.sizeDelta = new Vector2(0f, 0f);
            }

            // 研究リストの再構築
            foreach (var row in _researchItemRows)
                Destroy(row);
            _researchItemRows.Clear();
            _researchItemTexts.Clear();
            _researchItemButtons.Clear();

            var available = rm.GetAvailableResearch();
            float rowH = 50f;
            float listW = _researchListContainer.rect.width;
            if (listW <= 0f) listW = 450f;

            for (int i = 0; i < available.Count && i < 7; i++)
            {
                var item = available[i];
                float y = -i * (rowH + 4f);

                var rowGo = MakePanel(_researchListContainer, $"Row{i}", listW, rowH,
                    new Color(0.1f, 0.12f, 0.2f, 0.85f));
                var rowRt = rowGo.GetComponent<RectTransform>();
                rowRt.anchorMin = rowRt.anchorMax = new Vector2(0f, 1f);
                rowRt.pivot = new Vector2(0f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, y);

                // 研究名
                var nameText = MakeLabel(rowRt, "Name", item.NameJP, 14, Color.white,
                    FontStyle.Bold, TextAnchor.MiddleLeft);
                PlaceInParent(nameText.rectTransform, 8f, rowH - 2f, listW - 120f, 22f, new Vector2(0f, 1f));

                // コスト + カテゴリ
                string catLabel = item.Category == ResearchCategory.Attractions ? "アトラクション"
                    : item.Category == ResearchCategory.Shops ? "ショップ"
                    : item.Category == ResearchCategory.Upgrades ? "アップグレード" : "施設";
                var descText = MakeLabel(rowRt, "Desc", $"${item.ResearchCost:N0}  [{catLabel}]  {item.BaseResearchTime:F0}s",
                    11, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
                PlaceInParent(descText.rectTransform, 8f, rowH - 24f, listW - 120f, 18f, new Vector2(0f, 1f));

                // 開始ボタン
                bool canStart = !rm.IsResearching;
                bool canAfford = gm.EconomyManager != null && gm.EconomyManager.CanAfford(item.ResearchCost);
                bool enabled = canStart && canAfford && sciCount > 0;

                var btnGo = MakePanel(rowRt, "StartBtn", 80f, 30f,
                    enabled ? new Color(0.2f, 0.5f, 0.35f) : new Color(0.25f, 0.27f, 0.32f));
                var btnRt = btnGo.GetComponent<RectTransform>();
                btnRt.anchorMin = btnRt.anchorMax = new Vector2(1f, 0.5f);
                btnRt.pivot = new Vector2(1f, 0.5f);
                btnRt.anchoredPosition = new Vector2(-6f, 0f);
                var btnImg = btnGo.GetComponent<Image>();
                btnImg.raycastTarget = true;
                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = btnImg;
                btn.interactable = enabled;

                string resId = item.ResearchId;
                btn.onClick.AddListener(() =>
                {
                    if (gm.ResearchManager != null)
                    {
                        gm.ResearchManager.StartResearch(resId);
                        RefreshResearchPanel();
                    }
                });

                string btnLabel = !canStart ? "研究中" : sciCount <= 0 ? "人員不足" : !canAfford ? "資金不足" : "開始";
                var btnText = MakeLabel(btnRt, "BtnLabel", btnLabel, 13,
                    enabled ? Color.white : Muted, FontStyle.Bold, TextAnchor.MiddleCenter);
                StretchFill(btnText.rectTransform);

                _researchItemRows.Add(rowGo);
                _researchItemTexts.Add(nameText);
                _researchItemButtons.Add(btn);
            }

            // 完了済み研究の概要
            int completedCount = rm.CompletedResearchCount;
            int totalCount = rm.AllResearch.Count;
            if (completedCount > 0 || available.Count == 0)
            {
                float y = -(available.Count) * (rowH + 4f) - 8f;
                var summaryText = MakeLabel(_researchListContainer, "Summary",
                    $"完了: {completedCount}/{totalCount}  投資額: ${rm.TotalResearchSpending:N0}",
                    12, new Color(0.5f, 0.7f, 0.5f), FontStyle.Normal, TextAnchor.MiddleLeft);
                var sumRt = summaryText.rectTransform;
                sumRt.anchorMin = sumRt.anchorMax = new Vector2(0f, 1f);
                sumRt.pivot = new Vector2(0f, 1f);
                sumRt.anchoredPosition = new Vector2(4f, y);
                sumRt.sizeDelta = new Vector2(listW, 20f);
                _researchItemRows.Add(summaryText.gameObject);
            }
        }

        private string _lastResearchState;
        private void UpdateResearchPanel()
        {
            if (_researchPanel == null || !_researchPanel.activeSelf) return;

            // Only do progress bar update, not full rebuild
            var gm = GameManager.Instance;
            if (gm?.ResearchManager == null) return;
            var rm = gm.ResearchManager;

            // Quick progress bar update
            if (rm.IsResearching)
            {
                var cur = rm.CurrentResearch;
                _researchProgressText.text = $"{cur.ProgressRatio:P0}";
                float barW = 440f;
                _researchProgressFill.rectTransform.sizeDelta = new Vector2(barW * cur.ProgressRatio, 0f);
            }

            // Full rebuild only when state changes
            string stateKey = $"{rm.IsResearching}_{rm.CompletedResearchCount}_{rm.ScientistCount}";
            if (stateKey != _lastResearchState)
            {
                _lastResearchState = stateKey;
                RefreshResearchPanel();
            }
        }

        // ================================================================
        // ローンパネル
        // ================================================================

        private void BuildLoanPanel(RectTransform root)
        {
            float panelW = 420f;
            float panelH = 460f;

            _loanPanel = new GameObject("LoanPanel");
            _loanPanel.transform.SetParent(root, false);
            var rt = _loanPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(panelW, panelH);

            var bgImg = _loanPanel.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.08f, 0.14f, 0.97f);
            bgImg.raycastTarget = true;

            // タイトル
            var title = MakeLabel(rt, "Title", LocalizationData.LoanTitle, 24, Gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -10f);
            titleRt.sizeDelta = new Vector2(panelW, 30f);

            // 現在の残高・ローン状況
            _loanBalanceText = MakeLabel(rt, "Balance", "", 16, Cyan,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            var balRt = _loanBalanceText.rectTransform;
            balRt.anchorMin = balRt.anchorMax = new Vector2(0f, 1f);
            balRt.pivot = new Vector2(0f, 1f);
            balRt.anchoredPosition = new Vector2(15f, -48f);
            balRt.sizeDelta = new Vector2(panelW - 30f, 22f);

            _loanCountText = MakeLabel(rt, "LoanCount", "", 14, Muted,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            var lcRt = _loanCountText.rectTransform;
            lcRt.anchorMin = lcRt.anchorMax = new Vector2(0f, 1f);
            lcRt.pivot = new Vector2(0f, 1f);
            lcRt.anchoredPosition = new Vector2(15f, -72f);
            lcRt.sizeDelta = new Vector2(panelW - 30f, 20f);

            // ローン詳細リスト
            _loanDetailText = MakeLabel(rt, "LoanDetail", "", 13, Color.white,
                FontStyle.Normal, TextAnchor.UpperLeft);
            _loanDetailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _loanDetailText.verticalOverflow = VerticalWrapMode.Truncate;
            var ldRt = _loanDetailText.rectTransform;
            ldRt.anchorMin = ldRt.anchorMax = new Vector2(0f, 1f);
            ldRt.pivot = new Vector2(0f, 1f);
            ldRt.anchoredPosition = new Vector2(15f, -100f);
            ldRt.sizeDelta = new Vector2(panelW - 30f, 120f);

            // ---- 借入セクション ----
            var borrowLabel = MakeLabel(rt, "BorrowLabel", "--- 新規借入 ---", 15, Gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var blRt = borrowLabel.rectTransform;
            blRt.anchorMin = blRt.anchorMax = new Vector2(0.5f, 1f);
            blRt.pivot = new Vector2(0.5f, 1f);
            blRt.anchoredPosition = new Vector2(0f, -228f);
            blRt.sizeDelta = new Vector2(panelW, 22f);

            // 金額表示
            _loanBorrowAmountText = MakeLabel(rt, "BorrowAmount", "$10,000", 20, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var baRt = _loanBorrowAmountText.rectTransform;
            baRt.anchorMin = baRt.anchorMax = new Vector2(0.5f, 1f);
            baRt.pivot = new Vector2(0.5f, 1f);
            baRt.anchoredPosition = new Vector2(0f, -256f);
            baRt.sizeDelta = new Vector2(200f, 28f);

            // - ボタン
            var minusGo = MakePanel(rt, "MinusBtn", 40f, 28f, new Color(0.5f, 0.3f, 0.3f));
            var minusRt2 = minusGo.GetComponent<RectTransform>();
            minusRt2.anchorMin = minusRt2.anchorMax = new Vector2(0.5f, 1f);
            minusRt2.pivot = new Vector2(0.5f, 1f);
            minusRt2.anchoredPosition = new Vector2(-120f, -256f);
            var minusImg = minusGo.GetComponent<Image>();
            minusImg.raycastTarget = true;
            var minusBtn = minusGo.AddComponent<Button>();
            minusBtn.targetGraphic = minusImg;
            minusBtn.onClick.AddListener(() =>
            {
                _loanBorrowAmount = Mathf.Max(10000f, _loanBorrowAmount - 10000f);
                _loanBorrowAmountText.text = $"${_loanBorrowAmount:N0}";
            });
            var minusLbl = MakeLabel(minusRt2, "L", "-", 20, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(minusLbl.rectTransform);

            // + ボタン
            var plusGo = MakePanel(rt, "PlusBtn", 40f, 28f, new Color(0.3f, 0.5f, 0.3f));
            var plusRt2 = plusGo.GetComponent<RectTransform>();
            plusRt2.anchorMin = plusRt2.anchorMax = new Vector2(0.5f, 1f);
            plusRt2.pivot = new Vector2(0.5f, 1f);
            plusRt2.anchoredPosition = new Vector2(120f, -256f);
            var plusImg = plusGo.GetComponent<Image>();
            plusImg.raycastTarget = true;
            var plusBtn = plusGo.AddComponent<Button>();
            plusBtn.targetGraphic = plusImg;
            plusBtn.onClick.AddListener(() =>
            {
                _loanBorrowAmount = Mathf.Min(200000f, _loanBorrowAmount + 10000f);
                _loanBorrowAmountText.text = $"${_loanBorrowAmount:N0}";
            });
            var plusLbl = MakeLabel(plusRt2, "L", "+", 20, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(plusLbl.rectTransform);

            // 借入ボタン
            var borrowBtnGo = MakePanel(rt, "BorrowBtn", 180f, 36f, new Color(0.2f, 0.55f, 0.3f));
            var borrowBtnRt = borrowBtnGo.GetComponent<RectTransform>();
            borrowBtnRt.anchorMin = borrowBtnRt.anchorMax = new Vector2(0.5f, 1f);
            borrowBtnRt.pivot = new Vector2(0.5f, 1f);
            borrowBtnRt.anchoredPosition = new Vector2(-55f, -296f);
            var borrowBtnImg = borrowBtnGo.GetComponent<Image>();
            borrowBtnImg.raycastTarget = true;
            var borrowButton = borrowBtnGo.AddComponent<Button>();
            borrowButton.targetGraphic = borrowBtnImg;
            var borrowC = borrowButton.colors;
            borrowC.highlightedColor = new Color(0.3f, 0.65f, 0.4f);
            borrowC.pressedColor = new Color(0.15f, 0.4f, 0.2f);
            borrowButton.colors = borrowC;
            borrowButton.onClick.AddListener(OnBorrowClicked);
            var borrowBtnLbl = MakeLabel(borrowBtnRt, "L", "借入する", 16, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(borrowBtnLbl.rectTransform);

            // 繰り上げ返済ボタン
            var repayBtnGo = MakePanel(rt, "RepayBtn", 180f, 36f, new Color(0.5f, 0.35f, 0.2f));
            var repayBtnRt = repayBtnGo.GetComponent<RectTransform>();
            repayBtnRt.anchorMin = repayBtnRt.anchorMax = new Vector2(0.5f, 1f);
            repayBtnRt.pivot = new Vector2(0.5f, 1f);
            repayBtnRt.anchoredPosition = new Vector2(55f, -296f);
            var repayBtnImg = repayBtnGo.GetComponent<Image>();
            repayBtnImg.raycastTarget = true;
            var repayButton = repayBtnGo.AddComponent<Button>();
            repayButton.targetGraphic = repayBtnImg;
            var repayC = repayButton.colors;
            repayC.highlightedColor = new Color(0.6f, 0.45f, 0.3f);
            repayC.pressedColor = new Color(0.38f, 0.25f, 0.15f);
            repayButton.colors = repayC;
            repayButton.onClick.AddListener(OnRepayClicked);
            var repayBtnLbl = MakeLabel(repayBtnRt, "L", "繰り上げ返済", 14, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(repayBtnLbl.rectTransform);

            // ---- 入場料調整セクション ----
            var feeLabel = MakeLabel(rt, "FeeLabel", "--- 入場料設定 ---", 14, Gold,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var flRt = feeLabel.rectTransform;
            flRt.anchorMin = flRt.anchorMax = new Vector2(0.5f, 1f);
            flRt.pivot = new Vector2(0.5f, 1f);
            flRt.anchoredPosition = new Vector2(0f, -340f);
            flRt.sizeDelta = new Vector2(panelW, 20f);

            _entranceFeeText = MakeLabel(rt, "FeeAmount", "$15", 18, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            var feeTRt = _entranceFeeText.rectTransform;
            feeTRt.anchorMin = feeTRt.anchorMax = new Vector2(0.5f, 1f);
            feeTRt.pivot = new Vector2(0.5f, 1f);
            feeTRt.anchoredPosition = new Vector2(0f, -362f);
            feeTRt.sizeDelta = new Vector2(120f, 24f);

            var feeDownGo = MakePanel(rt, "FeeDown", 36f, 24f, new Color(0.5f, 0.3f, 0.3f));
            var feeDownRt = feeDownGo.GetComponent<RectTransform>();
            feeDownRt.anchorMin = feeDownRt.anchorMax = new Vector2(0.5f, 1f);
            feeDownRt.pivot = new Vector2(0.5f, 1f);
            feeDownRt.anchoredPosition = new Vector2(-80f, -362f);
            feeDownGo.GetComponent<Image>().raycastTarget = true;
            var feeDownBtn = feeDownGo.AddComponent<Button>();
            feeDownBtn.targetGraphic = feeDownGo.GetComponent<Image>();
            feeDownBtn.onClick.AddListener(() =>
            {
                var p = GameManager.Instance?.EconomyManager?.Pricing;
                if (p != null)
                {
                    p.SetEntranceFee(p.EntranceFee - 5f);
                    _entranceFeeText.text = $"${p.EntranceFee:N0}";
                }
            });
            var fdLbl = MakeLabel(feeDownRt, "L", "-5", 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(fdLbl.rectTransform);

            var feeUpGo = MakePanel(rt, "FeeUp", 36f, 24f, new Color(0.3f, 0.5f, 0.3f));
            var feeUpRt = feeUpGo.GetComponent<RectTransform>();
            feeUpRt.anchorMin = feeUpRt.anchorMax = new Vector2(0.5f, 1f);
            feeUpRt.pivot = new Vector2(0.5f, 1f);
            feeUpRt.anchoredPosition = new Vector2(80f, -362f);
            feeUpGo.GetComponent<Image>().raycastTarget = true;
            var feeUpBtn = feeUpGo.AddComponent<Button>();
            feeUpBtn.targetGraphic = feeUpGo.GetComponent<Image>();
            feeUpBtn.onClick.AddListener(() =>
            {
                var p = GameManager.Instance?.EconomyManager?.Pricing;
                if (p != null)
                {
                    p.SetEntranceFee(p.EntranceFee + 5f);
                    _entranceFeeText.text = $"${p.EntranceFee:N0}";
                }
            });
            var fuLbl = MakeLabel(feeUpRt, "L", "+5", 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(fuLbl.rectTransform);

            // ヒントテキスト
            var hint = MakeLabel(rt, "Hint", "年利8% | 最大3件 | 上限$200,000", 12, Muted,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 1f);
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.anchoredPosition = new Vector2(0f, -394f);
            hintRt.sizeDelta = new Vector2(panelW - 20f, 18f);

            // 閉じるボタン
            var closeGo = MakePanel(rt, "CloseBtn", 90f, 30f, new Color(0.5f, 0.2f, 0.2f));
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 8f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.raycastTarget = true;
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() => _loanPanel.SetActive(false));
            var closeLbl = MakeLabel(closeRt, "L", LocalizationData.BtnClose, 14, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLbl.rectTransform);

            _loanPanel.SetActive(false);
        }

        private void OnBorrowClicked()
        {
            var em = GameManager.Instance?.EconomyManager;
            if (em == null) return;

            var loan = em.TakeLoan(_loanBorrowAmount);
            if (loan != null)
            {
                NotificationSystem.Instance?.Notify(
                    $"ローン借入: ${_loanBorrowAmount:N0} (月額返済: ${loan.MonthlyPayment:N0})",
                    NotifLevel.Info);
                RefreshLoanPanel();
            }
            else
            {
                NotificationSystem.Instance?.Notify("ローンの借入に失敗しました", NotifLevel.Warning);
            }
        }

        private void OnRepayClicked()
        {
            var em = GameManager.Instance?.EconomyManager;
            if (em == null) return;

            var loans = em.GetActiveLoans();
            if (loans.Count == 0) return;

            // 最初のアクティブローンに対して繰り上げ返済（借入額分）
            var target = loans[0];
            float repayAmount = Mathf.Min(_loanBorrowAmount, target.RemainingBalance);
            if (em.RepayLoanEarly(target.LoanId, repayAmount))
            {
                NotificationSystem.Instance?.Notify(
                    $"ローン#{target.LoanId} 繰り上げ返済: ${repayAmount:N0}",
                    NotifLevel.Success);
                RefreshLoanPanel();
            }
            else
            {
                NotificationSystem.Instance?.Notify("返済に失敗しました（資金不足）", NotifLevel.Warning);
            }
        }

        private void RefreshLoanPanel()
        {
            var em = GameManager.Instance?.EconomyManager;
            if (em == null) return;

            _loanBalanceText.text = $"所持金: ${em.CurrentBalance:N0}  |  総借入残高: ${em.TotalLoanBalance:N0}";

            // 入場料表示更新
            if (_entranceFeeText != null && em.Pricing != null)
                _entranceFeeText.text = $"${em.Pricing.EntranceFee:N0}";
            _loanCountText.text = $"アクティブローン: {em.ActiveLoanCount} / 3";

            var loans = em.GetActiveLoans();
            if (loans.Count == 0)
            {
                _loanDetailText.text = "現在、アクティブなローンはありません。\n\n資金が不足した場合は新規借入を検討してください。";
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                foreach (var loan in loans)
                {
                    sb.AppendLine($"[ローン #{loan.LoanId}]");
                    sb.AppendLine($"  元本: ${loan.Principal:N0}  残高: ${loan.RemainingBalance:N0}");
                    sb.AppendLine($"  月額返済: ${loan.MonthlyPayment:N0}  経過: {loan.ElapsedMonths}/{loan.TermMonths}ヶ月");
                    sb.AppendLine();
                }
                _loanDetailText.text = sb.ToString();
            }
        }

        private void UpdateLoanPanel()
        {
            if (_loanPanel == null || !_loanPanel.activeSelf) return;
            var em = GameManager.Instance?.EconomyManager;
            if (em == null) return;

            string stateKey = $"{em.ActiveLoanCount}_{em.TotalLoanBalance:F0}_{em.CurrentBalance:F0}";
            if (stateKey != _lastLoanState)
            {
                _lastLoanState = stateKey;
                RefreshLoanPanel();
            }
        }

        // ================================================================
        // 月次財務レポートポップアップ
        // ================================================================

        private void BuildMonthlyReportPanel(RectTransform root)
        {
            float panelW = 380f, panelH = 320f;
            _monthlyReportPanel = MakePanel(root, "MonthlyReportPanel", panelW, panelH,
                new Color(0.05f, 0.08f, 0.18f, 0.95f));
            var rt = _monthlyReportPanel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            float y = panelH / 2f - 20f;
            _mrTitle = MakeLabel(rt, "Title", "月次レポート", 24, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            _mrTitle.rectTransform.anchorMin = _mrTitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _mrTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            _mrTitle.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            _mrTitle.rectTransform.sizeDelta = new Vector2(panelW - 20f, 30f);

            float labelX = -panelW / 2f + 20f;

            _mrRevenue = MakeLabel(rt, "Revenue", "収入: ---", 20, Green, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetAnchoredTopLeft(_mrRevenue.rectTransform, 20f, 50f, panelW - 40f, 28f);

            _mrExpenses = MakeLabel(rt, "Expenses", "支出: ---", 20, Red, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetAnchoredTopLeft(_mrExpenses.rectTransform, 20f, 82f, panelW - 40f, 28f);

            _mrProfit = MakeLabel(rt, "Profit", "利益: ---", 22, Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetAnchoredTopLeft(_mrProfit.rectTransform, 20f, 118f, panelW - 40f, 28f);

            // 区切り線代わりのラベル
            var sep = MakeLabel(rt, "Sep", "────────────────────", 12, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetAnchoredTopLeft(sep.rectTransform, 10f, 150f, panelW - 20f, 16f);

            _mrVisitors = MakeLabel(rt, "Visitors", "来場者: ---", 18, Cyan, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetAnchoredTopLeft(_mrVisitors.rectTransform, 20f, 170f, panelW - 40f, 24f);

            _mrDetails = MakeLabel(rt, "Details", "", 15, Muted, FontStyle.Normal, TextAnchor.UpperLeft);
            SetAnchoredTopLeft(_mrDetails.rectTransform, 20f, 200f, panelW - 40f, 70f);

            // 閉じるボタン
            var closeGo = MakePanel(rt, "CloseBtn", 100f, 32f, new Color(0.3f, 0.35f, 0.5f, 0.9f));
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 12f);
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeGo.GetComponent<Image>();
            closeBtn.onClick.AddListener(() => _monthlyReportPanel.SetActive(false));
            var closeLabel = MakeLabel(closeRt, "Label", "OK", 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFill(closeLabel.rectTransform);

            _monthlyReportPanel.SetActive(false);
        }

        private void SetAnchoredTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private void ShowMonthlyReport()
        {
            if (_monthlyReportPanel == null) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.EconomyManager == null) return;
            if (gm.CurrentState != GameState.Playing) return;

            var report = gm.EconomyManager.GetLatestMonthlyReport();
            if (report == null) return;

            // 同じ月のレポートを二重表示しない
            int reportKey = report.Year * 100 + report.Month;
            if (reportKey == _lastReportMonth) return;
            _lastReportMonth = reportKey;

            _mrTitle.text = $"月次レポート  {report.Year}年 {report.Month}月";
            _mrRevenue.text = $"収入:  ${report.Revenue.Total:N0}";
            _mrExpenses.text = $"支出:  ${report.Expenses.Total:N0}";

            float profit = report.NetProfit;
            _mrProfit.text = $"利益:  ${profit:N0}";
            _mrProfit.color = profit >= 0 ? Green : Red;

            _mrVisitors.text = $"来場者数:  {report.TotalVisitors}名";

            string details = "";
            if (report.Revenue.EntranceFees > 0)
                details += $"入場料: ${report.Revenue.EntranceFees:N0}\n";
            if (report.Revenue.AttractionFees > 0)
                details += $"アトラクション: ${report.Revenue.AttractionFees:N0}\n";
            if (report.Revenue.ShopSales > 0)
                details += $"ショップ: ${report.Revenue.ShopSales:N0}\n";
            if (report.Expenses.StaffSalaries > 0)
                details += $"人件費: -${report.Expenses.StaffSalaries:N0}\n";
            if (report.Expenses.Maintenance > 0)
                details += $"維持費: -${report.Expenses.Maintenance:N0}";
            _mrDetails.text = details;

            _monthlyReportPanel.SetActive(true);
            _monthlyReportAutoClose = 15f; // 15秒後に自動で閉じる
        }
    }
}
