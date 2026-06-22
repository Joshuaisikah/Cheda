using Cheda.Core.Categorization;
using Cheda.Core.Models;
using FluentAssertions;

namespace Cheda.Tests.Categorization;

public class CategorizerTests
{
    private static readonly DateTimeOffset Morning =
        new(2025, 6, 16, 7, 15, 0, TimeSpan.FromHours(3));  // 07:15 EAT, Monday

    private static readonly DateTimeOffset Afternoon =
        new(2025, 6, 16, 14, 0, 0, TimeSpan.FromHours(3));  // 14:00 EAT, Monday

    private static Transaction MakeSent(decimal amount, string? counterparty = null, DateTimeOffset? ts = null) =>
        new()
        {
            TransactionCode = Guid.NewGuid().ToString("N")[..10].ToUpper(),
            Source = TransactionSource.Mpesa,
            Amount = amount,
            Type = TransactionType.Sent,
            Counterparty = counterparty ?? "UNKNOWN PERSON 0700000000",
            Timestamp = ts ?? Morning,
            RawMessage = "",
        };

    private static Transaction MakeType(TransactionType type, decimal amount = 100m) =>
        new()
        {
            TransactionCode = Guid.NewGuid().ToString("N")[..10].ToUpper(),
            Source = TransactionSource.Mpesa,
            Amount = amount,
            Type = type,
            Timestamp = Morning,
            RawMessage = "",
        };

    private static Transaction MakePaidTill(string counterparty, decimal amount) =>
        new()
        {
            TransactionCode = Guid.NewGuid().ToString("N")[..10].ToUpper(),
            Source = TransactionSource.Mpesa,
            Amount = amount,
            Type = TransactionType.PaidTill,
            Counterparty = counterparty,
            Timestamp = Afternoon,
            RawMessage = "",
        };

    private static Transaction MakePaidPaybill(string counterparty, decimal amount) =>
        new()
        {
            TransactionCode = Guid.NewGuid().ToString("N")[..10].ToUpper(),
            Source = TransactionSource.Mpesa,
            Amount = amount,
            Type = TransactionType.PaidPaybill,
            Counterparty = counterparty,
            Timestamp = Afternoon,
            RawMessage = "",
        };

    // ── Step 0: type-based deterministic rules ────────────────────────────────

    [Theory]
    [InlineData(TransactionType.Airtime,   DefaultCategories.Airtime)]
    [InlineData(TransactionType.Withdrawn, DefaultCategories.Withdrawals)]
    [InlineData(TransactionType.MShwari,   DefaultCategories.MShwari)]
    [InlineData(TransactionType.Fuliza,    DefaultCategories.Fuliza)]
    [InlineData(TransactionType.Reversal,  DefaultCategories.RefundsReversals)]
    public void TypeRule_KnownType_CategorisesAtFullConfidence(TransactionType type, string expected)
    {
        var cat = new RuleBasedCategorizer(new InMemoryCategorizerStore());
        var result = cat.Categorize(MakeType(type));

        result.Category.Should().Be(expected);
        result.Confidence.Should().Be(1.0);
        result.NeedsReview.Should().BeFalse();
    }

    // ── Step 1: recipient rules ───────────────────────────────────────────────

    [Fact]
    public void RecipientRule_MatchingKeyword_AppliesHighConfidence()
    {
        var store = new InMemoryCategorizerStore();
        store.Add(new RecipientRule
        {
            Priority = 1,
            Label = "KPLC",
            Keywords = ["KPLC", "888880"],
            Category = DefaultCategories.Electricity,
        });

        var cat = new RuleBasedCategorizer(store);
        var tx = MakePaidPaybill("KPLC PREPAID (888880/54321)", 2500m);

        var result = cat.Categorize(tx);

        result.Category.Should().Be(DefaultCategories.Electricity);
        result.Confidence.Should().Be(0.95);
        result.NeedsReview.Should().BeFalse();
        result.MatchedRule.Should().Contain("KPLC");
    }

