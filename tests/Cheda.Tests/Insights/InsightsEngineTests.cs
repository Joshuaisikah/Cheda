using Cheda.Core.Analytics;
using Cheda.Core.Bills;
using Cheda.Core.Budgets;
using Cheda.Core.Categorization;
using Cheda.Core.Insights;
using Cheda.Core.Models;
using FluentAssertions;

namespace Cheda.Tests.Insights;

public class InsightsEngineTests
{
    private readonly InsightsEngine _engine = new();
    private static readonly TimeSpan Eat = TimeSpan.FromHours(3);
    private static readonly DateTimeOffset AsOf = new(2025, 6, 15, 12, 0, 0, Eat);
    private static readonly DateRange June = DateRange.ForMonth(2025, 6, Eat);
    private static readonly DateRange May  = DateRange.ForMonth(2025, 5, Eat);

    private IReadOnlyList<Insight> Run(
        IReadOnlyList<Transaction>? txs = null,
        IReadOnlyList<Budget>? budgets = null,
        IReadOnlyList<RecurringBill>? bills = null,
        IReadOnlyList<BillOccurrence>? occurrences = null) =>
        _engine.Generate(
            txs ?? [],
            June, May,
            budgets ?? [],
            bills ?? [],
            occurrences ?? [],
            AsOf);

    private static Transaction Tx(
        TransactionType type, decimal amount, string? category = null,
        int month = 6, int day = 5, bool nonExpense = false) => new()
    {
        TransactionCode      = Guid.NewGuid().ToString("N")[..10].ToUpper(),
        Source               = TransactionSource.Mpesa,
        Amount               = amount,
        Type                 = type,
        Timestamp            = new DateTimeOffset(2025, month, day, 10, 0, 0, Eat),
        RawMessage           = "",
        Category             = category,
        IsNonExpenseTransfer = nonExpense,
    };

    // ── Spending up ───────────────────────────────────────────────────────────

    [Fact]
    public void SpendingUp_MoreThan20Pct_GeneratesWarning()
    {
        var txs = new[]
        {
            Tx(TransactionType.PaidTill, 10_000m, month: 6),   // current
            Tx(TransactionType.PaidTill,  5_000m, month: 5),   // previous (50% less)
        };

        var insights = Run(txs);
        insights.Should().Contain(i => i.Id == "spending-up");
    }

    [Fact]
    public void SpendingUp_LessThan20Pct_NoInsight()
    {
        var txs = new[]
        {
            Tx(TransactionType.PaidTill, 5_100m, month: 6),
            Tx(TransactionType.PaidTill, 5_000m, month: 5),
        };
        Run(txs).Should().NotContain(i => i.Id == "spending-up");
    }

    // ── Spending down ─────────────────────────────────────────────────────────

    [Fact]
    public void SpendingDown_MoreThan15PctReduction_GeneratesInfo()
    {
        var txs = new[]
        {
            Tx(TransactionType.PaidTill, 3_000m, month: 6),
            Tx(TransactionType.PaidTill, 5_000m, month: 5),
        };
        var insights = Run(txs);
        insights.Should().Contain(i => i.Id == "spending-down");
        insights.First(i => i.Id == "spending-down").Severity.Should().Be(InsightSeverity.Info);
    }

    // ── High fees ─────────────────────────────────────────────────────────────

    [Fact]
    public void HighFees_FeesOver3PctOfSpend_GeneratesInsight()
    {
        var txs = new[]
        {
            new Transaction
            {
                TransactionCode = "FEEHIGH001",
                Source          = TransactionSource.Mpesa,
                Amount          = 1_000m,
                Type            = TransactionType.Sent,
                TransactionCost = 50m,   // 5% fee
                Timestamp       = new DateTimeOffset(2025, 6, 5, 10, 0, 0, Eat),
                RawMessage      = "",
            },
        };
        Run(txs).Should().Contain(i => i.Id == "high-fees");
    }

    // ── Betting share ─────────────────────────────────────────────────────────

    [Fact]
    public void BettingShare_Over5PctOfSpend_GeneratesWarning()
    {
        var txs = new[]
        {
            Tx(TransactionType.PaidTill, 10_000m, DefaultCategories.Groceries,  month: 6),
            Tx(TransactionType.Sent,      1_000m, DefaultCategories.Betting,    month: 6),
        };
        var insights = Run(txs);
        insights.Should().Contain(i => i.Id == "betting-share");
    }

    [Fact]
    public void BettingShare_NoBetting_NoInsight()
    {
        var txs = new[] { Tx(TransactionType.PaidTill, 10_000m, DefaultCategories.Groceries) };
        Run(txs).Should().NotContain(i => i.Id == "betting-share");
    }

    // ── Fuliza heavy use ──────────────────────────────────────────────────────

    [Fact]
    public void FulizaHeavyUse_ThreeOrMoreDrawdowns_GeneratesWarning()
    {
        var txs = new[]
        {
            Tx(TransactionType.Fuliza, 200m, month: 6, day: 2),
            Tx(TransactionType.Fuliza, 300m, month: 6, day: 8),
            Tx(TransactionType.Fuliza, 150m, month: 6, day: 14),
        };
        Run(txs).Should().Contain(i => i.Id == "fuliza-heavy");
    }

