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

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var lockService = _services.GetRequiredService<IAppLockService>();

        Page startPage = lockService.IsSetUp
            ? new LockPage(_services.GetRequiredService<LockViewModel>())
            : new OnboardingPage(_services.GetRequiredService<OnboardingViewModel>());

        return new Window(startPage);
    }
}
