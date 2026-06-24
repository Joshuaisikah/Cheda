namespace Cheda.Core.Sms;

public interface ISmsReader
{
    /// <summary>True if the READ_SMS runtime permission has been granted.</summary>
    bool HasPermission { get; }

    /// <summary>Total raw SMS rows scanned in the last ReadInbox call (diagnostic).</summary>
    int LastRawCount { get; }

    /// <summary>Error message from the last ReadInbox call, or null on success.</summary>
    string? LastError { get; }

    /// <summary>
    /// Reads inbox messages from known financial senders only.
    /// Pass <paramref name="since"/> to limit to messages after a cutoff date.
    /// The parser rejects OTP and marketing messages downstream.
    /// </summary>
    IReadOnlyList<SmsMessage> ReadInbox(DateTimeOffset? since = null);
}
