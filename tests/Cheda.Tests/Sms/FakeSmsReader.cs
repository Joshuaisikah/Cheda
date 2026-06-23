using Cheda.Core.Sms;

namespace Cheda.Tests.Sms;

/// <summary>Test double for ISmsReader — returns a pre-configured list of messages.</summary>
public sealed class FakeSmsReader : ISmsReader
{
    private readonly List<SmsMessage> _messages;

    public FakeSmsReader(IEnumerable<SmsMessage>? messages = null) =>
        _messages = [.. messages ?? []];

    public bool HasPermission { get; set; } = true;

    public IReadOnlyList<SmsMessage> ReadInbox(DateTimeOffset? since = null)
    {
        if (since is null) return _messages;
        return [.. _messages.Where(m => m.Timestamp >= since.Value)];
    }

    public static SmsMessage Mpesa(string body, DateTimeOffset? at = null, int? simSlot = null) =>
        new()
        {
            Sender    = "MPESA",
            Body      = body,
            Timestamp = at ?? DateTimeOffset.UtcNow,
            SimSlot   = simSlot,
        };
}
