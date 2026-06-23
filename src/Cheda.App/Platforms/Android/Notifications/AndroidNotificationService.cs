using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Cheda.Core.Notifications;
using Cheda.Core.Storage;

namespace Cheda.App.Platforms.Android.Notifications;

/// <summary>
/// Delivers local notifications via Android's NotificationManager (no third-party SDK needed).
/// Quiet-hours and daily-cap enforcement happen here so the Core evaluator stays pure.
///
/// Note on reliable delivery: real-time alerts (triggered by SmsBroadcastReceiver) are the
/// reliable backbone. Scheduled digests via WorkManager are best-effort — OEMs aggressively
/// kill background work. Tell users in Settings to disable battery optimisation for Cheda
/// if they want reliable daily digests.
/// </summary>
internal sealed class AndroidNotificationService : INotificationService
{
    private const string AlertChannelId  = "cheda_alerts";
    private const string DigestChannelId = "cheda_digest";

    private readonly NotificationSettingsService _settingsSvc;
    private readonly ISettingsRepository         _settingsRepo;
    private static   int                         _nextId       = 2000;
    private static   bool                        _channelsReady;

    public AndroidNotificationService(
        NotificationSettingsService settingsSvc,
        ISettingsRepository settingsRepo)
    {
        _settingsSvc  = settingsSvc;
        _settingsRepo = settingsRepo;
    }

    public Task SendAlertAsync(AppAlert alert, CancellationToken ct = default)
    {
        var s = _settingsSvc.Load();
        if (!IsTypeEnabled(alert.Type, s)) return Task.CompletedTask;
        if (IsQuietHours(s))               return Task.CompletedTask;
        if (IsDailyCapped(s))              return Task.CompletedTask;

        Post(AlertChannelId, alert.Title, alert.Body, NotificationCompat.PriorityHigh);
        IncrementDailyCount();
        return Task.CompletedTask;
    }

    public Task SendDigestAsync(DigestPayload digest, CancellationToken ct = default)
    {
        var s = _settingsSvc.Load();
        if (!s.DailyDigestEnabled) return Task.CompletedTask;
        if (IsQuietHours(s))       return Task.CompletedTask;

        Post(DigestChannelId, digest.Title, digest.Body, NotificationCompat.PriorityDefault);
        return Task.CompletedTask;
    }

    // ── Gating helpers ────────────────────────────────────────────────────────

    private static bool IsTypeEnabled(AlertType type, NotificationSettings s) => type switch
    {
        AlertType.BudgetBreach     => s.BudgetBreachEnabled,
        AlertType.LargeTransaction => s.LargeTransactionEnabled,
        AlertType.FulizaDrawdown   => s.FulizaAlertEnabled,
        AlertType.NewTransaction   => s.NewTransactionEnabled,
        AlertType.DailyDigest      => s.DailyDigestEnabled,
        AlertType.WeeklyReport     => s.WeeklyReportEnabled,
        _                          => true,
    };

    private static bool IsQuietHours(NotificationSettings s)
    {
        if (!s.QuietHoursEnabled) return false;
        var now = TimeOnly.FromDateTime(DateTime.Now);
        // QuietStart > QuietEnd means the window wraps midnight (e.g. 22:00–07:00).
        return s.QuietStart <= s.QuietEnd
            ? now >= s.QuietStart && now < s.QuietEnd
            : now >= s.QuietStart || now  < s.QuietEnd;
    }

    private bool IsDailyCapped(NotificationSettings s)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if ((_settingsRepo.Get("notif_cap_date") ?? "") != today)
        {
            _settingsRepo.Set("notif_cap_date",  today);
            _settingsRepo.Set("notif_cap_count", "0");
            return false;
        }
        var count = int.TryParse(_settingsRepo.Get("notif_cap_count"), out var n) ? n : 0;
        return count >= s.DailyNotificationCap;
    }

    private void IncrementDailyCount()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        _settingsRepo.Set("notif_cap_date", today);
        var count = int.TryParse(_settingsRepo.Get("notif_cap_count"), out var n) ? n : 0;
        _settingsRepo.Set("notif_cap_count", (count + 1).ToString());
    }

    // ── Android plumbing ──────────────────────────────────────────────────────

    private static void Post(string channelId, string title, string body, int priority)
    {
        EnsureChannels();

        var ctx = global::Android.App.Application.Context;
        if (ctx is null) return;

        // Break the fluent chain — each Xamarin Java binding method returns Builder? so
        // chaining causes CS8602 on every call. Calling on a typed variable avoids this.
        NotificationCompat.Builder builder = new(ctx, channelId);
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo);
        builder.SetContentTitle(title);
        builder.SetContentText(body);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(body));
        builder.SetAutoCancel(true);
        builder.SetPriority(priority);
        var notif = builder.Build();

        if (notif is null) return;

        if (ctx.GetSystemService(Context.NotificationService) is NotificationManager mgr)
            mgr.Notify(System.Threading.Interlocked.Increment(ref _nextId), notif);
    }

    private static void EnsureChannels()
    {
        if (_channelsReady) return;
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) { _channelsReady = true; return; }

        var ctx = global::Android.App.Application.Context;
        if (ctx is null) return;

        var mgr = ctx.GetSystemService(Context.NotificationService) as NotificationManager;
        if (mgr is null) return;

        mgr.CreateNotificationChannel(new NotificationChannel(
            AlertChannelId, "Transaction Alerts", NotificationImportance.High)
        {
            Description = "Budget breaches, large transactions, and Fuliza alerts.",
        });
        mgr.CreateNotificationChannel(new NotificationChannel(
            DigestChannelId, "Daily Digest", NotificationImportance.Default)
        {
            Description = "Daily spending summary.",
        });
        _channelsReady = true;
    }
}
