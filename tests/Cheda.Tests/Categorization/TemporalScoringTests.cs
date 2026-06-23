using Cheda.Core.Categorization;
using Cheda.Core.Models;
using FluentAssertions;

namespace Cheda.Tests.Categorization;

/// <summary>
/// Verifies that amount band and time-of-day signals boost confidence in the learned-memory
/// step without ever overriding explicit rules or creating hard categorizations.
/// </summary>
public class TemporalScoringTests
{
    // Day 9 of month, 14:00 EAT — a typical afternoon grocery / utility payment
    private static readonly DateTimeOffset BaseTime =
        new(2026, 6, 9, 14, 0, 0, TimeSpan.FromHours(3));

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Transaction MakeTx(
        decimal amount,
        string  counterparty = "MAXWELL CHEMIST",
        int     day          = 9,
        int     hour         = 14) =>
        new()
        {
            TransactionCode = "TEMP000001",
            Source          = TransactionSource.Mpesa,
            Amount          = amount,
            Type            = TransactionType.PaidTill,
            Counterparty    = counterparty,
            Timestamp       = new DateTimeOffset(2026, 6, day, hour, 0, 0, TimeSpan.FromHours(3)),
            RawMessage      = "",
        };

    private static RuleBasedCategorizer BuildWith(
        LearnedMapping mapping, double threshold = 0.6)
    {
        var store = new InMemoryCategorizerStore();
        store.UpsertLearnedMapping(mapping);
        return new RuleBasedCategorizer(store, threshold);
    }

    // Mapping key for "MAXWELL CHEMIST" (PaidTill, no till number) = "maxwell chemist"
    private static LearnedMapping Mapping(
        decimal amountLow    = 0,
        decimal amountHigh   = 0,
        int     sampleCount  = 0,
        int     dayMask      = 0,
        int     hourMask     = 0) =>
        new()
        {
            Key               = "maxwell chemist",
            Category          = DefaultCategories.Groceries,
            ConfirmationCount = 1,
            TypicalAmountLow  = amountLow,
            TypicalAmountHigh = amountHigh,
            SampleCount       = sampleCount,
            DayOfMonthMask    = dayMask,
            HourMask          = hourMask,
        };

    // ── Amount band ───────────────────────────────────────────────────────────

    [Fact]
    public void AmountInBand_BoostsConfidenceByFivePercent()
    {
        // Band Ksh80-130, slack = max(50*0.20, 20) = 20 → accepted [60, 150]
        var cat    = BuildWith(Mapping(amountLow: 80, amountHigh: 130, sampleCount: 1));
        var result = cat.Categorize(MakeTx(100m));

        // Base 0.90 + temporal boost 0.05 = 0.95
        result.Confidence.Should().BeApproximately(0.95, precision: 0.001);
    }

    [Fact]
    public void AmountOutsideBand_NoBoost()
    {
        // Band Ksh80-130, slack = 20 → accepted [60, 150].  Ksh500 is way outside.
        var cat    = BuildWith(Mapping(amountLow: 80, amountHigh: 130, sampleCount: 1));
        var result = cat.Categorize(MakeTx(500m));

        // Base 0.90, no temporal boost
        result.Confidence.Should().BeApproximately(0.90, precision: 0.001);
    }

    [Fact]
    public void FixedAmount_TightBandWithMinimumSlack()
    {
        // Always exactly Ksh20 — band [20,20], slack = max(0, 20) = 20 → accepted [0, 40]
        var cat = BuildWith(Mapping(amountLow: 20, amountHigh: 20, sampleCount: 1));

        cat.Categorize(MakeTx(20m)).Confidence.Should().BeApproximately(0.95, 0.001);  // in band
        cat.Categorize(MakeTx(35m)).Confidence.Should().BeApproximately(0.95, 0.001);  // within slack
        cat.Categorize(MakeTx(500m)).Confidence.Should().BeApproximately(0.90, 0.001); // outside
    }

    [Fact]
    public void NoSamples_NoTemporalBoost()
    {
        // SampleCount = 0 → temporal profile inactive → base confidence only
        var cat    = BuildWith(Mapping(sampleCount: 0));
        var result = cat.Categorize(MakeTx(100m));

        result.Confidence.Should().BeApproximately(0.90, precision: 0.001);
    }

    // ── Day-of-month signal ───────────────────────────────────────────────────

    [Fact]
    public void DayMatch_WithThreePlusSamples_AddsThreePercentBoost()
    {
        // Observed on day 9 → bit 8 set (1 << 8 = 256)
        // Tolerance ±5: tx on day 9 → match
        var dayMask = 1 << 8;  // day 9
        var cat     = BuildWith(Mapping(
            amountLow: 80, amountHigh: 130, sampleCount: 3, dayMask: dayMask));

        var result = cat.Categorize(MakeTx(100m, day: 9));

        // Base 0.90 + amount boost 0.05 + day boost 0.03 = 0.98
        result.Confidence.Should().BeApproximately(0.98, precision: 0.001);
    }

