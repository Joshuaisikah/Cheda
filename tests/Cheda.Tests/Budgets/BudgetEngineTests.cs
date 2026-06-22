using Cheda.Core.Analytics;
using Cheda.Core.Budgets;
using Cheda.Core.Categorization;
using Cheda.Core.Models;
using FluentAssertions;

namespace Cheda.Tests.Budgets;

public class BudgetEngineTests
{
    private readonly BudgetEngine _engine = new();
    private static readonly TimeSpan Eat = TimeSpan.FromHours(3);
    private static readonly DateRange June = DateRange.ForMonth(2025, 6, Eat);

    private static Transaction Expense(string category, decimal amount, int day = 5) => new()
    {
        TransactionCode = Guid.NewGuid().ToString("N")[..10].ToUpper(),
        Source          = TransactionSource.Mpesa,
        Amount          = amount,
        Type            = TransactionType.PaidTill,
        Timestamp       = new DateTimeOffset(2025, 6, day, 10, 0, 0, Eat),
        RawMessage      = "",
        Category        = category,
    };

    private static Budget FoodBudget(decimal limit = 5_000m) => new()
    {
        Category             = DefaultCategories.Groceries,
        MonthlyLimit         = limit,
        AmberThresholdPercent = 75.0,
        RedThresholdPercent   = 90.0,
    };

    // ── Alert levels ──────────────────────────────────────────────────────────

    [Fact]
    public void GetStatus_NoSpend_AlertIsNone()
    {
        var status = _engine.GetStatus(FoodBudget(5_000m), [], June);
        status.AlertLevel.Should().Be(AlertLevel.None);
        status.AmountSpent.Should().Be(0m);
        status.ProgressPercent.Should().Be(0.0);
    }

    [Fact]
    public void GetStatus_BelowAmber_AlertIsNone()
    {
        var txs = new[] { Expense(DefaultCategories.Groceries, 3_000m) }; // 60%
        var status = _engine.GetStatus(FoodBudget(5_000m), txs, June);
        status.AlertLevel.Should().Be(AlertLevel.None);
        status.ProgressPercent.Should().BeApproximately(60.0, 0.01);
    }

    [Fact]
    public void GetStatus_AtAmberThreshold_AlertIsAmber()
    {
        var txs = new[] { Expense(DefaultCategories.Groceries, 3_750m) }; // exactly 75%
        var status = _engine.GetStatus(FoodBudget(5_000m), txs, June);
        status.AlertLevel.Should().Be(AlertLevel.Amber);
    }

    [Fact]
    public void GetStatus_AtRedThreshold_AlertIsRed()
    {
        var txs = new[] { Expense(DefaultCategories.Groceries, 4_500m) }; // exactly 90%
        var status = _engine.GetStatus(FoodBudget(5_000m), txs, June);
        status.AlertLevel.Should().Be(AlertLevel.Red);
    }

    [Fact]
    public void GetStatus_Overspent_AlertIsOverspent()
    {
        var txs = new[] { Expense(DefaultCategories.Groceries, 5_500m) }; // 110%
        var status = _engine.GetStatus(FoodBudget(5_000m), txs, June);
        status.AlertLevel.Should().Be(AlertLevel.Overspent);
        status.IsOverspent.Should().BeTrue();
        status.AmountRemaining.Should().BeNegative();
    }

    // ── Spend calculation ─────────────────────────────────────────────────────

    [Fact]
    public void GetStatus_SumsAllMatchingTransactions()
    {
        var txs = new[]
        {
            Expense(DefaultCategories.Groceries, 1_000m, day: 5),
            Expense(DefaultCategories.Groceries, 1_500m, day: 12),
            Expense(DefaultCategories.Groceries,   500m, day: 20),
        };
        var status = _engine.GetStatus(FoodBudget(5_000m), txs, June);
        status.AmountSpent.Should().Be(3_000m);
        status.AmountRemaining.Should().Be(2_000m);
    }

    [Fact]
    public void GetStatus_OtherCategoryTransactions_NotCounted()
    {
        var txs = new[]
        {
            Expense(DefaultCategories.Groceries, 1_000m),
            Expense(DefaultCategories.Rent,     15_000m),   // different category
            Expense(DefaultCategories.MatatuFare,   200m),
        };
        var status = _engine.GetStatus(FoodBudget(5_000m), txs, June);
        status.AmountSpent.Should().Be(1_000m); // only Groceries
    }

    [Fact]
    public void GetStatus_OutOfRangeTransactions_NotCounted()
    {
        var inJune = Expense(DefaultCategories.Groceries, 2_000m, day: 15);
        var inMay  = new Transaction
        {
            TransactionCode = "MAYXXXXX01",
            Source          = TransactionSource.Mpesa,
            Amount          = 3_000m,
            Type            = TransactionType.PaidTill,
            Timestamp       = new DateTimeOffset(2025, 5, 15, 10, 0, 0, Eat),
            RawMessage      = "",
            Category        = DefaultCategories.Groceries,
        };

        var status = _engine.GetStatus(FoodBudget(5_000m), [inJune, inMay], June);
        status.AmountSpent.Should().Be(2_000m);
    }