    [Fact]
    public void RecipientRule_PriorityOrdering_HigherPriorityWins()
    {
        var store = new InMemoryCategorizerStore();
        store.Add(new RecipientRule
        {
            Priority = 2,
            Label = "Generic Java",
            Keywords = ["JAVA"],
            Category = DefaultCategories.EatingOut,
        });
        store.Add(new RecipientRule
        {
            Priority = 1,   // wins
            Label = "Java House Till",
            Keywords = ["JAVA HOUSE"],
            Category = DefaultCategories.EatingOut,
        });

        var cat = new RuleBasedCategorizer(store);
        var result = cat.Categorize(MakePaidTill("JAVA HOUSE (Till 123456)", 250m));

        result.MatchedRule.Should().Contain("Java House Till");
    }

    // ── Step 2: pattern rules — morning matatu fare ───────────────────────────

    [Fact]
    public void PatternRule_MorningFare_MatchesContextually()
    {
        var store = new InMemoryCategorizerStore();
        store.Add(new PatternRule
        {
            Priority = 1,
            Label = "Morning matatu fare",
            TransactionTypes = [TransactionType.Sent],
            AmountMax = 100m,
            TimeOfDayStart = new TimeOnly(5, 0),
            TimeOfDayEnd = new TimeOnly(9, 0),
            Category = DefaultCategories.MatatuFare,
        });

        var cat = new RuleBasedCategorizer(store);

        // Morning small send → should match
        var morningTx = MakeSent(50m, "CONDUCTOR 0799999999", Morning); // 07:15
        var morning = cat.Categorize(morningTx);
        morning.Category.Should().Be(DefaultCategories.MatatuFare);
        morning.NeedsReview.Should().BeFalse();

        // Afternoon same amount → should NOT match
        var afternoonTx = MakeSent(50m, "CONDUCTOR 0799999999", Afternoon); // 14:00
        var afternoon = cat.Categorize(afternoonTx);
        afternoon.Category.Should().NotBe(DefaultCategories.MatatuFare);
    }

    [Fact]
    public void PatternRule_AmountTooHigh_DoesNotMatch()
    {
        var store = new InMemoryCategorizerStore();
        store.Add(new PatternRule
        {
            Priority = 1,
            Label = "Morning matatu fare",
            TransactionTypes = [TransactionType.Sent],
            AmountMax = 100m,
            TimeOfDayStart = new TimeOnly(5, 0),
            TimeOfDayEnd = new TimeOnly(9, 0),
            Category = DefaultCategories.MatatuFare,
        });

        var cat = new RuleBasedCategorizer(store);
        var result = cat.Categorize(MakeSent(500m, ts: Morning)); // too large

        result.Category.Should().NotBe(DefaultCategories.MatatuFare);
    }

    // ── Step 3: learned memory ────────────────────────────────────────────────

