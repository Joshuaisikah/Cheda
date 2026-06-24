using Cheda.Core.Analytics;
using Cheda.Core.Bills;
using Cheda.Core.Budgets;
using Cheda.Core.Categorization;
using Cheda.Core.Models;

namespace Cheda.Core.Insights;

/// <summary>
/// Rule-based observations about the user's own data.
/// Each rule is a private method returning zero or one Insight.
/// Rules are neutral observations — no advice, no investment recommendations.
/// </summary>
public sealed class InsightsEngine : IInsightsEngine
{
    private readonly AnalyticsEngine _analytics = new();
    private readonly BudgetEngine    _budgets   = new();
    private readonly BillEngine      _bills     = new();

    public IReadOnlyList<Insight> Generate(
        IReadOnlyList<Transaction> transactions,
        DateRange currentPeriod,
        DateRange previousPeriod,
        IReadOnlyList<Budget> budgets,
        IReadOnlyList<RecurringBill> bills,
        IReadOnlyList<BillOccurrence> billOccurrences,
        DateTimeOffset asOf,
        string? currentPeriodLabel  = null,
        string? previousPeriodLabel = null)
    {
        var cur    = _analytics.GetSummary(transactions, currentPeriod);
        var prv    = _analytics.GetSummary(transactions, previousPeriod);
        var curLbl = currentPeriodLabel  ?? currentPeriod.Start.ToString("MMMM yyyy");
        var prvLbl = previousPeriodLabel ?? previousPeriod.Start.ToString("MMMM yyyy");

        var insights = new List<Insight?>();

        insights.Add(SpendingUp(cur, prv, curLbl, prvLbl, asOf));
        insights.Add(SpendingDown(cur, prv, curLbl, prvLbl, asOf));
        insights.Add(HighFees(cur, curLbl, asOf));
        insights.Add(BettingShare(transactions, currentPeriod, cur, curLbl, asOf));
        insights.Add(FulizaHeavyUse(transactions, currentPeriod, curLbl, asOf));
        insights.Add(LowSavingsRate(cur, curLbl, asOf));
        insights.Add(IncomeDropped(cur, prv, curLbl, prvLbl, asOf));
        insights.Add(PeakSpendingDay(transactions, currentPeriod, curLbl, asOf));
        insights.Add(TopSpendingCategory(transactions, currentPeriod, cur, curLbl, asOf));
        insights.AddRange(BreachedBudgets(budgets, transactions, currentPeriod, curLbl, asOf));
        insights.AddRange(OverdueBills(bills, billOccurrences, asOf));
        insights.Add(UpcomingBillsTotal(bills, billOccurrences, asOf));

        return insights.OfType<Insight>().OrderByDescending(i => i.Severity).ToList();
    }

    // ── Rule: spending up week-over-week ─────────────────────────────────────

