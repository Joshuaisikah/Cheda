using Cheda.Core.Categorization;
using Cheda.Core.Models;

namespace Cheda.Core.Analytics;

/// <summary>
/// Pure, stateless analytics engine. Pass transactions in, get analytics out.
/// No storage, no side effects — fully unit-testable.
/// </summary>
public sealed class AnalyticsEngine : IAnalyticsEngine
{
    // ── Classification helpers ───────────────────────────────────────────────

    private static bool IsIncome(Transaction t) =>
        t.Type == TransactionType.Received && !t.IsNonExpenseTransfer;

    private static bool IsExpense(Transaction t) =>
        !t.IsNonExpenseTransfer &&
        t.Type is TransactionType.Sent
               or TransactionType.PaidTill
               or TransactionType.PaidPaybill
               or TransactionType.Airtime;

    /// <summary>
    /// Returns true for savings-product transfers that move money but should not
    /// appear as an expense or income in any spending/income total.
    /// </summary>
    public static bool IsSavingsTransfer(Transaction t) =>
        t.Type is TransactionType.MShwari
               or TransactionType.KcbMpesa
               or TransactionType.Zidii;

    // ── Summary ──────────────────────────────────────────────────────────────

    public PeriodSummary GetSummary(IReadOnlyList<Transaction> transactions, DateRange range)
    {
        var inRange = transactions.Where(t => range.Contains(t.Timestamp)).ToList();

        var income     = inRange.Where(IsIncome).Sum(t => t.Amount);
        var grossExp   = inRange.Where(IsExpense).Sum(t => t.Amount);
        var reversals  = inRange.Where(t => t.Type == TransactionType.Reversal).Sum(t => t.Amount);
        var netExp     = grossExp - reversals;
        var fees       = inRange.Sum(t => t.TransactionCost ?? 0m);
        var days       = Math.Max(1.0, range.TotalDays);
        var avgDaily   = netExp / (decimal)days;
        var savingsRate = income > 0 ? (double)((income - netExp) / income * 100m) : 0.0;

        var balance = inRange
            .Where(t => t.BalanceAfter.HasValue && t.Type != TransactionType.Fuliza)
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefault()?.BalanceAfter;

        return new PeriodSummary
        {
            Range                  = range,
            TotalIncome            = income,
            TotalExpenses          = netExp,
            TotalReversals         = reversals,
            Net                    = income - netExp,
            TotalFees              = fees,
            AverageDailySpend      = avgDaily,
            SavingsRate            = savingsRate,
            CurrentBalance         = balance,
            TransactionCount       = inRange.Count,
            IncomeTransactionCount = inRange.Count(IsIncome),
            ExpenseTransactionCount = inRange.Count(IsExpense),
        };
    }

    // ── Category breakdown ───────────────────────────────────────────────────

    public IReadOnlyList<CategoryBreakdown> GetCategoryBreakdown(
        IReadOnlyList<Transaction> transactions, DateRange range)
    {
        var expenses = transactions
            .Where(t => range.Contains(t.Timestamp) && IsExpense(t))
            .ToList();

        var total = expenses.Sum(t => t.Amount);

        return expenses
            .GroupBy(t => t.Category ?? DefaultCategories.Uncategorized)
            .Select(g =>
            {
                var groupTotal = g.Sum(t => t.Amount);
                return new CategoryBreakdown
                {
                    Category         = g.Key,
                    Total            = groupTotal,
                    Percentage       = total > 0 ? (double)(groupTotal / total * 100m) : 0,
                    TransactionCount = g.Count(),
                };
            })
            .OrderByDescending(b => b.Total)
            .ToList();
    }

    // ── Trend ────────────────────────────────────────────────────────────────

