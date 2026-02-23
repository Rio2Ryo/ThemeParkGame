// ============================================================
// ThemeParkGame - EconomyManager
// 経済システムの中央管理（収支・ローン・財務レポート）
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Economy
{
    /// <summary>
    /// 収入カテゴリ。AddRevenue()で使用する。
    /// </summary>
    public enum RevenueCategory
    {
        EntranceFee,
        AttractionFee,
        ShopSale,
        Other
    }

    /// <summary>
    /// 支出カテゴリ。PayExpense()で使用する。
    /// </summary>
    public enum ExpenseCategory
    {
        StaffSalary,
        Maintenance,
        Research,
        Construction,
        LoanRepayment,
        Other
    }

    /// <summary>
    /// ローン情報。借入金の元本・利息・返済状況を管理する。
    /// </summary>
    [Serializable]
    public class LoanData
    {
        /// <summary>ローンID</summary>
        public int LoanId;

        /// <summary>借入元本</summary>
        public float Principal;

        /// <summary>年利率（例: 0.05 = 5%）</summary>
        public float AnnualInterestRate;

        /// <summary>残高</summary>
        public float RemainingBalance;

        /// <summary>月々の返済額</summary>
        public float MonthlyPayment;

        /// <summary>返済期間（月数）</summary>
        public int TermMonths;

        /// <summary>借入からの経過月数</summary>
        public int ElapsedMonths;

        /// <summary>完済済みか</summary>
        public bool IsFullyRepaid => RemainingBalance <= 0f;
    }

    /// <summary>
    /// 経済システムの中央コントローラー。
    /// パーク全体の収支を管理し、月次・年次レポートを生成する。
    ///
    /// 【ゲームデザイン: 収益の3本柱】
    /// 1. 入場料: 安定した基礎収入。パーク評価に連動して上限が決まる。
    /// 2. アトラクション料金: 主力収入。適正価格の維持が重要。
    ///    メンテナンスコストの4%未満で「最高評価」（来場者が満足する上限）。
    /// 3. ショップ売上: 補助収入。天候や来場者ニーズに左右される。
    ///    仕入れ原価の1.6倍未満が最適利益率。
    ///
    /// 【支出管理のポイント】
    /// - スタッフ給料は毎月固定で発生する最大の固定費
    /// - メンテナンス費は故障頻度に比例（メカニック配置で抑制可能）
    /// - 研究開発費は一時的だが長期的な競争力につながる
    /// - ローンは緊急時の資金調達手段（高利息に注意）
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        // ---- ローン設定 ----
        private const float BASE_LOAN_INTEREST_RATE = 0.08f;  // 基本年利8%
        private const int DEFAULT_LOAN_TERM_MONTHS = 24;       // 標準返済期間24ヶ月
        private const float MAX_LOAN_AMOUNT = 200000f;         // 借入上限
        private const int MAX_ACTIVE_LOANS = 3;                // 同時借入上限数

        /// <summary>現在の所持金</summary>
        public float CurrentBalance { get; private set; }

        /// <summary>CurrentBalanceの別名。UI層からの簡易アクセス用。</summary>
        public float CurrentMoney => CurrentBalance;

        /// <summary>価格管理システム</summary>
        public PricingSystem Pricing { get; private set; }

        // ---- 当月の収支トラッキング ----
        private RevenueBreakdown _currentMonthRevenue;
        private ExpenseBreakdown _currentMonthExpenses;
        private int _currentMonthVisitors;
        private float _currentMonthVisitorSpendingTotal;

        // ---- アトラクション別収益トラッキング ----
        private readonly Dictionary<int, AttractionProfitability> _attractionProfits
            = new Dictionary<int, AttractionProfitability>();

        // ---- 財務レポート履歴 ----
        private readonly List<MonthlyFinancialReport> _monthlyReports = new List<MonthlyFinancialReport>();
        private readonly List<YearlyFinancialReport> _yearlyReports = new List<YearlyFinancialReport>();

        /// <summary>
        /// 収入履歴（グラフ描画用）。直近24ヶ月分を保持する。
        /// </summary>
        private readonly List<float> _revenueHistory = new List<float>();
        private const int MAX_REVENUE_HISTORY = 24;

        // ---- ローン ----
        private readonly List<LoanData> _activeLoans = new List<LoanData>();
        private int _nextLoanId = 1;

        // ---- 累計統計 ----

        /// <summary>累計収入</summary>
        public float TotalRevenueEarned { get; private set; }

        /// <summary>累計支出</summary>
        public float TotalExpensesPaid { get; private set; }

        /// <summary>当月の収入合計</summary>
        public float CurrentMonthRevenue => _currentMonthRevenue.Total;

        /// <summary>当月の支出合計</summary>
        public float CurrentMonthExpenses => _currentMonthExpenses.Total;

        /// <summary>アクティブローン数</summary>
        public int ActiveLoanCount => _activeLoans.Count(l => !l.IsFullyRepaid);

        /// <summary>総借入残高</summary>
        public float TotalLoanBalance => _activeLoans.Where(l => !l.IsFullyRepaid).Sum(l => l.RemainingBalance);

        // ================================================================
        // 初期化
        // ================================================================

        /// <summary>
        /// 経済システムを初期化する。GameManager.StartNewGame()から呼ばれる。
        /// </summary>
        public void Initialize(int startingMoney)
        {
            CurrentBalance = startingMoney;
            TotalRevenueEarned = 0f;
            TotalExpensesPaid = 0f;

            Pricing = new PricingSystem();

            _currentMonthRevenue = new RevenueBreakdown();
            _currentMonthExpenses = new ExpenseBreakdown();
            _currentMonthVisitors = 0;
            _currentMonthVisitorSpendingTotal = 0f;

            _attractionProfits.Clear();
            _monthlyReports.Clear();
            _yearlyReports.Clear();
            _revenueHistory.Clear();
            _activeLoans.Clear();
            _nextLoanId = 1;

            SubscribeToEvents();
            Debug.Log($"[EconomyManager] 初期化完了。初期資金: {startingMoney}");
        }

        private void SubscribeToEvents()
        {
            // TimeManagerの月・年イベントに購読
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnMonthChanged += OnMonthEnd;
                GameManager.Instance.TimeManager.OnYearChanged += OnYearEnd;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                GameManager.Instance.TimeManager.OnMonthChanged -= OnMonthEnd;
                GameManager.Instance.TimeManager.OnYearChanged -= OnYearEnd;
            }
        }

        // ================================================================
        // 収入処理
        // ================================================================

        /// <summary>
        /// 収入を計上する。所持金に加算し、月次データを更新する。
        /// </summary>
        /// <param name="amount">収入額（正の値）</param>
        /// <param name="category">収入カテゴリ</param>
        /// <param name="facilityId">関連施設ID（アトラクション・ショップの場合）</param>
        public void AddRevenue(float amount, RevenueCategory category, int facilityId = -1)
        {
            if (amount <= 0f)
            {
                Debug.LogWarning($"[EconomyManager] AddRevenue: 不正な金額 {amount}");
                return;
            }

            CurrentBalance += amount;
            TotalRevenueEarned += amount;

            // カテゴリ別に振り分け
            switch (category)
            {
                case RevenueCategory.EntranceFee:
                    _currentMonthRevenue.EntranceFees += amount;
                    break;
                case RevenueCategory.AttractionFee:
                    _currentMonthRevenue.AttractionFees += amount;
                    TrackAttractionRevenue(facilityId, amount);
                    break;
                case RevenueCategory.ShopSale:
                    _currentMonthRevenue.ShopSales += amount;
                    break;
                case RevenueCategory.Other:
                    _currentMonthRevenue.OtherRevenue += amount;
                    break;
            }

            GameEvents.FireRevenueEarned(amount);
            GameEvents.FireMoneyChanged(CurrentBalance);
        }

        /// <summary>来場者1人分の入場料を徴収する</summary>
        public void ChargeEntranceFee()
        {
            float fee = Pricing.EntranceFee;
            if (fee > 0f)
            {
                AddRevenue(fee, RevenueCategory.EntranceFee);
                _currentMonthVisitors++;
                _currentMonthVisitorSpendingTotal += fee;
            }
            else
            {
                _currentMonthVisitors++;
            }
        }

        /// <summary>来場者がショップ・アトラクションで支払った金額を記録する</summary>
        public void RecordVisitorSpending(float amount)
        {
            if (amount > 0f)
            {
                _currentMonthVisitorSpendingTotal += amount;
            }
        }

        // ================================================================
        // 支出処理
        // ================================================================

        /// <summary>
        /// 経費を支払う。所持金から減算し、月次データを更新する。
        /// </summary>
        /// <param name="amount">支出額（正の値）</param>
        /// <param name="category">支出カテゴリ</param>
        /// <param name="facilityId">関連施設ID（メンテナンス等の場合）</param>
        /// <returns>支払いが成功したか（残高不足の場合false）</returns>
        public bool PayExpense(float amount, ExpenseCategory category, int facilityId = -1)
        {
            if (amount <= 0f)
            {
                Debug.LogWarning($"[EconomyManager] PayExpense: 不正な金額 {amount}");
                return false;
            }

            // 残高不足でも強制支出（赤字を許容する設計）
            // ただし赤字が続くとゲームオーバー警告を出す
            CurrentBalance -= amount;
            TotalExpensesPaid += amount;

            switch (category)
            {
                case ExpenseCategory.StaffSalary:
                    _currentMonthExpenses.StaffSalaries += amount;
                    break;
                case ExpenseCategory.Maintenance:
                    _currentMonthExpenses.Maintenance += amount;
                    TrackAttractionMaintenanceCost(facilityId, amount);
                    break;
                case ExpenseCategory.Research:
                    _currentMonthExpenses.Research += amount;
                    break;
                case ExpenseCategory.Construction:
                    _currentMonthExpenses.Construction += amount;
                    break;
                case ExpenseCategory.LoanRepayment:
                    _currentMonthExpenses.LoanRepayment += amount;
                    break;
                case ExpenseCategory.Other:
                    _currentMonthExpenses.OtherExpenses += amount;
                    break;
            }

            GameEvents.FireExpensePaid(amount);
            GameEvents.FireMoneyChanged(CurrentBalance);

            if (CurrentBalance < 0f)
            {
                Debug.LogWarning($"[EconomyManager] 赤字状態! 残高: {CurrentBalance:F0}");
            }

            return true;
        }

        /// <summary>指定金額を支払えるか確認する</summary>
        public bool CanAfford(float amount)
        {
            return CurrentBalance >= amount;
        }

        /// <summary>
        /// 汎用の支出メソッド。UI層からカテゴリを意識せずに支出する場合に使用する。
        /// 内部的にはPayExpense(amount, ExpenseCategory.Construction)を呼び出す。
        /// </summary>
        /// <param name="amount">支出額（正の値）</param>
        /// <returns>支払いが成功したか</returns>
        public bool SpendMoney(float amount)
        {
            return PayExpense(amount, ExpenseCategory.Construction);
        }

        // ================================================================
        // ローンシステム
        // ================================================================

        /// <summary>
        /// ローンを借り入れる。
        ///
        /// 【ゲームデザイン: ローンの位置づけ】
        /// ローンは「最後の手段」として設計。利率が高いため、
        /// 計画的な建設投資にのみ使うべき。乱用すると返済地獄に陥る。
        /// </summary>
        /// <param name="amount">借入額</param>
        /// <param name="termMonths">返済期間（月数）。省略時は24ヶ月。</param>
        /// <returns>借入成功時はLoanData、失敗時はnull</returns>
        public LoanData TakeLoan(float amount, int termMonths = DEFAULT_LOAN_TERM_MONTHS)
        {
            // バリデーション
            if (amount <= 0f || amount > MAX_LOAN_AMOUNT)
            {
                Debug.LogWarning($"[EconomyManager] ローン金額が不正: {amount} (上限: {MAX_LOAN_AMOUNT})");
                return null;
            }

            int activeLoanCount = _activeLoans.Count(l => !l.IsFullyRepaid);
            if (activeLoanCount >= MAX_ACTIVE_LOANS)
            {
                Debug.LogWarning($"[EconomyManager] ローン上限に達しています ({MAX_ACTIVE_LOANS}件)");
                return null;
            }

            if (termMonths < 6) termMonths = 6;
            if (termMonths > 60) termMonths = 60;

            // 月利と月々の返済額を計算（元利均等返済）
            float monthlyRate = BASE_LOAN_INTEREST_RATE / 12f;
            float monthlyPayment;

            if (monthlyRate > 0f)
            {
                // 元利均等返済の計算式
                float factor = Mathf.Pow(1f + monthlyRate, termMonths);
                monthlyPayment = amount * (monthlyRate * factor) / (factor - 1f);
            }
            else
            {
                monthlyPayment = amount / termMonths;
            }

            var loan = new LoanData
            {
                LoanId = _nextLoanId++,
                Principal = amount,
                AnnualInterestRate = BASE_LOAN_INTEREST_RATE,
                RemainingBalance = amount + (amount * BASE_LOAN_INTEREST_RATE * termMonths / 12f),
                MonthlyPayment = monthlyPayment,
                TermMonths = termMonths,
                ElapsedMonths = 0
            };

            _activeLoans.Add(loan);
            CurrentBalance += amount;
            GameEvents.FireMoneyChanged(CurrentBalance);

            Debug.Log($"[EconomyManager] ローン借入: {amount:F0} (月額返済: {monthlyPayment:F0}, {termMonths}ヶ月)");
            return loan;
        }

        /// <summary>全アクティブローンの月次返済を実行する</summary>
        private void ProcessMonthlyLoanRepayments()
        {
            foreach (var loan in _activeLoans)
            {
                if (loan.IsFullyRepaid) continue;

                float payment = Mathf.Min(loan.MonthlyPayment, loan.RemainingBalance);
                loan.RemainingBalance -= payment;
                loan.ElapsedMonths++;

                if (loan.RemainingBalance < 0.01f)
                {
                    loan.RemainingBalance = 0f;
                    Debug.Log($"[EconomyManager] ローン#{loan.LoanId} 完済!");
                }

                PayExpense(payment, ExpenseCategory.LoanRepayment);
            }
        }

        /// <summary>ローン一覧を取得する</summary>
        public IReadOnlyList<LoanData> GetActiveLoans()
        {
            return _activeLoans.Where(l => !l.IsFullyRepaid).ToList().AsReadOnly();
        }

        /// <summary>ローンの繰り上げ返済を行う</summary>
        public bool RepayLoanEarly(int loanId, float amount)
        {
            var loan = _activeLoans.FirstOrDefault(l => l.LoanId == loanId && !l.IsFullyRepaid);
            if (loan == null) return false;

            if (!CanAfford(amount)) return false;

            float actualPayment = Mathf.Min(amount, loan.RemainingBalance);
            loan.RemainingBalance -= actualPayment;

            if (loan.RemainingBalance < 0.01f)
            {
                loan.RemainingBalance = 0f;
                Debug.Log($"[EconomyManager] ローン#{loan.LoanId} 繰り上げ完済!");
            }

            PayExpense(actualPayment, ExpenseCategory.LoanRepayment);
            return true;
        }

        // ================================================================
        // アトラクション別収益トラッキング
        // ================================================================

        /// <summary>アトラクションの収益追跡を開始する</summary>
        public void RegisterAttraction(int attractionId, string name)
        {
            if (!_attractionProfits.ContainsKey(attractionId))
            {
                _attractionProfits[attractionId] = new AttractionProfitability
                {
                    AttractionId = attractionId,
                    AttractionName = name,
                    Revenue = 0f,
                    MaintenanceCost = 0f,
                    RiderCount = 0
                };
            }
        }

        /// <summary>アトラクションの収益追跡を解除する（撤去時）</summary>
        public void UnregisterAttraction(int attractionId)
        {
            _attractionProfits.Remove(attractionId);
        }

        /// <summary>アトラクション乗車1回分を記録する</summary>
        public void RecordAttractionRide(int attractionId, float fee)
        {
            if (_attractionProfits.TryGetValue(attractionId, out AttractionProfitability profit))
            {
                profit.RiderCount++;
            }
        }

        private void TrackAttractionRevenue(int facilityId, float amount)
        {
            if (_attractionProfits.TryGetValue(facilityId, out AttractionProfitability profit))
            {
                profit.Revenue += amount;
            }
        }

        private void TrackAttractionMaintenanceCost(int facilityId, float amount)
        {
            if (_attractionProfits.TryGetValue(facilityId, out AttractionProfitability profit))
            {
                profit.MaintenanceCost += amount;
            }
        }

        // ================================================================
        // 月次・年次処理
        // ================================================================

        /// <summary>月末処理。レポート生成とデータリセット。</summary>
        private void OnMonthEnd()
        {
            // ローン返済を処理
            ProcessMonthlyLoanRepayments();

            // 月次レポートを生成
            var report = GenerateMonthlyReport();
            _monthlyReports.Add(report);

            // 収入履歴を更新（グラフ用）
            _revenueHistory.Add(report.Revenue.Total);
            if (_revenueHistory.Count > MAX_REVENUE_HISTORY)
            {
                _revenueHistory.RemoveAt(0);
            }

            Debug.Log($"[EconomyManager] 月次レポート: 収入={report.Revenue.Total:F0}, " +
                      $"支出={report.Expenses.Total:F0}, 利益={report.NetProfit:F0}");

            // 当月データをリセット
            ResetMonthlyTracking();
        }

        /// <summary>年末処理。年次レポートを生成する。</summary>
        private void OnYearEnd(int newYear)
        {
            var yearlyReport = GenerateYearlyReport(newYear - 1);
            if (yearlyReport != null)
            {
                _yearlyReports.Add(yearlyReport);
                Debug.Log($"[EconomyManager] 年次レポート Year{yearlyReport.Year}: " +
                          $"総収入={yearlyReport.TotalRevenue:F0}, 純利益={yearlyReport.NetProfit:F0}");
            }
        }

        /// <summary>月次レポートを生成する</summary>
        private MonthlyFinancialReport GenerateMonthlyReport()
        {
            int year = 1;
            int month = 1;
            if (GameManager.Instance != null && GameManager.Instance.TimeManager != null)
            {
                year = GameManager.Instance.TimeManager.CurrentYear;
                month = GameManager.Instance.TimeManager.CurrentMonth;
            }

            var report = new MonthlyFinancialReport(year, month)
            {
                Revenue = _currentMonthRevenue.Clone(),
                Expenses = _currentMonthExpenses.Clone(),
                TotalVisitors = _currentMonthVisitors,
                AverageVisitorSpending = _currentMonthVisitors > 0
                    ? _currentMonthVisitorSpendingTotal / _currentMonthVisitors
                    : 0f
            };

            // アトラクション別収益をコピー
            foreach (var kvp in _attractionProfits)
            {
                report.AttractionProfits.Add(new AttractionProfitability
                {
                    AttractionId = kvp.Value.AttractionId,
                    AttractionName = kvp.Value.AttractionName,
                    Revenue = kvp.Value.Revenue,
                    MaintenanceCost = kvp.Value.MaintenanceCost,
                    RiderCount = kvp.Value.RiderCount
                });
            }

            return report;
        }

        /// <summary>年次レポートを生成する</summary>
        private YearlyFinancialReport GenerateYearlyReport(int year)
        {
            var monthlyForYear = _monthlyReports.Where(r => r.Year == year).ToList();
            if (monthlyForYear.Count == 0) return null;

            var yearlyReport = new YearlyFinancialReport(year);
            yearlyReport.MonthlyReports.AddRange(monthlyForYear);
            return yearlyReport;
        }

        /// <summary>月次トラッキングデータをリセットする</summary>
        private void ResetMonthlyTracking()
        {
            _currentMonthRevenue.Reset();
            _currentMonthExpenses.Reset();
            _currentMonthVisitors = 0;
            _currentMonthVisitorSpendingTotal = 0f;

            // アトラクション別収益も月次リセット
            foreach (var kvp in _attractionProfits)
            {
                kvp.Value.Revenue = 0f;
                kvp.Value.MaintenanceCost = 0f;
                kvp.Value.RiderCount = 0;
            }
        }

        // ================================================================
        // レポート・統計参照
        // ================================================================

        /// <summary>
        /// 当月の利益を取得する。
        /// </summary>
        public float GetMonthlyProfit()
        {
            return _currentMonthRevenue.Total - _currentMonthExpenses.Total;
        }

        /// <summary>直近の月次レポートを取得する</summary>
        public MonthlyFinancialReport GetLatestMonthlyReport()
        {
            return _monthlyReports.Count > 0 ? _monthlyReports[_monthlyReports.Count - 1] : null;
        }

        /// <summary>指定月の月次レポートを取得する</summary>
        public MonthlyFinancialReport GetMonthlyReport(int year, int month)
        {
            return _monthlyReports.FirstOrDefault(r => r.Year == year && r.Month == month);
        }

        /// <summary>全月次レポートを取得する</summary>
        public IReadOnlyList<MonthlyFinancialReport> GetAllMonthlyReports()
        {
            return _monthlyReports.AsReadOnly();
        }

        /// <summary>指定年の年次レポートを取得する</summary>
        public YearlyFinancialReport GetYearlyReport(int year)
        {
            return _yearlyReports.FirstOrDefault(r => r.Year == year);
        }

        /// <summary>全年次レポートを取得する</summary>
        public IReadOnlyList<YearlyFinancialReport> GetAllYearlyReports()
        {
            return _yearlyReports.AsReadOnly();
        }

        /// <summary>
        /// 収入履歴を取得する（グラフ描画用）。
        /// 直近最大24ヶ月分の月次収入合計のリスト。
        /// </summary>
        public IReadOnlyList<float> GetRevenueHistory()
        {
            return _revenueHistory.AsReadOnly();
        }

        /// <summary>当月の収入内訳を取得する</summary>
        public RevenueBreakdown GetCurrentMonthRevenueBreakdown()
        {
            return _currentMonthRevenue;
        }

        /// <summary>当月の支出内訳を取得する</summary>
        public ExpenseBreakdown GetCurrentMonthExpenseBreakdown()
        {
            return _currentMonthExpenses;
        }

        /// <summary>当月の来場者数を取得する</summary>
        public int GetCurrentMonthVisitorCount()
        {
            return _currentMonthVisitors;
        }

        /// <summary>
        /// 当月の来場者1人あたりの平均支出額を取得する。
        /// </summary>
        public float GetAverageVisitorSpending()
        {
            return _currentMonthVisitors > 0
                ? _currentMonthVisitorSpendingTotal / _currentMonthVisitors
                : 0f;
        }

        /// <summary>アトラクション別の収益データを取得する</summary>
        public IReadOnlyDictionary<int, AttractionProfitability> GetAttractionProfitabilities()
        {
            return _attractionProfits;
        }
    }
}