    private Insight? SpendingUp(PeriodSummary cur, PeriodSummary prv,
        string curLbl, string prvLbl, DateTimeOffset asOf)
    {
        if (prv.TotalExpenses == 0) return null;
        var pct = (double)((cur.TotalExpenses - prv.TotalExpenses) / prv.TotalExpenses * 100m);
        if (pct < 20) return null;

        return new Insight
        {
            Id          = "spending-up",
            Title       = $"Spending is up in {curLbl}",
            Body        = $"You spent {pct:F0}% more in {curLbl} (Ksh {cur.TotalExpenses:N0}) than in {prvLbl} (Ksh {prv.TotalExpenses:N0}).",
            Severity    = pct >= 50 ? InsightSeverity.Alert : InsightSeverity.Warning,
            Amount      = cur.TotalExpenses,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: spending down (positive observation) ───────────────────────────

    private Insight? SpendingDown(PeriodSummary cur, PeriodSummary prv,
        string curLbl, string prvLbl, DateTimeOffset asOf)
    {
        if (prv.TotalExpenses == 0) return null;
        var pct = (double)((prv.TotalExpenses - cur.TotalExpenses) / prv.TotalExpenses * 100m);
        if (pct < 15) return null;

        return new Insight
        {
            Id          = "spending-down",
            Title       = $"Spending is down in {curLbl}",
            Body        = $"You spent {pct:F0}% less in {curLbl} (Ksh {cur.TotalExpenses:N0}) than in {prvLbl} (Ksh {prv.TotalExpenses:N0}). Great work.",
            Severity    = InsightSeverity.Info,
            Amount      = cur.TotalExpenses,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: high M-Pesa fees ────────────────────────────────────────────────

    private Insight? HighFees(PeriodSummary cur, string curLbl, DateTimeOffset asOf)
    {
        if (cur.TotalExpenses == 0) return null;
        var feePct = (double)(cur.TotalFees / cur.TotalExpenses * 100m);
        if (feePct < 3) return null;

        return new Insight
        {
            Id          = "high-fees",
            Title       = "M-Pesa fees are eating into your spending",
            Body        = $"In {curLbl} you paid Ksh {cur.TotalFees:N0} in M-Pesa transaction fees — {feePct:F1}% of total spending.",
            Severity    = feePct >= 5 ? InsightSeverity.Warning : InsightSeverity.Info,
            Amount      = cur.TotalFees,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: betting share of spending ──────────────────────────────────────

    private Insight? BettingShare(
        IReadOnlyList<Transaction> transactions, DateRange period,
        PeriodSummary cur, string curLbl, DateTimeOffset asOf)
    {
        if (cur.TotalExpenses == 0) return null;
        var bettingTotal = transactions
            .Where(t => period.Contains(t.Timestamp) &&
                        t.Category == DefaultCategories.Betting &&
                        !t.IsNonExpenseTransfer)
            .Sum(t => t.Amount);

        if (bettingTotal == 0) return null;
        var pct = (double)(bettingTotal / cur.TotalExpenses * 100m);
        if (pct < 5) return null;

        return new Insight
        {
            Id          = "betting-share",
            Title       = "Betting is a significant expense in " + curLbl,
            Body        = $"Ksh {bettingTotal:N0} went to betting in {curLbl} — {pct:F1}% of total expenses.",
            Severity    = pct >= 15 ? InsightSeverity.Alert : InsightSeverity.Warning,
            Category    = DefaultCategories.Betting,
            Amount      = bettingTotal,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: heavy Fuliza usage ──────────────────────────────────────────────

    private Insight? FulizaHeavyUse(
        IReadOnlyList<Transaction> transactions, DateRange period, string curLbl, DateTimeOffset asOf)
    {
        var fuliza = _analytics.GetFulizaAnalytics(transactions, period);
        if (fuliza.DrawdownCount < 3) return null;

        return new Insight
        {
            Id          = "fuliza-heavy",
            Title       = $"Heavy Fuliza use in {curLbl}",
            Body        = $"Fuliza was drawn {fuliza.DrawdownCount} times in {curLbl}, borrowing Ksh {fuliza.TotalBorrowed:N0} in total.",
            Severity    = fuliza.DrawdownCount >= 5 ? InsightSeverity.Alert : InsightSeverity.Warning,
            Amount      = fuliza.TotalBorrowed,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: low savings rate ────────────────────────────────────────────────

    private Insight? LowSavingsRate(PeriodSummary cur, string curLbl, DateTimeOffset asOf)
    {
        if (cur.TotalIncome == 0) return null;
        if (cur.SavingsRate >= 10) return null;

        var body = cur.SavingsRate < 0
            ? $"In {curLbl} you spent Ksh {Math.Abs(cur.Net):N0} more than you earned (income Ksh {cur.TotalIncome:N0}, expenses Ksh {cur.TotalExpenses:N0})."
            : $"Your savings rate in {curLbl} is {cur.SavingsRate:F1}% — you kept less than 10% of income.";

        return new Insight
        {
            Id          = "low-savings",
            Title       = cur.SavingsRate < 0 ? $"Spending exceeded income in {curLbl}" : $"Low savings rate in {curLbl}",
            Body        = body,
            Severity    = cur.SavingsRate < 0 ? InsightSeverity.Alert : InsightSeverity.Warning,
            Amount      = cur.Net,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: income dropped vs previous period ───────────────────────────────

    private Insight? IncomeDropped(PeriodSummary cur, PeriodSummary prv,
        string curLbl, string prvLbl, DateTimeOffset asOf)
    {
        if (prv.TotalIncome == 0) return null;
        var pct = (double)((prv.TotalIncome - cur.TotalIncome) / prv.TotalIncome * 100m);
        if (pct < 20) return null;

        return new Insight
        {
            Id          = "income-drop",
            Title       = $"Income dropped in {curLbl}",
            Body        = $"You received Ksh {cur.TotalIncome:N0} in {curLbl} — {pct:F0}% less than {prvLbl} (Ksh {prv.TotalIncome:N0}).",
            Severity    = InsightSeverity.Warning,
            Amount      = cur.TotalIncome,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: peak spending day ───────────────────────────────────────────────

    private Insight? PeakSpendingDay(
        IReadOnlyList<Transaction> transactions, DateRange period, string curLbl, DateTimeOffset asOf)
    {
        var daily = transactions
            .Where(t => period.Contains(t.Timestamp) && !t.IsNonExpenseTransfer && t.Type != TransactionType.Received)
            .GroupBy(t => t.Timestamp.LocalDateTime.Date)
            .Select(g => (Date: g.Key, Total: g.Sum(t => t.Amount)))
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();

        if (daily.Total == 0) return null;

        return new Insight
        {
            Id          = "peak-day",
            Title       = $"Highest spending day in {curLbl}",
            Body        = $"{daily.Date:dddd d MMM} — you spent Ksh {daily.Total:N0} in a single day.",
            Severity    = InsightSeverity.Info,
            Amount      = daily.Total,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: top spending category ───────────────────────────────────────────

    private Insight? TopSpendingCategory(
        IReadOnlyList<Transaction> transactions, DateRange period,
        PeriodSummary cur, string curLbl, DateTimeOffset asOf)
    {
        if (cur.TotalExpenses == 0) return null;

        var top = transactions
            .Where(t => period.Contains(t.Timestamp) && !t.IsNonExpenseTransfer &&
                        t.Type != TransactionType.Received &&
                        t.Category is not null && t.Category != DefaultCategories.Uncategorized)
            .GroupBy(t => t.Category!)
            .Select(g => (Category: g.Key, Total: g.Sum(t => t.Amount)))
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();

        if (top.Total == 0) return null;
        var share = (double)(top.Total / cur.TotalExpenses * 100m);

        return new Insight
        {
            Id          = "top-category",
            Title       = $"{top.Category} is your biggest expense in {curLbl}",
            Body        = $"You spent Ksh {top.Total:N0} on {top.Category} in {curLbl} — {share:F0}% of all your expenses.",
            Severity    = InsightSeverity.Info,
            Category    = top.Category,
            Amount      = top.Total,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: breached budgets ────────────────────────────────────────────────

    private IEnumerable<Insight> BreachedBudgets(
        IReadOnlyList<Budget> budgets,
        IReadOnlyList<Transaction> transactions,
        DateRange period,
        string curLbl,
        DateTimeOffset asOf)
    {
        var breached = _budgets.GetBreachedBudgets(budgets, transactions, period);
        foreach (var status in breached)
        {
            var severity = status.AlertLevel switch
            {
                AlertLevel.Overspent => InsightSeverity.Alert,
                AlertLevel.Red       => InsightSeverity.Warning,
                _                    => InsightSeverity.Info,
            };
            yield return new Insight
            {
                Id          = $"budget-{status.Budget.Category.ToLowerInvariant().Replace(' ', '-')}",
                Title       = $"{status.Budget.Category} budget {(status.IsOverspent ? "exceeded" : "warning")} in {curLbl}",
                Body        = $"Spent Ksh {status.AmountSpent:N0} of your Ksh {status.Budget.MonthlyLimit:N0} {status.Budget.Category} budget in {curLbl} ({status.ProgressPercent:F0}%).",
                Severity    = severity,
                Category    = status.Budget.Category,
                Amount      = status.AmountSpent,
                GeneratedAt = asOf,
            };
        }
    }

    // ── Rule: overdue bills ───────────────────────────────────────────────────

    private IEnumerable<Insight> OverdueBills(
        IReadOnlyList<RecurringBill> bills,
        IReadOnlyList<BillOccurrence> occurrences,
        DateTimeOffset asOf)
    {
        var overdue = _bills.GetOverdue(bills, occurrences, asOf);
        foreach (var upcoming in overdue)
        {
            yield return new Insight
            {
                Id          = $"overdue-{upcoming.Bill.Id}",
                Title       = $"{upcoming.Bill.Label} is overdue",
                Body        = $"{upcoming.Bill.Label} was due on {upcoming.DueDate.LocalDateTime:dd MMM} and hasn't been detected as paid.",
                Severity    = InsightSeverity.Alert,
                Amount      = upcoming.ExpectedAmount,
                GeneratedAt = asOf,
            };
        }
    }

    // ── Rule: upcoming bills summary ─────────────────────────────────────────

    private Insight? UpcomingBillsTotal(
        IReadOnlyList<RecurringBill> bills,
        IReadOnlyList<BillOccurrence> occurrences,
        DateTimeOffset asOf)
    {
        var total    = _bills.GetUpcomingTotal(bills, occurrences, asOf, 7);
        var upcoming = _bills.GetUpcoming(bills, occurrences, asOf, 7);
        if (upcoming.Count == 0) return null;

        return new Insight
        {
            Id          = "upcoming-bills",
            Title       = $"{upcoming.Count} bill{(upcoming.Count > 1 ? "s" : "")} due in the next 7 days",
            Body        = $"Ksh {total:N0} in bills due in the next 7 days: {string.Join(", ", upcoming.Select(u => u.Bill.Label))}.",
            Severity    = InsightSeverity.Info,
            Amount      = total,
            GeneratedAt = asOf,
        };
    }
}
