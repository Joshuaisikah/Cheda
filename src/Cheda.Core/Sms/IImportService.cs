namespace Cheda.Core.Sms;

public interface IImportService
{
    /// <summary>
    /// Reads the SMS inbox (filtered to financial senders), parses, categorizes,
    /// deduplicates, and persists all transactions.
    /// Pass <paramref name="since"/> to limit the look-back window (first-run import
    /// and manual rescans). Returns a batched review queue for low-confidence items.
    /// </summary>
    Task<ImportResult> ImportInboxAsync(DateTimeOffset? since = null, CancellationToken ct = default);

    /// <summary>
    /// Processes a single SMS delivered by the real-time BroadcastReceiver.
    /// No-ops silently if the message is not a recognizable financial transaction.
    /// </summary>
    Task<ImportResult> ProcessSingleAsync(SmsMessage message, CancellationToken ct = default);
}
