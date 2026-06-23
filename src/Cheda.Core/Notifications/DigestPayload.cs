namespace Cheda.Core.Notifications;

public sealed class DigestPayload
{
    public required string Title { get; init; }
    public required string Body  { get; init; }
}
