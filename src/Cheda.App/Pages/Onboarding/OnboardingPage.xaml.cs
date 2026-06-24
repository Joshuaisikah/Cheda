namespace Cheda.App.Pages.Onboarding;

public partial class OnboardingPage : ContentPage
{
    public OnboardingPage(OnboardingViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    // Prevent back-swipe from skipping onboarding
    protected override bool OnBackButtonPressed() => true;
}
