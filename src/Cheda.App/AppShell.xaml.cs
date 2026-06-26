using Cheda.App.Pages.Analytics;
using Cheda.App.Pages.Dashboard;
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
        AnalyticsPage    analytics,
        SettingsPage     settings)
    {
        // Opacity=0 so the fade-in from LockViewModel/OnboardingViewModel starts from invisible.
        Opacity = 0;
        InitializeComponent();

        HomeTab.ContentTemplate         = new DataTemplate(() => home);
        TransactionsTab.ContentTemplate = new DataTemplate(() => txns);
        PlanTab.ContentTemplate         = new DataTemplate(() => plan);
        InsightsTab.ContentTemplate     = new DataTemplate(() => analytics);
        SettingsTab.ContentTemplate     = new DataTemplate(() => settings);
    }

    protected override bool OnBackButtonPressed()
    {
        if (Navigation.ModalStack.Count > 0)
            return false;

        // CurrentPage is the pushed sub-page when one is open (e.g. TransactionEditPage,
        // CategoryPickerPage). Returning false for "unhandled" exits the app — so we must
        // pop manually and return true (consumed).
        if (CurrentPage is not (DashboardPage or TransactionsPage or PlanPage or AnalyticsPage or SettingsPage))
        {
            _ = Navigation.PopAsync();
            return true;
        }

        var tabBar = (TabBar)Items[0];
        if (tabBar.CurrentItem != tabBar.Items[0])
        {
            tabBar.CurrentItem = tabBar.Items[0];
            return true;
        }

        return false;
    }
}
