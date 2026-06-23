using Cheda.Core.Categorization;
using Cheda.Core.Parsing;
using Cheda.Core.Storage;

namespace Cheda.Core.Sms;

/// <summary>
/// Orchestrates the full pipeline: read SMS → parse → categorize → dedup-insert.
/// Separating orchestration from Android platform code keeps this fully unit-testable.
/// </summary>
public sealed class ImportService : IImportService
{
    private readonly ISmsReader _reader;
    private readonly IParserEngine _parser;
    private readonly ICategorizer _categorizer;
    private readonly ITransactionRepository _repository;

    public ImportService(
        ISmsReader reader,
        IParserEngine parser,
        ICategorizer categorizer,
        ITransactionRepository repository)
    {
        _reader     = reader;
        _parser     = parser;
        _categorizer = categorizer;
        _repository = repository;
    }

    public async Task<ImportResult> ImportInboxAsync(
        DateTimeOffset? since = null, CancellationToken ct = default)
    {
        var messages = await Task.Run(() => _reader.ReadInbox(since), ct);
        return Process(messages);
    }

    public Task<ImportResult> ProcessSingleAsync(
        SmsMessage message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Process([message]));
    }

    private ImportResult Process(IReadOnlyList<SmsMessage> messages)
    {
        var newCount     = 0;
        var dupCount     = 0;
        var unparseCount = 0;
        var reviewQueue  = new List<ReviewItem>();
        var inserted     = new List<Models.Transaction>();

        foreach (var sms in messages)
        {
            var result = _parser.Parse(sms.Sender, sms.Body, sms.Timestamp);

            // Parser returns Fail() for OTP/marketing (no transaction code) and for
            // completely unrecognised senders. These are counted but never stored.
            if (!result.Success || result.Transaction is null)
            {
                unparseCount++;
                continue;
            }

            var tx = result.Transaction;

            // Propagate SIM slot from the raw message — the parser has no SIM awareness.
            tx.SimSlot = sms.SimSlot;

            // Categorize (applies type rules → recipient rules → pattern rules →
            // learned memory → similarity guess).
            var cat = _categorizer.Categorize(tx);
            tx.Category           = cat.Category;
            tx.CategoryConfidence = cat.Confidence;
            tx.MatchedRule        = cat.MatchedRule;

            // TryAdd returns false for duplicates (same TransactionCode + Source).
            if (!_repository.TryAdd(tx))
            {
                dupCount++;
                continue;
            }

            newCount++;
            inserted.Add(tx);

            // Flag for batched review if the categorizer is uncertain.
            // The UI presents all flagged items as a single review screen, not
            // individual pop-ups, to avoid overwhelming the user on first import.
            if (cat.NeedsReview)
            {
                reviewQueue.Add(new ReviewItem
                {
                    Transaction       = tx,
                    Confidence        = cat.Confidence,
                    SuggestedCategory = cat.Category,
                });
            }
        }

        return new ImportResult
        {
            NewTransactions = newCount,
            Duplicates      = dupCount,
            Unparseable     = unparseCount,
            ReviewQueue     = reviewQueue,
            Inserted        = inserted,
        };
    }
}
