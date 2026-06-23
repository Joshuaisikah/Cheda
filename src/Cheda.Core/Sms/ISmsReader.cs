namespace Cheda.Core.Sms;

public interface ISmsReader
{
    /// <summary>True if the READ_SMS runtime permission has been granted.</summary>
    bool HasPermission { get; }

    /// <summary>
    /// Reads inbox messages from known financial senders only.
    /// Pass <paramref name="since"/> to limit to messages after a cutoff date (first-run import).
    /// OTP and marketing messages are excluded by the parser downstream — this reader
    /// filters only by sender address.
    /// </summary>
    IReadOnlyList<SmsMessage> ReadInbox(DateTimeOffset? since = null);
}
