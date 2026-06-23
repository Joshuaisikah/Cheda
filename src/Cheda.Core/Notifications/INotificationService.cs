namespace Cheda.Core.Notifications;

public interface INotificationService
{
    Task SendAlertAsync(AppAlert alert, CancellationToken ct = default);
    Task SendDigestAsync(DigestPayload digest, CancellationToken ct = default);
}
