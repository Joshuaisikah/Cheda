using System.Text;
using Cheda.Core.Categorization;
using Cheda.Core.Parsing;
using Cheda.Core.Parsing.Parsers;
using Cheda.Core.Sms;
using Cheda.Tests.Categorization;
using Cheda.Tests.Storage.InMemory;
using FluentAssertions;

namespace Cheda.Tests.Sms;

/// <summary>
/// Tests for ImportService using real Parser + real Categorizer + in-memory repository.
/// The only fake is ISmsReader (FakeSmsReader).
/// </summary>
public sealed class ImportServiceTests
{
    // Sample M-Pesa SMS fixtures — same format as the parser tests.
    private const string SentSms =
        "QAB1CD2EF3 Confirmed. Ksh500.00 sent to JOHN DOE 0712345678 on 23/6/26 at 9:00 AM. " +
        "New M-PESA balance is Ksh4,500.00. Transaction cost, Ksh11.00.";

    private const string ReceivedSms =
        "QBC2DE3FG4 Confirmed. You have received Ksh1,000.00 from JANE DOE 0798765432 " +
        "on 23/6/26 at 10:00 AM. New M-PESA balance is Ksh5,500.00.";

    private const string PaybillSms =
        "QCD3EF4GH5 Confirmed. Ksh2,000.00 paid to KPLC PREPAID 888880 Account 54321234 " +
        "on 23/6/26 at 8:00 AM. New M-PESA balance is Ksh3,500.00. Transaction cost, Ksh0.00.";

    private const string OtpSms =
        "Your M-PESA PIN is 1234. Do not share with anyone.";

    private static IImportService BuildService(FakeSmsReader reader)
    {
        var engine = new ParserEngine();
        engine.Register(new MpesaParser());

        var store       = new InMemoryCategorizerStore();
        var categorizer = new RuleBasedCategorizer(store);
        var repo        = new InMemoryTransactionRepository();

        return new ImportService(reader, engine, categorizer, repo);
    }

    // ── Basic import ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportInboxAsync_new_messages_returns_correct_new_count()
    {
        var reader  = new FakeSmsReader([FakeSmsReader.Mpesa(SentSms), FakeSmsReader.Mpesa(ReceivedSms)]);
        var service = BuildService(reader);

        var result = await service.ImportInboxAsync();

        result.NewTransactions.Should().Be(2);
        result.Duplicates.Should().Be(0);
        result.Unparseable.Should().Be(0);
    }

    [Fact]
    public async Task ImportInboxAsync_otp_message_counted_as_unparseable_not_stored()
    {
        // OTP has no transaction code — parser returns Fail().
        var reader  = new FakeSmsReader([FakeSmsReader.Mpesa(OtpSms)]);
        var service = BuildService(reader);

        var result = await service.ImportInboxAsync();

        result.NewTransactions.Should().Be(0);
        result.Unparseable.Should().Be(1);
    }

    // ── Deduplication ────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportInboxAsync_duplicate_message_not_inserted_twice()
    {
        var reader  = new FakeSmsReader([FakeSmsReader.Mpesa(SentSms)]);
        var service = BuildService(reader);

        var first  = await service.ImportInboxAsync();
        var second = await service.ImportInboxAsync();  // same messages, same codes

        first.NewTransactions.Should().Be(1);
        second.Duplicates.Should().Be(1);
        second.NewTransactions.Should().Be(0);
    }

    [Fact]
    public async Task ImportInboxAsync_batch_with_inline_duplicate_counts_correctly()
    {
        var reader = new FakeSmsReader([
            FakeSmsReader.Mpesa(SentSms),
            FakeSmsReader.Mpesa(SentSms),  // same transaction code — duplicate
            FakeSmsReader.Mpesa(ReceivedSms),
        ]);
        var service = BuildService(reader);

        var result = await service.ImportInboxAsync();

        result.NewTransactions.Should().Be(2);
        result.Duplicates.Should().Be(1);
    }

    // ── Date filter (first-run / rescan) ─────────────────────────────────────

    [Fact]
    public async Task ImportInboxAsync_since_filter_only_imports_messages_after_cutoff()
    {
        var old   = DateTimeOffset.UtcNow.AddMonths(-7);
        var recent = DateTimeOffset.UtcNow.AddDays(-3);
        var cutoff = DateTimeOffset.UtcNow.AddMonths(-6);

        var reader = new FakeSmsReader([
            FakeSmsReader.Mpesa(SentSms,     at: old),
            FakeSmsReader.Mpesa(ReceivedSms, at: recent),
        ]);
        var service = BuildService(reader);

        var result = await service.ImportInboxAsync(since: cutoff);

        result.NewTransactions.Should().Be(1);  // only the recent one
        result.Total.Should().Be(1);
    }

    // ── ProcessSingleAsync (real-time BroadcastReceiver path) ────────────────

    [Fact]
    public async Task ProcessSingleAsync_valid_message_inserts_transaction()
    {
        var reader  = new FakeSmsReader();
        var service = BuildService(reader);

        var sms    = FakeSmsReader.Mpesa(PaybillSms);
        var result = await service.ProcessSingleAsync(sms);

        result.NewTransactions.Should().Be(1);
    }

    [Fact]
    public async Task ProcessSingleAsync_otp_message_not_stored()
    {
        var reader  = new FakeSmsReader();
        var service = BuildService(reader);

        var result = await service.ProcessSingleAsync(FakeSmsReader.Mpesa(OtpSms));

        result.NewTransactions.Should().Be(0);
        result.Unparseable.Should().Be(1);
    }