    [Fact]
    public void LearnedMemory_AfterCorrection_ExactMatchHighConfidence()
    {
        var store = new InMemoryCategorizerStore();
        var cat = new RuleBasedCategorizer(store);

        var tx = MakePaidPaybill("LANDLORD INC (123456/RENT01)", 15000m);
        cat.LearnFromCorrection(tx, DefaultCategories.Rent);

        var result = cat.Categorize(tx);

        result.Category.Should().Be(DefaultCategories.Rent);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.90);
        result.NeedsReview.Should().BeFalse();
        result.MatchedRule.Should().Contain("Learned");
    }

    [Fact]
    public void LearnedMemory_RepeatedCorrections_IncreaseConfidence()
    {
        var store = new InMemoryCategorizerStore();
        var cat = new RuleBasedCategorizer(store);
        var tx = MakePaidPaybill("LANDLORD INC (123456/RENT01)", 15000m);

        cat.LearnFromCorrection(tx, DefaultCategories.Rent);
        cat.LearnFromCorrection(tx, DefaultCategories.Rent);
        cat.LearnFromCorrection(tx, DefaultCategories.Rent);

        var result = cat.Categorize(tx);
        result.Confidence.Should().BeGreaterThan(0.90);
    }

    [Fact]
    public void LearnedMemory_StableKeyAcrossNameVariation_StillMatches()
    {
        // Paybill number is the same; merchant name text differs (e.g. re-registered)
        var store = new InMemoryCategorizerStore();
        var cat = new RuleBasedCategorizer(store);

        var original = MakePaidPaybill("NAIROBI WATER (123999/ACC77)", 800m);
        cat.LearnFromCorrection(original, DefaultCategories.Water);

        var variant = MakePaidPaybill("NBI WATER CO (123999/ACC77)", 900m); // name changed
        var result = cat.Categorize(variant);

        result.Category.Should().Be(DefaultCategories.Water);
    }

    // ── Step 4: similarity guess ──────────────────────────────────────────────

    [Fact]
    public void SimilarityGuess_PartialNameMatch_ReturnsLowConfidenceFlag()
    {
        var store = new InMemoryCategorizerStore();
        var cat = new RuleBasedCategorizer(store);

        // Learn from a named Sent transaction → key = "java coffee shop"
        var known = MakeSent(300m, "JAVA COFFEE SHOP 0712345678");
        cat.LearnFromCorrection(known, DefaultCategories.EatingOut);

        // Similar name, different phone → key = "java coffee westlands"
        // Token overlap: {"java", "coffee"} → Jaccard = 2/4 = 0.5 > threshold
        var unknown = MakeSent(280m, "JAVA COFFEE WESTLANDS 0799999999");
        var result = cat.Categorize(unknown);

        result.Confidence.Should().BeLessThan(0.6);
        result.NeedsReview.Should().BeTrue();
        result.Category.Should().Be(DefaultCategories.EatingOut);
    }

    // ── Step 5: ask-when-unsure threshold ─────────────────────────────────────

    [Fact]
    public void NeedsReview_WhenNoRulesMatch_FlagsForReview()
    {
        var cat = new RuleBasedCategorizer(new InMemoryCategorizerStore());
        // A transaction type that doesn't hit type rules, with no rules configured
        var tx = MakeSent(5000m, "TOTALLY UNKNOWN PERSON");

        var result = cat.Categorize(tx);

        result.NeedsReview.Should().BeTrue();
        result.Confidence.Should().BeLessThan(0.6);
    }

    [Fact]
    public void NeedsReview_CustomThreshold_RespectsSetting()
    {
        var store = new InMemoryCategorizerStore();
        // Learned mapping gives 0.90 confidence
        var cat = new RuleBasedCategorizer(store, reviewThreshold: 0.95);
        var tx = MakePaidPaybill("SHOP (999/ACC)", 100m);
        cat.LearnFromCorrection(tx, DefaultCategories.Groceries);

        var result = cat.Categorize(tx);

        // 0.90 < 0.95 threshold → NeedsReview even though it's a known mapping
        result.NeedsReview.Should().BeTrue();
    }

    // ── Mapping key extraction ────────────────────────────────────────────────

    [Fact]
    public void MappingKey_TillPayment_KeysOnTillNumber()
    {
        var tx = MakePaidTill("JAVA HOUSE (Till 123456)", 250m);
        RuleBasedCategorizer.MappingKey(tx).Should().Be("till:123456");
    }

    [Fact]
    public void MappingKey_PaybillPayment_KeysOnPaybillAccount()
    {
        var tx = MakePaidPaybill("KPLC PREPAID (888880/54321)", 2500m);
        RuleBasedCategorizer.MappingKey(tx).Should().Be("paybill:888880/54321");
    }

    [Fact]
    public void MappingKey_PersonTransfer_StripsPhoneNumber()
    {
        var tx = MakeSent(1000m, "JOHN DOE 0712345678");
        var key = RuleBasedCategorizer.MappingKey(tx);
        key.Should().NotContain("0712345678");
        key.Should().Contain("john doe");
    }
}
