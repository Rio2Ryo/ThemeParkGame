// ============================================================
// ThemeParkGame - LoanInvestmentUI
// ローン管理 & 投資UIパネル
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ThemeParkGame.Core;

namespace ThemeParkGame.Economy
{
    /// <summary>
    /// ローン借入・返済の管理UIと、アトラクション投資機能を提供する。
    ///
    /// 【ゲームデザイン】
    /// ・銀行ローン: 資金調達の手段。借入額・期間を選択可能。
    /// ・繰上返済: 余裕がある時に一括返済して利息を削減。
    /// ・投資プラン: 大型アトラクション建設のための融資パッケージ。
    ///   通常ローンより低金利だが用途制限あり。
    /// </summary>
    public class LoanInvestmentUI : MonoBehaviour
    {
        // ============================================================
        // 投資プランデータ
        // ============================================================

        private struct InvestmentPlan
        {
            public string Name;
            public string Description;
            public float Amount;
            public int TermMonths;
            public float BonusRating;
        }

        private static readonly InvestmentPlan[] InvestmentPlans = new InvestmentPlan[]
        {
            new InvestmentPlan
            {
                Name = "小規模開発プラン",
                Description = "ショップ・施設の充実に最適",
                Amount = 20000f,
                TermMonths = 12,
                BonusRating = 1f
            },
            new InvestmentPlan
            {
                Name = "アトラクション投資プラン",
                Description = "新アトラクション1基の建設費用",
                Amount = 50000f,
                TermMonths = 24,
                BonusRating = 2f
            },
            new InvestmentPlan
            {
                Name = "大規模拡張プラン",
                Description = "ゾーン全体の大規模開発",
                Amount = 100000f,
                TermMonths = 36,
                BonusRating = 4f
            },
            new InvestmentPlan
            {
                Name = "フラッグシップ投資",
                Description = "目玉アトラクション建設のための最大融資",
                Amount = 200000f,
                TermMonths = 48,
                BonusRating = 8f
            }
        };

        // ============================================================
        // フィールド
        // ============================================================

        private GameObject _uiPanel;
        private GameObject _loanListContent;
        private GameObject _investContent;
        private Text _headerText;
        private Text _balanceText;
        private bool _uiVisible;
        private bool _showingLoans = true; // true=ローン管理, false=投資プラン

        public static LoanInvestmentUI Instance { get; private set; }

        // ============================================================
        // ライフサイクル
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ============================================================
        // UI表示制御
        // ============================================================

        public void ShowUI()
        {
            if (_uiPanel == null) CreateUI();
            RefreshUI();
            _uiPanel.SetActive(true);
            _uiVisible = true;
        }

        public void HideUI()
        {
            if (_uiPanel != null) _uiPanel.SetActive(false);
            _uiVisible = false;
        }

        public void ToggleUI()
        {
            if (_uiVisible) HideUI();
            else ShowUI();
        }

        // ============================================================
        // UI構築
        // ============================================================

        private void CreateUI()
        {
            var canvas = CanvasCache.Get();
            if (canvas == null) return;

            // メインパネル
            _uiPanel = new GameObject("LoanInvestmentPanel");
            _uiPanel.transform.SetParent(canvas.transform, false);
            var panelRect = _uiPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.15f, 0.08f);
            panelRect.anchorMax = new Vector2(0.85f, 0.92f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImg = _uiPanel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.1f, 0.16f, 0.96f);

            // タイトル
            var titleObj = CreateUIText(_uiPanel.transform, "Title",
                new Vector2(0f, 0.92f), new Vector2(0.7f, 1f), "", 22, FontStyle.Bold);
            _headerText = titleObj.GetComponent<Text>();

            // 閉じるボタン
            CreateButton(_uiPanel.transform, "CloseBtn",
                new Vector2(0.9f, 0.93f), new Vector2(0.98f, 0.99f),
                "X", new Color(0.7f, 0.15f, 0.15f), HideUI);