    [Fact]
    public void FulizaHeavyUse_TwoDrawdowns_NoInsight()
    {
        var txs = new[]
        {
            Tx(TransactionType.Fuliza, 200m, month: 6, day: 2),
            Tx(TransactionType.Fuliza, 300m, month: 6, day: 8),
        };
        Run(txs).Should().NotContain(i => i.Id == "fuliza-heavy");
    }

    // ── Low savings rate ──────────────────────────────────────────────────────

    [Fact]
    public void LowSavingsRate_SpendingExceedsIncome_GeneratesAlert()
    {
        var txs = new[]
        {
            Tx(TransactionType.Received, 10_000m, month: 6),
            Tx(TransactionType.PaidTill, 12_000m, month: 6),
        };
        var insights = Run(txs);
        var insight = insights.FirstOrDefault(i => i.Id == "low-savings");
        insight.Should().NotBeNull();
        insight!.Severity.Should().Be(InsightSeverity.Alert);
    }

    [Fact]
    public void LowSavingsRate_GoodSavingsRate_NoInsight()
    {
        var txs = new[]
        {
            Tx(TransactionType.Received, 50_000m, month: 6),
            Tx(TransactionType.PaidTill,  5_000m, month: 6),
        };
        Run(txs).Should().NotContain(i => i.Id == "low-savings");
    }

    // ── Income drop ───────────────────────────────────────────────────────────

    [Fact]
    public void IncomeDrop_Over20Pct_GeneratesWarning()
    {
        var txs = new[]
        {
            Tx(TransactionType.Received, 30_000m, month: 6),
            Tx(TransactionType.Received, 50_000m, month: 5),
        };
        Run(txs).Should().Contain(i => i.Id == "income-drop");
    }

    // ── Budget breaches ───────────────────────────────────────────────────────

    [Fact]
    public void BreachedBudget_Overspent_GeneratesAlert()
    {
        var budget = new Budget
        {
            Category     = DefaultCategories.Betting,
            MonthlyLimit = 1_000m,
        };
        var txs = new[] { Tx(TransactionType.Sent, 1_500m, DefaultCategories.Betting, month: 6) };

        var insights = Run(txs, budgets: [budget]);
        insights.Should().Contain(i =>
            i.Id.StartsWith("budget-") && i.Severity == InsightSeverity.Alert);
    }

    // ── Overdue bills ─────────────────────────────────────────────────────────

    [Fact]
    public void OverdueBill_UnpaidPastDue_GeneratesAlert()
    {
        var bill = new RecurringBill
        {
            Label          = "KPLC",
            PaymentKey     = "888880",
            PaymentKeyType = BillPaymentKeyType.Paybill,
            ExpectedAmount = 2_000m,
            Schedule       = BillSchedule.Monthly,
            DayOfMonth     = 5,
        };

        // asOf is June 15, bill was due June 5, no paid occurrence
        var insights = Run(bills: [bill]);
        insights.Should().Contain(i => i.Id == $"overdue-{bill.Id}");
        insights.First(i => i.Id == $"overdue-{bill.Id}").Severity
            .Should().Be(InsightSeverity.Alert);
    }

    // ── Upcoming bills ────────────────────────────────────────────────────────

    [Fact]
    public void UpcomingBills_DueInWindow_GeneratesInfo()
    {
        var bill = new RecurringBill
        {
            Label          = "Rent",
            PaymentKey     = "123456",
            PaymentKeyType = BillPaymentKeyType.Paybill,
            ExpectedAmount = 15_000m,
            Schedule       = BillSchedule.Monthly,
            DayOfMonth     = 20,  // 5 days from asOf (June 15)
        };

        var insights = Run(bills: [bill]);
        insights.Should().Contain(i => i.Id == "upcoming-bills");
    }

    // ── Ordering ──────────────────────────────────────────────────────────────

    [Fact]
    public void Insights_OrderedBySeverityDescending()
    {
        var txs = new[]
        {
            Tx(TransactionType.Received, 10_000m, month: 6),
            Tx(TransactionType.PaidTill, 12_000m, month: 6),    // triggers low-savings Alert
            Tx(TransactionType.PaidTill,  5_000m, month: 5),    // triggers spending-up Warning
            Tx(TransactionType.Received, 50_000m, month: 5),
        };

        var insights = Run(txs);
        var severities = insights.Select(i => (int)i.Severity).ToList();
        severities.Should().BeInDescendingOrder();
    }

    // ── Full fixture integration ──────────────────────────────────────────────

    [Fact]
    public void Generate_WithAnalyticsFixtures_DoesNotThrow()
    {
        // Fixture data is healthy (good savings rate, modest fee share, small spend change)
        // so not every rule fires — but the engine must run without error and return a list.
        var act = () => _engine.Generate(
            Analytics.AnalyticsFixtures.All,
            June, May,
            [], [], [],
            AsOf);

        act.Should().NotThrow();
        var insights = act();
        insights.Should().NotBeNull();
        // If any insights fired they must be ordered by severity descending
        if (insights.Count > 1)
            insights.Select(i => (int)i.Severity)
                    .Should().BeInDescendingOrder();
    }
}
