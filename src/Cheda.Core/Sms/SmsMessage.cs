namespace Cheda.Core.Sms;

/// <summary>A raw SMS before parsing — source-agnostic.</summary>
public sealed class SmsMessage
{
    public required string Sender { get; init; }
    public required string Body { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Android telephony subscription_id of the SIM that received this message.
    /// Null on single-SIM devices or when not available (API &lt; 22).
    /// Used for dual-SIM tracking; not meaningful as a slot index across devices.
    /// </summary>
    public int? SimSlot { get; init; }
}
