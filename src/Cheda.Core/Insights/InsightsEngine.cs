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
        DateTimeOffset asOf)
    {
        var cur = _analytics.GetSummary(transactions, currentPeriod);
        var prv = _analytics.GetSummary(transactions, previousPeriod);

        var insights = new List<Insight?>();

        insights.Add(SpendingUp(cur, prv, asOf));
        insights.Add(SpendingDown(cur, prv, asOf));
        insights.Add(HighFees(cur, asOf));
        insights.Add(BettingShare(transactions, currentPeriod, cur, asOf));
        insights.Add(FulizaHeavyUse(transactions, currentPeriod, asOf));
        insights.Add(LowSavingsRate(cur, asOf));
        insights.Add(IncomeDropped(cur, prv, asOf));
        insights.AddRange(BreachedBudgets(budgets, transactions, currentPeriod, asOf));
        insights.AddRange(OverdueBills(bills, billOccurrences, asOf));
        insights.Add(UpcomingBillsTotal(bills, billOccurrences, asOf));

        return insights.OfType<Insight>().OrderByDescending(i => i.Severity).ToList();
    }

    // ── Rule: spending up week-over-week ─────────────────────────────────────

    private Insight? SpendingUp(PeriodSummary cur, PeriodSummary prv, DateTimeOffset asOf)
    {
        if (prv.TotalExpenses == 0) return null;
        var pct = (double)((cur.TotalExpenses - prv.TotalExpenses) / prv.TotalExpenses * 100m);
        if (pct < 20) return null;

        return new Insight
        {
            Id          = "spending-up",
            Title       = "Spending is up this period",
            Body        = $"You've spent {pct:F0}% more than the previous period (Ksh {cur.TotalExpenses:N0} vs Ksh {prv.TotalExpenses:N0}).",
            Severity    = pct >= 50 ? InsightSeverity.Alert : InsightSeverity.Warning,
            Amount      = cur.TotalExpenses,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: spending down (positive observation) ───────────────────────────

    private Insight? SpendingDown(PeriodSummary cur, PeriodSummary prv, DateTimeOffset asOf)
    {
        if (prv.TotalExpenses == 0) return null;
        var pct = (double)((prv.TotalExpenses - cur.TotalExpenses) / prv.TotalExpenses * 100m);
        if (pct < 15) return null;

        return new Insight
        {
            Id          = "spending-down",
            Title       = "Spending is down this period",
            Body        = $"You spent {pct:F0}% less than the previous period (Ksh {cur.TotalExpenses:N0} vs Ksh {prv.TotalExpenses:N0}).",
            Severity    = InsightSeverity.Info,
            Amount      = cur.TotalExpenses,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: high M-Pesa fees ────────────────────────────────────────────────

    private Insight? HighFees(PeriodSummary cur, DateTimeOffset asOf)
    {
        if (cur.TotalExpenses == 0) return null;
        var feePct = (double)(cur.TotalFees / cur.TotalExpenses * 100m);
        if (feePct < 3) return null;

        return new Insight
        {
            Id          = "high-fees",
            Title       = "M-Pesa transaction fees are notable",
            Body        = $"You paid Ksh {cur.TotalFees:N0} in M-Pesa fees this period ({feePct:F1}% of total spending).",
            Severity    = feePct >= 5 ? InsightSeverity.Warning : InsightSeverity.Info,
            Amount      = cur.TotalFees,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: betting share of spending ──────────────────────────────────────

    private Insight? BettingShare(
        IReadOnlyList<Transaction> transactions, DateRange period, PeriodSummary cur, DateTimeOffset asOf)
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
            Title       = "Betting represents a significant share of spending",
            Body        = $"Ksh {bettingTotal:N0} went to betting this period — {pct:F1}% of total expenses.",
            Severity    = pct >= 15 ? InsightSeverity.Alert : InsightSeverity.Warning,
            Category    = DefaultCategories.Betting,
            Amount      = bettingTotal,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: heavy Fuliza usage ──────────────────────────────────────────────

    private Insight? FulizaHeavyUse(
        IReadOnlyList<Transaction> transactions, DateRange period, DateTimeOffset asOf)
    {
        var fuliza = _analytics.GetFulizaAnalytics(transactions, period);
        if (fuliza.DrawdownCount < 3) return null;

        return new Insight
        {
            Id          = "fuliza-heavy",
            Title       = "Frequent Fuliza use this period",
            Body        = $"Fuliza was used {fuliza.DrawdownCount} times, borrowing Ksh {fuliza.TotalBorrowed:N0} in total.",
            Severity    = fuliza.DrawdownCount >= 5 ? InsightSeverity.Alert : InsightSeverity.Warning,
            Amount      = fuliza.TotalBorrowed,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: low savings rate ────────────────────────────────────────────────

    private Insight? LowSavingsRate(PeriodSummary cur, DateTimeOffset asOf)
    {
        if (cur.TotalIncome == 0) return null;
        if (cur.SavingsRate >= 10) return null;

        var body = cur.SavingsRate < 0
            ? $"You spent Ksh {Math.Abs(cur.Net):N0} more than you earned this period."
            : $"Your savings rate is {cur.SavingsRate:F1}% — less than 10% of income.";

        return new Insight
        {
            Id          = "low-savings",
            Title       = cur.SavingsRate < 0 ? "Spending exceeded income" : "Low savings rate",
            Body        = body,
            Severity    = cur.SavingsRate < 0 ? InsightSeverity.Alert : InsightSeverity.Warning,
            Amount      = cur.Net,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: income dropped vs previous period ───────────────────────────────

    private Insight? IncomeDropped(PeriodSummary cur, PeriodSummary prv, DateTimeOffset asOf)
    {
        if (prv.TotalIncome == 0) return null;
        var pct = (double)((prv.TotalIncome - cur.TotalIncome) / prv.TotalIncome * 100m);
        if (pct < 20) return null;

        return new Insight
        {
            Id          = "income-drop",
            Title       = "Income is lower than last period",
            Body        = $"Income this period is Ksh {cur.TotalIncome:N0} — {pct:F0}% less than the previous period (Ksh {prv.TotalIncome:N0}).",
            Severity    = InsightSeverity.Warning,
            Amount      = cur.TotalIncome,
            GeneratedAt = asOf,
        };
    }

    // ── Rule: breached budgets ────────────────────────────────────────────────

    private IEnumerable<Insight> BreachedBudgets(
        IReadOnlyList<Budget> budgets,
        IReadOnlyList<Transaction> transactions,
        DateRange period,
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
                Title       = $"{status.Budget.Category} budget {(status.IsOverspent ? "exceeded" : "warning")}",
                Body        = $"Spent Ksh {status.AmountSpent:N0} of Ksh {status.Budget.MonthlyLimit:N0} budget ({status.ProgressPercent:F0}%).",
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