    // ── Review queue ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportInboxAsync_unknown_transaction_format_lands_in_review_queue()
    {
        // A message with a transaction code but unrecognised format → Type.Unknown
        // → Categorizer returns confidence 0.0 → NeedsReview = true
        var unknownSms = "QZZ9AA8BB7 Confirmed. Something happened. New M-PESA balance is Ksh1,000.00.";
        var reader      = new FakeSmsReader([FakeSmsReader.Mpesa(unknownSms)]);
        var service     = BuildService(reader);

        var result = await service.ImportInboxAsync();

        result.NewTransactions.Should().Be(1);
        result.ReviewQueue.Should().HaveCount(1);
        result.ReviewQueue[0].Confidence.Should().Be(0.0);
    }

    [Fact]
    public async Task ImportInboxAsync_high_confidence_transaction_not_in_review_queue()
    {
        // Airtime → ApplyTypeRules → confidence 1.0 → NeedsReview = false
        var airtimeSms =
            "QXY8ZA9BC0 confirmed. You have bought Ksh50.00 of airtime " +
            "on 23/6/26 at 7:00 AM. New M-PESA balance is Ksh450.00.";

        var reader  = new FakeSmsReader([FakeSmsReader.Mpesa(airtimeSms)]);
        var service = BuildService(reader);

        var result = await service.ImportInboxAsync();

        result.NewTransactions.Should().Be(1);
        // Airtime → type rule → confidence 1.0 → NeedsReview = false
        result.ReviewQueue.Should().BeEmpty();
    }

    // ── Dual-SIM ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessSingleAsync_propagates_sim_slot_to_transaction()
    {
        var engine = new ParserEngine();
        engine.Register(new MpesaParser());
        var store  = new InMemoryCategorizerStore();
        var cat    = new RuleBasedCategorizer(store);
        var repo   = new InMemoryTransactionRepository();
        var reader = new FakeSmsReader();

        var service = new ImportService(reader, engine, cat, repo);
        await service.ProcessSingleAsync(FakeSmsReader.Mpesa(SentSms, simSlot: 2));

        var stored = repo.GetAll().Should().HaveCount(1).And.Subject.Single();
        stored.SimSlot.Should().Be(2);
    }

    [Fact]
    public async Task ProcessSingleAsync_null_sim_slot_when_single_sim()
    {
        var engine = new ParserEngine();
        engine.Register(new MpesaParser());
        var repo    = new InMemoryTransactionRepository();
        var service = new ImportService(
            new FakeSmsReader(),
            engine,
            new RuleBasedCategorizer(new InMemoryCategorizerStore()),
            repo);

        await service.ProcessSingleAsync(FakeSmsReader.Mpesa(SentSms, simSlot: null));

        repo.GetAll().Single().SimSlot.Should().BeNull();
    }

    // ── XML import ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromXmlAsync_imports_mpesa_messages_skips_other_senders()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <smses>
              <sms address="MPESA" date="1778066364125" body="UE62Q3B5VZ Confirmed. Ksh200.00 sent to FAITH GACHEMI 0797460219 on 6/5/26 at 2:19 PM. New M-PESA balance is Ksh5,914.90. Transaction cost, Ksh7.00." />
              <sms address="MPESA" date="1778066364126" body="Your M-PESA PIN reset OTP is 123456. Do not share." />
              <sms address="SAFARICOM" date="1778066364127" body="Not a financial message." />
            </smses>
            """;
        using var stream  = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var       service = BuildService(new FakeSmsReader());

        var result = await service.ImportFromXmlAsync(stream);

        result.NewTransactions.Should().Be(1);   // only the Sent transaction
        result.Unparseable.Should().Be(2);        // OTP (no tx code) + SAFARICOM (unknown sender)
    }

    [Fact]
    public async Task ImportFromXmlAsync_timestamp_comes_from_date_attribute()
    {
        // date="1778066364125" → 2026-05-06 11:19:24 UTC
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <smses>
              <sms address="MPESA" date="1778066364125" body="UE62Q3B5VZ Confirmed. Ksh200.00 sent to FAITH GACHEMI 0797460219 on 6/5/26 at 2:19 PM. New M-PESA balance is Ksh5,914.90. Transaction cost, Ksh7.00." />
            </smses>
            """;
        using var stream  = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var       repo    = new InMemoryTransactionRepository();
        var       engine  = new ParserEngine();
        engine.Register(new MpesaParser());
        var svc = new ImportService(
            new FakeSmsReader(), engine,
            new RuleBasedCategorizer(new InMemoryCategorizerStore()), repo);

        await svc.ImportFromXmlAsync(stream);

        var tx = repo.GetAll().Single();
        tx.Timestamp.ToUnixTimeMilliseconds().Should().Be(1778066364125L);
    }

    // ── Total accounting ─────────────────────────────────────────────────────

    [Fact]
    public async Task ImportResult_Total_is_sum_of_all_buckets()
    {
        var reader = new FakeSmsReader([
            FakeSmsReader.Mpesa(SentSms),
            FakeSmsReader.Mpesa(OtpSms),
        ]);
        var service = BuildService(reader);

        var first  = await service.ImportInboxAsync();
        var second = await service.ImportInboxAsync();  // SentSms is now a duplicate

        second.Total.Should().Be(second.NewTransactions + second.Duplicates + second.Unparseable);
    }
}