            // タブ: ローン管理 / 投資プラン
            CreateButton(_uiPanel.transform, "TabLoans",
                new Vector2(0.02f, 0.93f), new Vector2(0.25f, 0.99f),
                "ローン管理", new Color(0.2f, 0.3f, 0.5f), () => { _showingLoans = true; RefreshUI(); });

            CreateButton(_uiPanel.transform, "TabInvest",
                new Vector2(0.26f, 0.93f), new Vector2(0.49f, 0.99f),
                "投資プラン", new Color(0.3f, 0.4f, 0.2f), () => { _showingLoans = false; RefreshUI(); });

            // 残高表示
            var balObj = CreateUIText(_uiPanel.transform, "Balance",
                new Vector2(0.5f, 0.92f), new Vector2(0.98f, 0.93f), "", 13, FontStyle.Normal);
            _balanceText = balObj.GetComponent<Text>();
            _balanceText.alignment = TextAnchor.MiddleRight;
            _balanceText.color = new Color(0.6f, 0.9f, 0.6f);

            // ローン管理コンテンツ
            _loanListContent = CreateScrollContent(_uiPanel.transform, "LoanScroll",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.91f));

            // 投資プランコンテンツ
            _investContent = CreateScrollContent(_uiPanel.transform, "InvestScroll",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.91f));

            _uiPanel.SetActive(false);
        }

        private GameObject CreateScrollContent(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var scrollObj = new GameObject(name);
            scrollObj.transform.SetParent(parent, false);
            var scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = anchorMin;
            scrollRect.anchorMax = anchorMax;
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var scrollView = scrollObj.AddComponent<ScrollRect>();
            var scrollImg = scrollObj.AddComponent<Image>();
            scrollImg.color = new Color(0.06f, 0.06f, 0.1f, 0.7f);
            scrollObj.AddComponent<Mask>().showMaskGraphic = true;

            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(scrollObj.transform, false);
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollView.content = contentRect;
            scrollView.vertical = true;
            scrollView.horizontal = false;

            return contentObj;
        }

        // ============================================================
        // UI更新
        // ============================================================

        private void RefreshUI()
        {
            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null) return;

            if (_balanceText != null)
            {
                _balanceText.text = $"所持金: ${econ.CurrentMoney:N0}";
            }

            if (_showingLoans)
            {
                if (_headerText != null) _headerText.text = "銀行ローン管理";
                if (_loanListContent != null)
                {
                    _loanListContent.transform.parent.gameObject.SetActive(true);
                    ClearChildren(_loanListContent);
                    BuildLoanUI(econ);
                }
                if (_investContent != null)
                    _investContent.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                if (_headerText != null) _headerText.text = "投資プラン";
                if (_investContent != null)
                {
                    _investContent.transform.parent.gameObject.SetActive(true);
                    ClearChildren(_investContent);
                    BuildInvestUI(econ);
                }
                if (_loanListContent != null)
                    _loanListContent.transform.parent.gameObject.SetActive(false);
            }
        }

        private void BuildLoanUI(EconomyManager econ)
        {
            // 新規借入セクション
            var newLoanHeader = CreateListItem(_loanListContent.transform,
                "NewLoanHeader", "--- 新規ローン借入 ---", 35f,
                new Color(0.15f, 0.25f, 0.4f, 0.9f));

            // プリセット借入額ボタン
            float[] loanAmounts = { 10000f, 30000f, 50000f, 100000f, 200000f };
            int[] terms = { 12, 18, 24, 36, 48 };

            for (int i = 0; i < loanAmounts.Length; i++)
            {
                float amount = loanAmounts[i];
                int term = terms[i];

                float monthlyRate = 0.08f / 12f;
                float factor = Mathf.Pow(1f + monthlyRate, term);
                float monthly = amount * (monthlyRate * factor) / (factor - 1f);
                float totalRepay = monthly * term;

                var itemObj = CreateListItem(_loanListContent.transform,
                    $"NewLoan_{i}",
                    $"${amount:N0}  ({term}ヶ月)  月額: ${monthly:N0}  総返済: ${totalRepay:N0}",
                    45f, new Color(0.12f, 0.18f, 0.28f, 0.8f));

                // 借入ボタン
                float capturedAmount = amount;
                int capturedTerm = term;
                CreateButton(itemObj.transform, "TakeLoanBtn",
                    new Vector2(0.8f, 0.1f), new Vector2(0.98f, 0.9f),
                    "借入", new Color(0.2f, 0.5f, 0.2f),
                    () => OnTakeLoan(capturedAmount, capturedTerm));
            }

            // 既存ローン一覧
            var activeLoans = econ.GetActiveLoans();
            if (activeLoans.Count > 0)
            {
                CreateListItem(_loanListContent.transform,
                    "ActiveHeader", $"--- 返済中のローン ({activeLoans.Count}件) ---", 35f,
                    new Color(0.4f, 0.25f, 0.15f, 0.9f));

                foreach (var loan in activeLoans)
                {
                    int remaining = loan.TermMonths - loan.ElapsedMonths;
                    var itemObj = CreateListItem(_loanListContent.transform,
                        $"Loan_{loan.LoanId}",
                        $"ローン#{loan.LoanId}  元本: ${loan.Principal:N0}  " +
                        $"残高: ${loan.RemainingBalance:N0}  月額: ${loan.MonthlyPayment:N0}  " +
                        $"残り{remaining}ヶ月",
                        50f, new Color(0.2f, 0.15f, 0.1f, 0.8f));

                    // 繰上返済ボタン
                    int loanId = loan.LoanId;
                    float balance = loan.RemainingBalance;
                    bool canRepay = econ.CurrentMoney >= balance;

                    var btn = CreateButton(itemObj.transform, "RepayBtn",
                        new Vector2(0.75f, 0.1f), new Vector2(0.98f, 0.9f),
                        $"全額返済\n${balance:N0}",
                        canRepay ? new Color(0.5f, 0.3f, 0.1f) : new Color(0.3f, 0.3f, 0.3f),
                        () => OnRepayLoan(loanId, balance));

                    btn.GetComponent<Button>().interactable = canRepay;
                }
            }
            else
            {
                CreateListItem(_loanListContent.transform,
                    "NoLoans", "返済中のローンはありません", 35f,
                    new Color(0.15f, 0.2f, 0.15f, 0.6f));
            }
        }

        private void BuildInvestUI(EconomyManager econ)
        {
            CreateListItem(_investContent.transform,
                "InvestDesc",
                "投資プランを選択して融資を受けましょう。\n" +
                "計画的な投資でパーク評価を向上させ、来場者を増やしましょう！",
                55f, new Color(0.15f, 0.2f, 0.3f, 0.8f));

            int activeCount = econ.GetActiveLoans().Count;

            for (int i = 0; i < InvestmentPlans.Length; i++)
            {
                var plan = InvestmentPlans[i];
                bool canTake = activeCount < 3 && econ.CurrentMoney >= 0; // ローン上限チェック

                float monthlyRate = 0.08f / 12f;
                float factor = Mathf.Pow(1f + monthlyRate, plan.TermMonths);
                float monthly = plan.Amount * (monthlyRate * factor) / (factor - 1f);

                var itemObj = CreateListItem(_investContent.transform,
                    $"Plan_{i}",
                    $"{plan.Name}\n{plan.Description}\n" +
                    $"融資額: ${plan.Amount:N0}  期間: {plan.TermMonths}ヶ月  月額返済: ${monthly:N0}\n" +
                    $"パーク評価ボーナス: +{plan.BonusRating:F1}",
                    80f, new Color(0.12f, 0.2f, 0.12f, 0.8f));

                int planIndex = i;
                var btn = CreateButton(itemObj.transform, "InvestBtn",
                    new Vector2(0.78f, 0.15f), new Vector2(0.97f, 0.85f),
                    "投資する",
                    canTake ? new Color(0.2f, 0.45f, 0.2f) : new Color(0.3f, 0.3f, 0.3f),
                    () => OnInvest(planIndex));

                if (!canTake) btn.GetComponent<Button>().interactable = false;
            }
        }

        // ============================================================
        // アクション
        // ============================================================

        private void OnTakeLoan(float amount, int termMonths)
        {
            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null) return;

            var loan = econ.TakeLoan(amount, termMonths);
            if (loan != null)
            {
                GameManager.Instance?.ShowNotification(
                    $"${amount:N0} のローンを借入しました (月額返済: ${loan.MonthlyPayment:N0})",
                    NotifLevel.Info);
                RefreshUI();
            }
            else
            {
                GameManager.Instance?.ShowNotification(
                    "ローンを借りられません（上限到達または金額不正）", NotifLevel.Warning);
            }
        }

        private void OnRepayLoan(int loanId, float amount)
        {
            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null) return;

            if (econ.RepayLoanEarly(loanId, amount))
            {
                GameManager.Instance?.ShowNotification(
                    $"ローン#{loanId} を完済しました！", NotifLevel.Success);
                RefreshUI();
            }
            else
            {
                GameManager.Instance?.ShowNotification(
                    "返済に失敗しました（資金不足）", NotifLevel.Warning);
            }
        }

        private void OnInvest(int planIndex)
        {
            if (planIndex < 0 || planIndex >= InvestmentPlans.Length) return;

            var plan = InvestmentPlans[planIndex];
            var econ = GameManager.Instance?.EconomyManager;
            if (econ == null) return;

            var loan = econ.TakeLoan(plan.Amount, plan.TermMonths);
            if (loan != null)
            {
                // 投資ボーナス: パーク評価にFameボーナスを付与
                var pm = GameManager.Instance?.ParkManager;
                if (pm?.Rating != null)
                {
                    pm.Rating.ApplyExternalBonus(CertificateCategory.Fame, plan.BonusRating);
                    pm.Rating.ApplyExternalBonus(CertificateCategory.Excitement, plan.BonusRating * 0.5f);
                }

                GameManager.Instance?.ShowNotification(
                    $"{plan.Name} の融資を獲得！ ${plan.Amount:N0} (評価+{plan.BonusRating:F1})",
                    NotifLevel.Success);
                RefreshUI();
            }
            else
            {
                GameManager.Instance?.ShowNotification(
                    "投資プランを利用できません（ローン上限到達）", NotifLevel.Warning);
            }
        }

        // ============================================================
        // UIヘルパー
        // ============================================================

        private GameObject CreateListItem(Transform parent, string name, string text,
            float height, Color bgColor)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            var img = obj.AddComponent<Image>();
            img.color = bgColor;

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(obj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = new Vector2(0.75f, 1f);
            txtRect.offsetMin = new Vector2(10f, 2f);
            txtRect.offsetMax = new Vector2(-5f, -2f);
            var txt = txtObj.AddComponent<Text>();
            txt.text = text;
            txt.font = FontManager.Regular;
            txt.fontSize = 13;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft;

            return obj;
        }

        private GameObject CreateUIText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string text,
            int fontSize, FontStyle style)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(10f, 0f);
            rect.offsetMax = new Vector2(-10f, 0f);
            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.font = FontManager.Regular;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft;
            return obj;
        }

        private GameObject CreateButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string text, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = obj.AddComponent<Image>();
            img.color = bgColor;

            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(obj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(2f, 0f);
            txtRect.offsetMax = new Vector2(-2f, 0f);
            var txt = txtObj.AddComponent<Text>();
            txt.text = text;
            txt.font = FontManager.Regular;
            txt.fontSize = 12;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            return obj;
        }

        private void ClearChildren(GameObject parent)
        {
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.transform.GetChild(i).gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
