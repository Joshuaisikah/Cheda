namespace Cheda.Core.Notifications;

public sealed class AppAlert
{
    public required AlertType Type  { get; init; }
    public required string Title    { get; init; }
    public required string Body     { get; init; }
    public string? Category         { get; init; }
}
