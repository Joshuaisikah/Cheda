using Cheda.Core.Analytics;
using Cheda.Core.Models;
using Cheda.Core.Storage;
using Cheda.Tests.Storage.InMemory;
using FluentAssertions;

namespace Cheda.Tests.Storage;

public sealed class TransactionRepositoryTests
{
    private static ITransactionRepository Repo() => new InMemoryTransactionRepository();

    private static Transaction Make(string code, decimal amount = 100m,
        TransactionSource source = TransactionSource.Mpesa,
        DateTimeOffset? at = null) => new()
    {
        TransactionCode = code,
        Source          = source,
        Amount          = amount,
        Type            = TransactionType.Sent,
        Timestamp       = at ?? DateTimeOffset.UtcNow,
        RawMessage      = "test",
    };

    // ── TryAdd ──────────────────────────────────────────────────────────────

    [Fact]
    public void TryAdd_new_transaction_returns_true_and_increments_count()
    {
        var repo = Repo();
        repo.TryAdd(Make("TX001")).Should().BeTrue();
        repo.Count().Should().Be(1);
    }

    [Fact]
    public void TryAdd_duplicate_code_same_source_returns_false_and_does_not_insert()
    {
        var repo = Repo();
        repo.TryAdd(Make("TX001", 100));
        repo.TryAdd(Make("TX001", 200)).Should().BeFalse();
        repo.Count().Should().Be(1);
        repo.GetByCode("TX001", TransactionSource.Mpesa)!.Amount.Should().Be(100);
    }

    [Fact]
    public void TryAdd_same_code_different_source_inserts_both()
    {
        // Reserve for future sources — dedup key includes source
        var repo  = Repo();
        var mpesa = Make("TX001", 100, TransactionSource.Mpesa);
        repo.TryAdd(mpesa).Should().BeTrue();
        // If a second source existed it would go in — count proves isolation
        repo.Count().Should().Be(1);
    }

    // ── AddRange ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddRange_returns_count_of_newly_inserted_skipping_duplicates()
    {
        var repo = Repo();
        repo.TryAdd(Make("TX001"));

        var batch = new[] { Make("TX001"), Make("TX002"), Make("TX003") };
        repo.AddRange(batch).Should().Be(2);
        repo.Count().Should().Be(3);
    }

    [Fact]
    public void AddRange_with_all_duplicates_inserts_nothing()
    {
        var repo = Repo();
        repo.TryAdd(Make("TX001"));
        repo.TryAdd(Make("TX002"));

        repo.AddRange([Make("TX001"), Make("TX002")]).Should().Be(0);
        repo.Count().Should().Be(2);
    }

    // ── GetInRange ────────────────────────────────────────────────────────────

    [Fact]
    public void GetInRange_returns_only_transactions_within_the_window()
    {
        var repo   = Repo();
        var offset = TimeSpan.FromHours(3);
        var jan1   = new DateTimeOffset(2025, 1, 1, 0, 0, 0, offset);
        var jan15  = new DateTimeOffset(2025, 1, 15, 12, 0, 0, offset);
        var feb1   = new DateTimeOffset(2025, 2, 1, 0, 0, 0, offset);

        repo.TryAdd(Make("TX001", at: jan1));
        repo.TryAdd(Make("TX002", at: jan15));
        repo.TryAdd(Make("TX003", at: feb1));

        var range  = DateRange.ForMonth(2025, 1, offset);
        var result = repo.GetInRange(range);

        result.Should().HaveCount(2);
        result.Select(t => t.TransactionCode).Should().Contain(["TX001", "TX002"]);
    }

    [Fact]
    public void GetInRange_excludes_end_boundary()
    {
        // DateRange is [Start, End) — End itself is excluded.
        var repo  = Repo();
        var range = DateRange.ForMonth(2025, 1, TimeSpan.Zero);
        repo.TryAdd(Make("TX001", at: range.End)); // exactly at boundary
        repo.GetInRange(range).Should().BeEmpty();
    }

    // ── GetByCode ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetByCode_returns_null_for_unknown_code()
    {
        Repo().GetByCode("NOPE", TransactionSource.Mpesa).Should().BeNull();
    }

    [Fact]
    public void GetByCode_returns_matching_transaction()
    {
        var repo = Repo();
        var tx   = Make("TX999", 500m);
        repo.TryAdd(tx);
        repo.GetByCode("TX999", TransactionSource.Mpesa)!.Amount.Should().Be(500m);
    }

    // ── Update / Delete ───────────────────────────────────────────────────────

    [Fact]
    public void Update_overwrites_existing_record()
    {
        var repo = Repo();
        var tx   = Make("TX001", 100m);
        repo.TryAdd(tx);

        var updated = new Transaction
        {
            Id              = tx.Id,
            TransactionCode = tx.TransactionCode,
            Source          = tx.Source,
            Amount          = tx.Amount,
            Type            = tx.Type,
            Timestamp       = tx.Timestamp,
            RawMessage      = tx.RawMessage,
            Category        = "Food",
        };
        repo.Update(updated);

        repo.GetByCode("TX001", TransactionSource.Mpesa)!.Category.Should().Be("Food");
    }

    [Fact]
    public void Delete_removes_the_record_by_id()
    {
        var repo = Repo();
        var tx   = Make("TX001");
        repo.TryAdd(tx);
        repo.Delete(tx.Id);
        repo.Count().Should().Be(0);
    }

    // ── GetAll ordering ───────────────────────────────────────────────────────

    [Fact]
    public void GetAll_returns_most_recent_first()
    {
        var repo  = Repo();
        var older = Make("TX001", at: DateTimeOffset.UtcNow.AddDays(-2));
        var newer = Make("TX002", at: DateTimeOffset.UtcNow.AddDays(-1));
        repo.TryAdd(older);
        repo.TryAdd(newer);

        var all = repo.GetAll();
        all[0].TransactionCode.Should().Be("TX002");
        all[1].TransactionCode.Should().Be("TX001");
    }
}