    [Fact]
    public void GetStatus_NonExpenseTransfers_NotCounted()
    {
        var withdrawal = new Transaction
        {
            TransactionCode  = "WDXXXXXXX1",
            Source           = TransactionSource.Mpesa,
            Amount           = 5_000m,
            Type             = TransactionType.Withdrawn,
            Timestamp        = new DateTimeOffset(2025, 6, 10, 10, 0, 0, Eat),
            RawMessage       = "",
            Category         = DefaultCategories.Groceries,
            IsNonExpenseTransfer = true,
        };
        var status = _engine.GetStatus(FoodBudget(5_000m), [withdrawal], June);
        status.AmountSpent.Should().Be(0m);
    }

    // ── Custom thresholds ─────────────────────────────────────────────────────

    [Fact]
    public void GetStatus_CustomThresholds_Respected()
    {
        var budget = new Budget
        {
            Category              = DefaultCategories.Betting,
            MonthlyLimit          = 1_000m,
            AmberThresholdPercent = 50.0,   // alert earlier
            RedThresholdPercent   = 70.0,
        };

        var txs = new[] { Expense(DefaultCategories.Betting, 550m) }; // 55%
        var status = _engine.GetStatus(budget, txs, June);
        status.AlertLevel.Should().Be(AlertLevel.Amber); // 55% >= 50% amber
    }

    // ── GetStatuses / GetBreachedBudgets ──────────────────────────────────────

    [Fact]
    public void GetStatuses_ReturnsOnePerEnabledBudget()
    {
        var budgets = new[]
        {
            FoodBudget(5_000m),
            new Budget { Category = DefaultCategories.Rent, MonthlyLimit = 15_000m },
            new Budget { Category = DefaultCategories.MatatuFare, MonthlyLimit = 2_000m, IsEnabled = false },
        };

        var statuses = _engine.GetStatuses(budgets, [], June);
        statuses.Should().HaveCount(2); // disabled budget excluded
    }

    [Fact]
    public void GetBreachedBudgets_OnlyReturnsAlertingBudgets()
    {
        var budgets = new[]
        {
            FoodBudget(5_000m),                                                     // will breach
            new Budget { Category = DefaultCategories.Rent, MonthlyLimit = 15_000m }, // won't breach
        };
        var txs = new[]
        {
            Expense(DefaultCategories.Groceries, 4_800m),  // 96% → Red
            Expense(DefaultCategories.Rent,      5_000m),  // 33% → None
        };

        var breached = _engine.GetBreachedBudgets(budgets, txs, June);
        breached.Should().HaveCount(1);
        breached[0].Budget.Category.Should().Be(DefaultCategories.Groceries);
        breached[0].AlertLevel.Should().Be(AlertLevel.Red);
    }

    [Fact]
    public void GetBreachedBudgets_OrderedByAlertLevelThenProgress()
    {
        var budgets = new[]
        {
            new Budget { Category = DefaultCategories.Betting,    MonthlyLimit = 1_000m },
            new Budget { Category = DefaultCategories.Groceries,  MonthlyLimit = 5_000m },
            new Budget { Category = DefaultCategories.MatatuFare, MonthlyLimit = 2_000m },
        };
        var txs = new[]
        {
            Expense(DefaultCategories.Betting,    1_100m),  // Overspent
            Expense(DefaultCategories.Groceries,  4_600m),  // Red (92%)
            Expense(DefaultCategories.MatatuFare, 1_600m),  // Amber (80%)
        };

        var breached = _engine.GetBreachedBudgets(budgets, txs, June);
        breached[0].AlertLevel.Should().Be(AlertLevel.Overspent);
        breached[1].AlertLevel.Should().Be(AlertLevel.Red);
        breached[2].AlertLevel.Should().Be(AlertLevel.Amber);
    }

    // ── Analytics fixture integration ─────────────────────────────────────────

    [Fact]
    public void GetStatus_WithAnalyticsFixtureData_RentBudgetCorrect()
    {
        var budget = new Budget { Category = DefaultCategories.Rent, MonthlyLimit = 15_000m };
        var status = _engine.GetStatus(budget, Analytics.AnalyticsFixtures.June2025, June);

        status.AmountSpent.Should().Be(15_000m);
        status.ProgressPercent.Should().BeApproximately(100.0, 0.01);
        // Exactly at limit — boundary is Overspent only when > 100%
        status.AlertLevel.Should().Be(AlertLevel.Red);
    }
}
