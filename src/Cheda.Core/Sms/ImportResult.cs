using Cheda.Core.Models;

namespace Cheda.Core.Sms;

public sealed class ImportResult
{
    public int NewTransactions { get; init; }
    public int Duplicates { get; init; }

    /// <summary>
    /// Messages that were read from the inbox but could not be parsed as a financial
    /// transaction (OTP, marketing, unrecognized format). Not stored.
    /// </summary>
    public int Unparseable { get; init; }

    /// <summary>
    /// Parsed transactions whose categorization confidence is below the review threshold.
    /// Presented to the user as a single batched review screen, not individual prompts.
    /// </summary>
    public IReadOnlyList<ReviewItem> ReviewQueue { get; init; } = [];

    /// <summary>
    /// Every transaction freshly inserted in this import round.
    /// Used by AlertCoordinator to evaluate per-transaction alerts without a second DB query.
    /// </summary>
    public IReadOnlyList<Transaction> Inserted { get; init; } = [];

    public int Total => NewTransactions + Duplicates + Unparseable;
}
