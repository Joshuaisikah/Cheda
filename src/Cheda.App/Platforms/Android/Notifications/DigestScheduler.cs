using AndroidX.Work;
using Cheda.Core.Notifications;

namespace Cheda.App.Platforms.Android.Notifications;

/// <summary>
/// Schedules (or cancels) the daily digest WorkManager job.
/// Call Schedule() from App.OnStart() after DB initialisation.
/// Uses ExistingPeriodicWorkPolicy.Keep so rescheduling on every launch
/// does not reset the next-run clock.
/// </summary>
public sealed class DigestScheduler
{
    private const string WorkName = "cheda_daily_digest";

    public void Schedule(NotificationSettings settings)
    {
        var ctx = global::Android.App.Application.Context;
        var wm  = WorkManager.GetInstance(ctx);

        if (!settings.DailyDigestEnabled)
        {
            wm.CancelUniqueWork(WorkName);
            return;
        }

        var request = new PeriodicWorkRequest.Builder(
                Java.Lang.Class.FromType(typeof(DigestWorker)),
                24, Java.Util.Concurrent.TimeUnit.Hours)
            .Build();

        wm.EnqueueUniquePeriodicWork(WorkName, ExistingPeriodicWorkPolicy.Keep, request);
    }

    public void Cancel()
    {
        var ctx = global::Android.App.Application.Context;
        WorkManager.GetInstance(ctx).CancelUniqueWork(WorkName);
    }
}
