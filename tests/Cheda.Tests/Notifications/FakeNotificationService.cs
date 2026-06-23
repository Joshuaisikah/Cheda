using Cheda.Core.Notifications;

namespace Cheda.Tests.Notifications;

public sealed class FakeNotificationService : INotificationService
{
    public List<AppAlert>      SentAlerts  { get; } = [];
    public List<DigestPayload> SentDigests { get; } = [];

    public Task SendAlertAsync(AppAlert alert, CancellationToken ct = default)
    {
        SentAlerts.Add(alert);
        return Task.CompletedTask;
    }

    public Task SendDigestAsync(DigestPayload digest, CancellationToken ct = default)
    {
        SentDigests.Add(digest);
        return Task.CompletedTask;
    }
}
