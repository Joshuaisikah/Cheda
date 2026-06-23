using Android.Content;
using AndroidX.Work;
using Cheda.Core.Analytics;
using Cheda.Core.Notifications;
using Cheda.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace Cheda.App.Platforms.Android.Notifications;

/// <summary>
/// WorkManager worker that generates and posts the daily digest notification.
/// Runs on a background thread managed by WorkManager — Task.GetAwaiter().GetResult()
/// blocking is intentional and safe here.
///
/// Reliable execution is not guaranteed on OEM devices with aggressive battery
/// optimisation. Real-time transaction alerts (SmsBroadcastReceiver) are the primary
/// notification mechanism; digests are supplementary.
/// </summary>
public sealed class DigestWorker : Worker
{
    public DigestWorker(Context context, WorkerParameters workerParams)
        : base(context, workerParams) { }

    public override Result DoWork()
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is null) return Result.InvokeSuccess();

            var notif    = services.GetService<INotificationService>();
            var settings = services.GetService<NotificationSettingsService>();
            if (notif is null || settings is null) return Result.InvokeSuccess();

            if (!settings.Load().DailyDigestEnabled) return Result.InvokeSuccess();

            var txRepo = services.GetService<ITransactionRepository>();
            if (txRepo is null) return Result.InvokeSuccess();

            var asOf    = DateTimeOffset.Now;
            var range   = DateRange.ForMonth(asOf.Year, asOf.Month, asOf.Offset);
            var summary = new AnalyticsEngine().GetSummary(txRepo.GetAll(), range);

            var body = $"Spent Ksh {summary.TotalExpenses:N0} this month";
            if (summary.CurrentBalance.HasValue)
                body += $" — Balance Ksh {summary.CurrentBalance:N0}";

            notif.SendDigestAsync(new DigestPayload
            {
                Title = $"Daily Digest — {asOf.LocalDateTime:d MMM}",
                Body  = body,
            }).GetAwaiter().GetResult();

            return Result.InvokeSuccess();
        }
        catch
        {
            return Result.InvokeFailure();
        }
    }
}
