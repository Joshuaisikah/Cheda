using Cheda.App.Pages.Dashboard;
using Cheda.App.Pages.Insights;
using Cheda.App.Pages.Plan;
using Cheda.App.Pages.Settings;
using Cheda.App.Pages.Transactions;

namespace Cheda.App;

public partial class AppShell : Shell
{
    public AppShell(
        DashboardPage    home,
        TransactionsPage txns,
        PlanPage         plan,
        InsightsPage     insights,
        SettingsPage     settings)
    {
        // Opacity=0 so the fade-in from LockViewModel/OnboardingViewModel starts from invisible.
        Opacity = 0;
        InitializeComponent();

        HomeTab.ContentTemplate         = new DataTemplate(() => home);
        TransactionsTab.ContentTemplate = new DataTemplate(() => txns);
        PlanTab.ContentTemplate         = new DataTemplate(() => plan);
        InsightsTab.ContentTemplate     = new DataTemplate(() => insights);
        SettingsTab.ContentTemplate     = new DataTemplate(() => settings);
    }

    protected override bool OnBackButtonPressed()
    {
        var tabBar = (TabBar)Items[0];
        if (tabBar.CurrentItem != tabBar.Items[0])
        {
            tabBar.CurrentItem = tabBar.Items[0];
            return true;
        }
        return false;
    }
}
