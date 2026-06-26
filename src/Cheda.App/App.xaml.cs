using Cheda.App.Pages.Lock;
using Cheda.App.Pages.Onboarding;
using Cheda.Core.Security;
using Cheda.Core.Sms;
using Cheda.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Cheda.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private DateTimeOffset _backgroundedAt = DateTimeOffset.MinValue;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    // Start with the auth page as the Window page so the Shell is never rendered before auth.
    // LockViewModel / OnboardingViewModel swap Window.Page to AppShell after successful auth.
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var lockService = _services.GetRequiredService<IAppLockService>();
        if (!lockService.IsSetUp)
            return new Window(_services.GetRequiredService<OnboardingPage>());

        var lockPage = _services.GetRequiredService<LockPage>();
        return new Window(lockPage);
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // App is always dark — no theme toggle.
        UserAppTheme = AppTheme.Dark;

        // Pre-warm AppShell while the lock screen is showing so that window.Page = shell
        // in DismissModal is instant after auth (first-time construction costs ~700ms).
        _ = _services.GetRequiredService<AppShell>();

        // Request POST_NOTIFICATIONS permission on first run (Android 13+).
        try
        {
            var status = await Permissions.CheckStatusAsync<Pages.Settings.NotificationPermission>();
            if (status != PermissionStatus.Granted)
                await Permissions.RequestAsync<Pages.Settings.NotificationPermission>();
        }
        catch { /* non-critical */ }

        // Silently scan for any SMS that arrived before the app was first opened.
        _ = AutoScanAsync();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        _backgroundedAt = DateTimeOffset.UtcNow;
    }

    protected override async void OnResume()
    {
        base.OnResume();

        // Silently scan for SMS that arrived while the app was in the background.
        // Runs in the background while the lock screen shows — data is ready by unlock.
        _ = AutoScanAsync();

        var lockService = _services.GetRequiredService<IAppLockService>();
        if (!lockService.IsSetUp) return;

        // Only re-lock when the Shell is the active page (user already authenticated once).
        if (Windows[0].Page is not AppShell) return;

        var nav = Windows[0].Page.Navigation;
        if (nav.ModalStack.Count > 0) return; // lock modal already showing

        // Respect the configured lock delay — don't lock if background time is within grace period.
        var settings      = _services.GetRequiredService<ISettingsRepository>();
        var delayMinutes  = int.TryParse(settings.Get("LockDelayMinutes"), out var d) ? d : 0;
        var elapsed       = DateTimeOffset.UtcNow - _backgroundedAt;
        if (delayMinutes > 0 && elapsed.TotalMinutes < delayMinutes) return;

        lockService.Lock();
        Windows[0].Page.Opacity = 0;

        // Pop any open sub-pages (e.g. TransactionEditPage) before locking so that
        // after unlock the user lands on the tab root, not a stale detail screen.
        while (nav.NavigationStack.Count > 0)
            await nav.PopAsync(animated: false);

        var lockPage = _services.GetRequiredService<LockPage>();
        await Windows[0].Page.Navigation.PushModalAsync(lockPage, animated: false);
    }

    private async Task AutoScanAsync()
    {
        try
        {
            var import = _services.GetService<IImportService>();
            var reader = _services.GetService<ISmsReader>();
            if (import is null || reader is null || !reader.HasPermission) return;

            var settings = _services.GetRequiredService<ISettingsRepository>();
            var lastMs   = settings.Get("AutoScanLastMs");
            DateTimeOffset? since = long.TryParse(lastMs, out var ms)
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                : null;

            // Record scan time BEFORE the query so concurrent arrivals are never missed.
            settings.Set("AutoScanLastMs", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

            await import.ImportInboxAsync(since);
        }
        catch { /* non-critical — user can always use the Settings scan button */ }
    }
}
