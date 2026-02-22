// ============================================================
// ThemeParkGame - FinancialReport
// 財務レポートデータクラス（月次・年次の収支サマリー）
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace ThemeParkGame.Economy
{
    /// <summary>
    /// 収入カテゴリ別の金額を保持する。
    /// 3本柱: 入場料・アトラクション料金・ショップ売上
    /// </summary>
    [Serializable]
    public class RevenueBreakdown
    {
        /// <summary>入場料収入</summary>
        public float EntranceFees;

        /// <summary>アトラクション利用料収入</summary>
        public float AttractionFees;

        /// <summary>ショップ売上（飲食・お土産）</summary>
        public float ShopSales;

        /// <summary>その他収入（イベント報酬、VIPボーナス等）</summary>
        public float OtherRevenue;

        /// <summary>収入合計</summary>
        public float Total => EntranceFees + AttractionFees + ShopSales + OtherRevenue;

        public RevenueBreakdown Clone()
        {
            return new RevenueBreakdown
            {
                EntranceFees = this.EntranceFees,
                AttractionFees = this.AttractionFees,
                ShopSales = this.ShopSales,
                OtherRevenue = this.OtherRevenue
            };
        }

        public void Reset()
        {
            EntranceFees = 0f;
            AttractionFees = 0f;
            ShopSales = 0f;
            OtherRevenue = 0f;
        }
    }

    /// <summary>
    /// 支出カテゴリ別の金額を保持する。
    /// </summary>
    [Serializable]
    public class ExpenseBreakdown
    {
        /// <summary>スタッフ給料</summary>
        public float StaffSalaries;

        /// <summary>メンテナンス費用（修理・定期点検）</summary>
        public float Maintenance;

        /// <summary>研究開発費</summary>
        public float Research;

        /// <summary>建設費（アトラクション・施設の新規建設）</summary>
        public float Construction;

        /// <summary>ローン返済（利息含む）</summary>
        public float LoanRepayment;

        /// <summary>その他経費</summary>
        public float OtherExpenses;

        /// <summary>支出合計</summary>
        public float Total => StaffSalaries + Maintenance + Research + Construction + LoanRepayment + OtherExpenses;

        public ExpenseBreakdown Clone()
        {
            return new ExpenseBreakdown
            {
                StaffSalaries = this.StaffSalaries,
                Maintenance = this.Maintenance,
                Research = this.Research,
                Construction = this.Construction,
                LoanRepayment = this.LoanRepayment,
                OtherExpenses = this.OtherExpenses
            };
        }

        public void Reset()
        {
            StaffSalaries = 0f;
            Maintenance = 0f;
            Research = 0f;
            Construction = 0f;
            LoanRepayment = 0f;
            OtherExpenses = 0f;
        }
    }

    /// <summary>
    /// アトラクション単体の収益情報。
    /// 各アトラクションがどれだけ稼いでいるかを追跡する。
    /// </summary>
    [Serializable]
    public class AttractionProfitability
    {
        public int AttractionId;
        public string AttractionName;

        /// <summary>乗車料金収入</summary>
        public float Revenue;

        /// <summary>メンテナンス費用</summary>
        public float MaintenanceCost;

        /// <summary>利用者数</summary>
        public int RiderCount;

        /// <summary>純利益</summary>
        public float Profit => Revenue - MaintenanceCost;

        /// <summary>乗客1人あたりの利益</summary>
        public float ProfitPerRider => RiderCount > 0 ? Profit / RiderCount : 0f;
    }

    /// <summary>
    /// 月次財務レポート。1ヶ月分の収支サマリーを保持する。
    /// EconomyManagerが月末に自動生成し、履歴として保存する。
    /// </summary>
    [Serializable]
    public class MonthlyFinancialReport
    {
        /// <summary>レポート対象年</summary>
        public int Year;

        /// <summary>レポート対象月</summary>
        public int Month;

        /// <summary>収入内訳</summary>
        public RevenueBreakdown Revenue;

        /// <summary>支出内訳</summary>
        public ExpenseBreakdown Expenses;

        /// <summary>月間来場者数</summary>
        public int TotalVisitors;

        /// <summary>来場者1人あたりの平均支出額</summary>
        public float AverageVisitorSpending;

        /// <summary>アトラクション別収益データ</summary>
        public List<AttractionProfitability> AttractionProfits;

        /// <summary>月間純利益</summary>
        public float NetProfit => Revenue.Total - Expenses.Total;

        /// <summary>利益率（%）</summary>
        public float ProfitMargin => Revenue.Total > 0f ? (NetProfit / Revenue.Total) * 100f : 0f;

        public MonthlyFinancialReport()
        {
            Revenue = new RevenueBreakdown();
            Expenses = new ExpenseBreakdown();
            AttractionProfits = new List<AttractionProfitability>();
        }

        public MonthlyFinancialReport(int year, int month) : this()
        {
            Year = year;
            Month = month;
        }
    }

    /// <summary>
    /// 年次財務レポート。12ヶ月分のデータを集約し、年間トレンドを提供する。
    /// </summary>
    [Serializable]
    public class YearlyFinancialReport
    {
        /// <summary>レポート対象年</summary>
        public int Year;

        /// <summary>月次レポートの一覧</summary>
        public List<MonthlyFinancialReport> MonthlyReports;

        /// <summary>年間総収入</summary>
        public float TotalRevenue => MonthlyReports.Sum(r => r.Revenue.Total);

        /// <summary>年間総支出</summary>
        public float TotalExpenses => MonthlyReports.Sum(r => r.Expenses.Total);

        /// <summary>年間純利益</summary>
        public float NetProfit => TotalRevenue - TotalExpenses;

        /// <summary>年間来場者数</summary>
        public int TotalVisitors => MonthlyReports.Sum(r => r.TotalVisitors);

        /// <summary>年間来場者平均支出</summary>
        public float AverageVisitorSpending
        {
            get
            {
                int visitors = TotalVisitors;
                return visitors > 0 ? TotalRevenue / visitors : 0f;
            }
        }

        /// <summary>
        /// 月次利益のトレンド（12ヶ月分）。グラフ描画用。
        /// </summary>
        public List<float> MonthlyProfitTrend => MonthlyReports.Select(r => r.NetProfit).ToList();

        /// <summary>
        /// 月次収入のトレンド（12ヶ月分）。グラフ描画用。
        /// </summary>
        public List<float> MonthlyRevenueTrend => MonthlyReports.Select(r => r.Revenue.Total).ToList();

        /// <summary>最も利益の高かった月</summary>
        public int BestMonth
        {
            get
            {
                if (MonthlyReports.Count == 0) return 0;
                return MonthlyReports.OrderByDescending(r => r.NetProfit).First().Month;
            }
        }

        /// <summary>最も利益の低かった月</summary>
        public int WorstMonth
        {
            get
            {
                if (MonthlyReports.Count == 0) return 0;
                return MonthlyReports.OrderBy(r => r.NetProfit).First().Month;
            }
        }

        public YearlyFinancialReport()
        {
            MonthlyReports = new List<MonthlyFinancialReport>();
        }

        public YearlyFinancialReport(int year) : this()
        {
            Year = year;
        }

        /// <summary>
        /// 前年比成長率を計算する（%）。
        /// </summary>
        public float CalculateGrowthRate(YearlyFinancialReport previousYear)
        {
            if (previousYear == null || previousYear.TotalRevenue <= 0f) return 0f;
            return ((TotalRevenue - previousYear.TotalRevenue) / previousYear.TotalRevenue) * 100f;
        }
    }
}
