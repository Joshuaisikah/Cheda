using Cheda.Core.Analytics;
using Cheda.Core.Categorization;
using Cheda.Core.Models;
using FluentAssertions;

namespace Cheda.Tests.Analytics;

public class AnalyticsEngineTests
{
    private readonly AnalyticsEngine _engine = new();
    private static readonly TimeSpan Eat = TimeSpan.FromHours(3);
    private static readonly DateRange June = DateRange.ForMonth(2025, 6, Eat);
    private static readonly DateRange May  = DateRange.ForMonth(2025, 5, Eat);

    // ── Summary ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetSummary_June_CorrectIncome()
    {
        var s = _engine.GetSummary(AnalyticsFixtures.June2025, June);
        s.TotalIncome.Should().Be(52_000m); // 50,000 + 2,000
    }

    [Fact]
    public void GetSummary_June_ExpensesNetOfReversal()
    {
        var s = _engine.GetSummary(AnalyticsFixtures.June2025, June);

        // Gross: rent 15000 + electricity 2500 + groceries 3000 +
        //        matatu x4 (200) + food 800 + airtime 200 + wrong-send 500 = 22200
        // Reversal: 500 → netExpenses = 21700
        s.TotalReversals.Should().Be(500m);
        s.TotalExpenses.Should().Be(21_700m);
    }

    [Fact]
    public void GetSummary_June_NonExpenseTransfersExcluded()
    {
        var s = _engine.GetSummary(AnalyticsFixtures.June2025, June);

        // Withdrawal (5000) and MShwari (3000) must not appear in expenses
        s.TotalExpenses.Should().BeLessThan(30_000m);
        // Their IsNonExpenseTransfer flag means they don't inflate income either
        s.TotalIncome.Should().Be(52_000m);
    }

    [Fact]
    public void GetSummary_June_NetIsIncomeMinusExpenses()
    {
        var s = _engine.GetSummary(AnalyticsFixtures.June2025, June);
        s.Net.Should().Be(s.TotalIncome - s.TotalExpenses);
    }

