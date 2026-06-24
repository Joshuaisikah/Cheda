using Cheda.App.Pages.Lock;
using Cheda.App.Pages.Onboarding;
using Cheda.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Cheda.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

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
        Page startPage  = lockService.IsSetUp
            ? _services.GetRequiredService<LockPage>()
            : _services.GetRequiredService<OnboardingPage>();
        return new Window(startPage);
    }

    protected override async void OnResume()
    {
        base.OnResume();

        var lockService = _services.GetRequiredService<IAppLockService>();
        if (!lockService.IsSetUp) return;

        // Only re-lock when the Shell is the active page (user already authenticated once).
        if (Windows[0].Page is not AppShell) return;

        var nav = Windows[0].Page.Navigation;
        if (nav.ModalStack.Count > 0) return; // lock modal already showing

        lockService.Lock();
        Windows[0].Page.Opacity = 0;
        var lockPage = _services.GetRequiredService<LockPage>();
        await Windows[0].Page.Navigation.PushModalAsync(lockPage, animated: false);
    }
}