    public IReadOnlyList<TrendPoint> GetTrend(
        IReadOnlyList<Transaction> transactions, DateRange range, TrendGranularity granularity)
    {
        var inRange = transactions.Where(t => range.Contains(t.Timestamp)).ToList();

        var groups = granularity switch
        {
            TrendGranularity.Daily   => inRange.GroupBy(t => t.Timestamp.LocalDateTime.Date),
            TrendGranularity.Weekly  => inRange.GroupBy(t => StartOfWeek(t.Timestamp.LocalDateTime.Date)),
            TrendGranularity.Monthly => inRange.GroupBy(t =>
                new DateTime(t.Timestamp.LocalDateTime.Year, t.Timestamp.LocalDateTime.Month, 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(granularity)),
        };

        return groups
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var txs       = g.ToList();
                var income    = txs.Where(IsIncome).Sum(t => t.Amount);
                var reversals = txs.Where(t => t.Type == TransactionType.Reversal).Sum(t => t.Amount);
                var expenses  = txs.Where(IsExpense).Sum(t => t.Amount) - reversals;
                var fees      = txs.Sum(t => t.TransactionCost ?? 0m);
                return new TrendPoint
                {
                    PeriodStart = new DateTimeOffset(g.Key, range.Start.Offset),
                    Income      = income,
                    Expenses    = expenses,
                    Net         = income - expenses,
                    Fees        = fees,
                };
            })
            .ToList();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var dow = (int)date.DayOfWeek;
        return date.AddDays(dow == 0 ? -6 : 1 - dow);
    }

    // ── Fee analytics ────────────────────────────────────────────────────────

    public FeeAnalytics GetFeeAnalytics(IReadOnlyList<Transaction> transactions, DateRange range)
    {
        var inRange = transactions
            .Where(t => range.Contains(t.Timestamp) && t.TransactionCost is > 0)
            .ToList();

        var byType = inRange
            .GroupBy(t => t.Type.ToString())
            .Select(g => new FeeBreakdownItem(
                g.Key,
                g.Sum(t => t.TransactionCost ?? 0m),
                g.Count()))
            .OrderByDescending(x => x.Total)
            .ToList();

        return new FeeAnalytics
        {
            TotalFees = inRange.Sum(t => t.TransactionCost ?? 0m),
            ByType    = byType,
        };
    }

    // ── Fuliza analytics ─────────────────────────────────────────────────────

    public FulizaAnalytics GetFulizaAnalytics(IReadOnlyList<Transaction> transactions, DateRange range)
    {
        var fuliza = transactions
            .Where(t => range.Contains(t.Timestamp) && t.Type == TransactionType.Fuliza)
            .OrderBy(t => t.Timestamp)
            .ToList();

        if (fuliza.Count == 0)
            return new FulizaAnalytics();

        var months = Math.Max(1.0, range.TotalDays / 30.0);

        return new FulizaAnalytics
        {
            DrawdownCount         = fuliza.Count,
            TotalBorrowed         = fuliza.Sum(t => t.Amount),
            TotalFees             = fuliza.Sum(t => t.TransactionCost ?? 0m),
            EstimatedOutstanding  = fuliza.Last().BalanceAfter,
            UsageFrequencyPerMonth = fuliza.Count / months,
        };
    }

    // ── Top counterparties ───────────────────────────────────────────────────

    public IReadOnlyList<TopCounterparty> GetTopCounterparties(
        IReadOnlyList<Transaction> transactions, DateRange range, int top = 10) =>
        transactions
            .Where(t => range.Contains(t.Timestamp) && IsExpense(t) && t.Counterparty is not null)
            .GroupBy(t => t.Counterparty!)
            .Select(g => new TopCounterparty(g.Key, g.Sum(t => t.Amount), g.Count()))
            .OrderByDescending(c => c.Total)
            .Take(top)
            .ToList();

    // ── Biggest transactions ─────────────────────────────────────────────────

    public IReadOnlyList<Transaction> GetBiggestTransactions(
        IReadOnlyList<Transaction> transactions, DateRange range, int top = 10) =>
        transactions
            .Where(t => range.Contains(t.Timestamp) && IsExpense(t))
            .OrderByDescending(t => t.Amount)
            .Take(top)
            .ToList();

    // ── Month-over-month / week-over-week ────────────────────────────────────

    public PeriodComparison GetMonthOverMonth(
        IReadOnlyList<Transaction> transactions, int year, int month, TimeSpan offset = default)
    {
        var current  = DateRange.ForMonth(year, month, offset);
        var prevDate = new DateTimeOffset(year, month, 1, 0, 0, 0, offset).AddMonths(-1);
        var previous = DateRange.ForMonth(prevDate.Year, prevDate.Month, offset);

        return BuildComparison(transactions, current, previous);
    }

    public PeriodComparison GetWeekOverWeek(
        IReadOnlyList<Transaction> transactions, DateTimeOffset anyDayInWeek)
    {
        var current  = DateRange.ForWeek(anyDayInWeek);
        var previous = DateRange.ForWeek(anyDayInWeek.AddDays(-7));
        return BuildComparison(transactions, current, previous);
    }

    private PeriodComparison BuildComparison(
        IReadOnlyList<Transaction> transactions, DateRange current, DateRange previous)
    {
        var cur = GetSummary(transactions, current);
        var prv = GetSummary(transactions, previous);

        return new PeriodComparison
        {
            Current              = cur,
            Previous             = prv,
            ExpenseChange        = cur.TotalExpenses - prv.TotalExpenses,
            ExpenseChangePercent = prv.TotalExpenses > 0
                ? (double)((cur.TotalExpenses - prv.TotalExpenses) / prv.TotalExpenses * 100m)
                : 0,
            IncomeChange         = cur.TotalIncome - prv.TotalIncome,
            IncomeChangePercent  = prv.TotalIncome > 0
                ? (double)((cur.TotalIncome - prv.TotalIncome) / prv.TotalIncome * 100m)
                : 0,
        };
    }

    // ── Current balance ──────────────────────────────────────────────────────

    public decimal? GetCurrentBalance(IReadOnlyList<Transaction> transactions) =>
        transactions
            .Where(t => t.BalanceAfter.HasValue && t.Type != TransactionType.Fuliza)
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefault()?.BalanceAfter;
}