    [Fact]
    public void DayOutsideTolerance_NoBoost()
    {
        // Observed day 9; tx on day 20 (11 days away, > ±5 tolerance)
        var dayMask = 1 << 8;  // day 9
        var cat     = BuildWith(Mapping(
            amountLow: 80, amountHigh: 130, sampleCount: 3, dayMask: dayMask));

        var result = cat.Categorize(MakeTx(100m, day: 20));

        // Only amount boost applies (0.05), no day boost
        result.Confidence.Should().BeApproximately(0.95, precision: 0.001);
    }

    [Fact]
    public void DaySignal_RequiresThreeSamples_NoBoostBeforeThat()
    {
        var dayMask = 1 << 8;  // day 9
        // sampleCount = 2 (< 3 threshold) → day signal inactive
        var cat     = BuildWith(Mapping(
            amountLow: 80, amountHigh: 130, sampleCount: 2, dayMask: dayMask));

        var result = cat.Categorize(MakeTx(100m, day: 9));

        // Only amount boost (0.05) — day threshold not met
        result.Confidence.Should().BeApproximately(0.95, precision: 0.001);
    }

    // ── Hour-of-day signal ────────────────────────────────────────────────────

    [Fact]
    public void HourMatch_WithThreePlusSamples_AddsTwoPercentBoost()
    {
        // Observed at hour 14 (2 PM) → bit 14 set
        // Tolerance ±2: tx at hour 13 → match
        var hourMask = 1 << 14;  // 2 PM
        var cat      = BuildWith(Mapping(
            amountLow: 80, amountHigh: 130, sampleCount: 3, hourMask: hourMask));

        var result = cat.Categorize(MakeTx(100m, hour: 13));

        // Base 0.90 + amount 0.05 + hour 0.02 = 0.97
        result.Confidence.Should().BeApproximately(0.97, precision: 0.001);
    }

    [Fact]
    public void AllThreeSignalsMatch_BoostsCappedAt0_99()
    {
        // Day 9 → bit 8; Hour 14 → bit 14
        var dayMask  = 1 << 8;
        var hourMask = 1 << 14;
        var cat      = BuildWith(Mapping(
            amountLow: 80, amountHigh: 130, sampleCount: 5,
            dayMask: dayMask, hourMask: hourMask));

        var result = cat.Categorize(MakeTx(100m, day: 9, hour: 14));

        // 0.90 + 0.05 + 0.03 + 0.02 = 1.00 → capped at 0.99
        result.Confidence.Should().BeApproximately(0.99, precision: 0.001);
        result.NeedsReview.Should().BeFalse();
    }

    // ── ObserveTransaction ────────────────────────────────────────────────────

    [Fact]
    public void ObserveTransaction_UpdatesAmountBand()
    {
        var store = new InMemoryCategorizerStore();
        var cat   = new RuleBasedCategorizer(store);
        var tx    = MakeTx(150m);
        cat.LearnFromCorrection(tx, DefaultCategories.Groceries);

        cat.ObserveTransaction(MakeTx(100m));
        cat.ObserveTransaction(MakeTx(200m));

        var m = store.GetLearnedMappings().Single();
        m.SampleCount.Should().Be(2);
        m.TypicalAmountLow.Should().Be(100m);
        m.TypicalAmountHigh.Should().Be(200m);
    }

    [Fact]
    public void ObserveTransaction_UpdatesDayAndHourMask()
    {
        var store = new InMemoryCategorizerStore();
        var cat   = new RuleBasedCategorizer(store);
        cat.LearnFromCorrection(MakeTx(100m), DefaultCategories.Groceries);

        // Observe on day 9 (bit 8) at hour 14 (bit 14)
        cat.ObserveTransaction(MakeTx(100m, day: 9, hour: 14));

        var m = store.GetLearnedMappings().Single();
        (m.DayOfMonthMask & (1 << 8)).Should().NotBe(0,  "day 9 should be recorded");
        (m.HourMask       & (1 << 14)).Should().NotBe(0, "hour 14 should be recorded");
    }

    [Fact]
    public void ObserveTransaction_NoOpWhenNoMappingExists()
    {
        var store = new InMemoryCategorizerStore();
        var cat   = new RuleBasedCategorizer(store);

        // No learned mapping for this counterparty — should not throw or create one
        var act = () => cat.ObserveTransaction(MakeTx(100m));
        act.Should().NotThrow();
        store.GetLearnedMappings().Should().BeEmpty();
    }

    [Fact]
    public void TemporalSignals_NeverFireBeforeLearnedMemoryStep()
    {
        // Type rules return confidence 1.0 and bypass learned memory entirely.
        // Temporal signals must not interfere.
        var store = new InMemoryCategorizerStore();
        store.UpsertLearnedMapping(Mapping(amountLow: 1, amountHigh: 1000, sampleCount: 10));
        var cat = new RuleBasedCategorizer(store);

        var airtimeTx = new Transaction
        {
            TransactionCode = "AIRTEST001",
            Source          = TransactionSource.Mpesa,
            Amount          = 50m,
            Type            = TransactionType.Airtime,  // type rule fires first
            Timestamp       = BaseTime,
            RawMessage      = "",
        };

        var result = cat.Categorize(airtimeTx);

        result.Category.Should().Be(DefaultCategories.Airtime);
        result.Confidence.Should().Be(1.0);  // type rule, not temporal
        result.MatchedRule.Should().Contain("Type");
    }
}
