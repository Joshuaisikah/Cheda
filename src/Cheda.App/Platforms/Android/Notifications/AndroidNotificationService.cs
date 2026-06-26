using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Cheda.Core.Notifications;
using Cheda.Core.Storage;

namespace Cheda.App.Platforms.Android.Notifications;

internal sealed class AndroidNotificationService : INotificationService
{
    private const string AlertChannelId   = "cheda_alerts";
    private const string DigestChannelId  = "cheda_digest";
    private const string BudgetChannelId  = "cheda_budget";

    // Brand terracotta colour used as notification accent
    private static readonly int BrandColor = global::Android.Graphics.Color.ParseColor("#F4A28A").ToArgb();

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

        var (channelId, priority, title, body) = BuildContent(alert);
        Post(channelId, priority, title, body);
        IncrementDailyCount();
        return Task.CompletedTask;
    }

    public Task SendDigestAsync(DigestPayload digest, CancellationToken ct = default)
    {
        var s = _settingsSvc.Load();
        if (!s.DailyDigestEnabled) return Task.CompletedTask;
        if (IsQuietHours(s))       return Task.CompletedTask;

        Post(DigestChannelId, NotificationCompat.PriorityDefault,
             "📊 " + digest.Title, digest.Body);
        return Task.CompletedTask;
    }

    // ── Content builders ──────────────────────────────────────────────────────

    private static (string channelId, int priority, string title, string body) BuildContent(AppAlert alert)
    {
        return alert.Type switch
        {
            AlertType.NewTransaction => (
                AlertChannelId, NotificationCompat.PriorityDefault,
                FormatNewTxTitle(alert),
                alert.Body),

            AlertType.LargeTransaction => (
                AlertChannelId, NotificationCompat.PriorityHigh,
                "⚠️ " + alert.Title,
                alert.Body),

            AlertType.FulizaDrawdown => (
                AlertChannelId, NotificationCompat.PriorityHigh,
                "⚡ " + alert.Title,
                alert.Body),

            AlertType.BudgetBreach => (
                BudgetChannelId, NotificationCompat.PriorityHigh,
                "📊 " + alert.Title,
                alert.Body),

            _ => (AlertChannelId, NotificationCompat.PriorityDefault, alert.Title, alert.Body),
        };
    }

    private static string FormatNewTxTitle(AppAlert alert)
    {
        // alert.Title is already "M-PESA Ksh 500 sent" or "M-PESA Ksh 1,200 received"
        var lower = alert.Title.ToLowerInvariant();
        var emoji = lower.Contains("received") ? "⬇️" : "⬆️";
        return $"{emoji} {alert.Title}";
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

    private static void Post(string channelId, int priority, string title, string body)
    {
        EnsureChannels();

        var ctx = global::Android.App.Application.Context;
        if (ctx is null) return;

        // Tap → open app (bring existing instance to front or create new one)
        var openIntent = new Intent(ctx, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var flags = Build.VERSION.SdkInt >= BuildVersionCodes.M
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;
        var pendingIntent = PendingIntent.GetActivity(ctx, 0, openIntent, flags);

        NotificationCompat.Builder builder = new(ctx, channelId);
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetColor(BrandColor);
        builder.SetContentTitle(title);
        builder.SetContentText(body);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(body));
        builder.SetContentIntent(pendingIntent);
        builder.SetAutoCancel(true);
        builder.SetPriority(priority);
        builder.SetCategory(channelId == BudgetChannelId
            ? NotificationCompat.CategoryStatus
            : NotificationCompat.CategoryMessage);

        var notif = builder.Build();
        if (notif is null) return;

        if (ctx.GetSystemService(Context.NotificationService) is NotificationManager mgr)
            mgr.Notify(System.Threading.Interlocked.Increment(ref _nextId), notif);
    }

    private static void EnsureChannels()
    {
        if (_channelsReady) return;

        var ctx = global::Android.App.Application.Context;
        if (ctx is null) return;

        var mgr = ctx.GetSystemService(Context.NotificationService) as NotificationManager;
        if (mgr is null) return;

        mgr.CreateNotificationChannel(new NotificationChannel(
            AlertChannelId, "M-PESA Alerts", NotificationImportance.High)
        {
            Description = "Real-time alerts: received money, sent money, large transactions.",
        });
        mgr.CreateNotificationChannel(new NotificationChannel(
            BudgetChannelId, "Budget Warnings", NotificationImportance.High)
        {
            Description = "Alerts when spending approaches or exceeds a budget limit.",
        });
        mgr.CreateNotificationChannel(new NotificationChannel(
            DigestChannelId, "Daily Summary", NotificationImportance.Default)
        {
            Description = "Daily spending digest sent once per day.",
        });
        _channelsReady = true;
    }
}