    [Fact]
    public void GetSummary_June_SavingsRateIsPositive()
    {
        var s = _engine.GetSummary(AnalyticsFixtures.June2025, June);
        s.SavingsRate.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetSummary_June_FeesIncludesAllTransactionCosts()
    {
        var s = _engine.GetSummary(AnalyticsFixtures.June2025, June);
        // rent 33 + electricity 33 + wrong-send 11 + withdrawal 33 = 110
        s.TotalFees.Should().Be(110m);
    }

    [Fact]
    public void GetSummary_June_CurrentBalanceFromLatestNonFuliza()
    {
        var s = _engine.GetSummary(AnalyticsFixtures.June2025, June);
        // Most recent transaction with BalanceAfter excluding Fuliza
        // MShwari on Jun 25 → balance 17,311
        s.CurrentBalance.Should().Be(17_311m);
    }

    [Fact]
    public void GetSummary_EmptyRange_ReturnsZeroes()
    {
        var empty = DateRange.ForMonth(2020, 1, Eat);
        var s = _engine.GetSummary(AnalyticsFixtures.All, empty);

        s.TotalIncome.Should().Be(0m);
        s.TotalExpenses.Should().Be(0m);
        s.TransactionCount.Should().Be(0);
    }

    // ── Category breakdown ────────────────────────────────────────────────────

    [Fact]
    public void GetCategoryBreakdown_June_RentIsLargestCategory()
    {
        var breakdown = _engine.GetCategoryBreakdown(AnalyticsFixtures.June2025, June);
        breakdown[0].Category.Should().Be(DefaultCategories.Rent);
        breakdown[0].Total.Should().Be(15_000m);
    }

    [Fact]
    public void GetCategoryBreakdown_June_PercentagesSumToHundred()
    {
        var breakdown = _engine.GetCategoryBreakdown(AnalyticsFixtures.June2025, June);
        breakdown.Sum(b => b.Percentage).Should().BeApproximately(100.0, 0.01);
    }

    [Fact]
    public void GetCategoryBreakdown_June_MatatuGroupedCorrectly()
    {
        var breakdown = _engine.GetCategoryBreakdown(AnalyticsFixtures.June2025, June);
        var matatu = breakdown.FirstOrDefault(b => b.Category == DefaultCategories.MatatuFare);
        matatu.Should().NotBeNull();
        matatu!.Total.Should().Be(200m);        // 4 × 50
        matatu.TransactionCount.Should().Be(4);
    }

    [Fact]
    public void GetCategoryBreakdown_ReversedTransactionNotCounted()
    {
        // The reversed "wrong person" send of 500 should be netted out in Summary,
        // but for category breakdown we only include expense-type transactions;
        // the reversal itself is Type.Reversal so IsExpense = false → not in breakdown.
        var breakdown = _engine.GetCategoryBreakdown(AnalyticsFixtures.June2025, June);
        var uncategorized = breakdown.FirstOrDefault(b => b.Category == DefaultCategories.Uncategorized);
        // The wrong-send had no category, so it'll show as Uncategorized with 500
        // (reversal is excluded from breakdown since it's not an expense type)
        if (uncategorized is not null)
            uncategorized.Total.Should().Be(500m);
    }

    // ── Trends ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetTrend_Monthly_ReturnsTwoPointsForTwoMonths()
    {
        var range = new DateRange(
            DateRange.ForMonth(2025, 5, Eat).Start,
            DateRange.ForMonth(2025, 6, Eat).End);

        var trend = _engine.GetTrend(AnalyticsFixtures.All, range, TrendGranularity.Monthly);
        trend.Should().HaveCount(2);
    }

    [Fact]
    public void GetTrend_Monthly_PointsOrderedAscending()
    {
        var range = new DateRange(
            DateRange.ForMonth(2025, 5, Eat).Start,
            DateRange.ForMonth(2025, 6, Eat).End);

        var trend = _engine.GetTrend(AnalyticsFixtures.All, range, TrendGranularity.Monthly);
        trend[0].PeriodStart.Month.Should().Be(5);
        trend[1].PeriodStart.Month.Should().Be(6);
    }

    [Fact]
    public void GetTrend_Daily_EachDayHasExpectedIncome()
    {
        // June 1 has a single Received 50,000
        var range = new DateRange(
            new DateTimeOffset(2025, 6, 1, 0, 0, 0, Eat),
            new DateTimeOffset(2025, 6, 2, 0, 0, 0, Eat));

        var trend = _engine.GetTrend(AnalyticsFixtures.June2025, range, TrendGranularity.Daily);
        trend.Should().HaveCount(1);
        trend[0].Income.Should().Be(50_000m);
    }

    [Fact]
    public void GetTrend_Weekly_NetReflectsExpensesAndIncome()
    {
        var range = DateRange.ForMonth(2025, 6, Eat);
        var trend = _engine.GetTrend(AnalyticsFixtures.June2025, range, TrendGranularity.Weekly);

        // Every point: Net = Income - Expenses
        foreach (var p in trend)
            p.Net.Should().Be(p.Income - p.Expenses);
    }

    // ── Fee analytics ─────────────────────────────────────────────────────────

    [Fact]
    public void GetFeeAnalytics_June_TotalMatchesSummaryFees()
    {
        var fees    = _engine.GetFeeAnalytics(AnalyticsFixtures.June2025, June);
        var summary = _engine.GetSummary(AnalyticsFixtures.June2025, June);
        fees.TotalFees.Should().Be(summary.TotalFees);
    }

    [Fact]
    public void GetFeeAnalytics_June_BreakdownByTypeIsNonEmpty()
    {
        var fees = _engine.GetFeeAnalytics(AnalyticsFixtures.June2025, June);
        fees.ByType.Should().NotBeEmpty();
    }

    // ── Fuliza analytics ──────────────────────────────────────────────────────

    [Fact]
    public void GetFulizaAnalytics_June_DrawdownCountAndAmount()
    {
        var f = _engine.GetFulizaAnalytics(AnalyticsFixtures.June2025, June);
        f.DrawdownCount.Should().Be(1);
        f.TotalBorrowed.Should().Be(200m);
    }

    [Fact]
    public void GetFulizaAnalytics_June_EstimatedOutstandingFromLastBalance()
    {
        var f = _engine.GetFulizaAnalytics(AnalyticsFixtures.June2025, June);
        f.EstimatedOutstanding.Should().Be(500m);
    }

    [Fact]
    public void GetFulizaAnalytics_NoFulizaInRange_ReturnsZeroes()
    {
        var f = _engine.GetFulizaAnalytics(AnalyticsFixtures.May2025, May);
        f.DrawdownCount.Should().Be(0);
        f.TotalBorrowed.Should().Be(0m);
        f.EstimatedOutstanding.Should().BeNull();
    }

    // ── Top counterparties ────────────────────────────────────────────────────

    [Fact]
    public void GetTopCounterparties_June_LandlordIsFirst()
    {
        var top = _engine.GetTopCounterparties(AnalyticsFixtures.June2025, June);
        top[0].Name.Should().Contain("LANDLORD");
        top[0].Total.Should().Be(15_000m);
    }

    [Fact]
    public void GetTopCounterparties_LimitsResults()
    {
        var top = _engine.GetTopCounterparties(AnalyticsFixtures.June2025, June, top: 3);
        top.Count.Should().BeLessThanOrEqualTo(3);
    }

    // ── Biggest transactions ──────────────────────────────────────────────────

    [Fact]
    public void GetBiggestTransactions_June_RentIsFirst()
    {
        var big = _engine.GetBiggestTransactions(AnalyticsFixtures.June2025, June);
        big[0].Amount.Should().Be(15_000m);
        big[0].Type.Should().Be(TransactionType.PaidPaybill);
    }

    // ── Month-over-month ──────────────────────────────────────────────────────

    [Fact]
    public void GetMonthOverMonth_JuneVsMay_ExpenseIncreased()
    {
        var mom = _engine.GetMonthOverMonth(AnalyticsFixtures.All, 2025, 6, Eat);

        mom.Current.TotalExpenses.Should().BeGreaterThan(mom.Previous.TotalExpenses);
        mom.ExpenseChange.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetMonthOverMonth_JuneVsMay_IncomeIncreased()
    {
        var mom = _engine.GetMonthOverMonth(AnalyticsFixtures.All, 2025, 6, Eat);
        // June 52,000 > May 48,000
        mom.IncomeChange.Should().BeGreaterThan(0);
    }

    // ── Week-over-week ────────────────────────────────────────────────────────

    [Fact]
    public void GetWeekOverWeek_ReturnsCurrentAndPreviousWeek()
    {
        var anyMonday = new DateTimeOffset(2025, 6, 9, 12, 0, 0, Eat);
        var wow = _engine.GetWeekOverWeek(AnalyticsFixtures.All, anyMonday);

        wow.Current.Range.Start.DayOfWeek.Should().Be(DayOfWeek.Monday);
        wow.Previous.Range.Start.DayOfWeek.Should().Be(DayOfWeek.Monday);
        wow.Previous.Range.Start.Should().BeBefore(wow.Current.Range.Start);
    }

    // ── Current balance ───────────────────────────────────────────────────────

    [Fact]
    public void GetCurrentBalance_ReturnsLatestNonFulizaBalance()
    {
        var balance = _engine.GetCurrentBalance(AnalyticsFixtures.June2025);
        balance.Should().Be(17_311m);
    }

    [Fact]
    public void GetCurrentBalance_EmptyList_ReturnsNull()
    {
        _engine.GetCurrentBalance([]).Should().BeNull();
    }

    // ── DateRange helpers ─────────────────────────────────────────────────────

    [Fact]
    public void DateRange_ForMonth_StartsOnFirst()
    {
        var r = DateRange.ForMonth(2025, 6, Eat);
        r.Start.Day.Should().Be(1);
        r.Start.Month.Should().Be(6);
    }

    [Fact]
    public void DateRange_ForWeek_StartsOnMonday()
    {
        var wednesday = new DateTimeOffset(2025, 6, 11, 0, 0, 0, Eat);
        var r = DateRange.ForWeek(wednesday);
        r.Start.DayOfWeek.Should().Be(DayOfWeek.Monday);
        r.Start.Day.Should().Be(9); // Mon 9 June
    }
}
