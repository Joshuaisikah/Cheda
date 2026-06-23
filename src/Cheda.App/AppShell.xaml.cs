using Cheda.App.Pages.Analytics;
using Cheda.App.Pages.Dashboard;
using Cheda.App.Pages.Review;
using Cheda.App.Pages.Settings;
using Cheda.App.Pages.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace Cheda.App;

public partial class AppShell : Shell
{
    public AppShell(
        DashboardPage    home,
        TransactionsPage txns,
        ReviewQueuePage  review,
        AnalyticsPage    analytics,
        SettingsPage     settings)
    {
        InitializeComponent();

        // Wire DI-resolved pages to shell tabs
        HomeTab.ContentTemplate         = new DataTemplate(() => home);
        TransactionsTab.ContentTemplate = new DataTemplate(() => txns);
        ReviewTab.ContentTemplate       = new DataTemplate(() => review);
        AnalyticsTab.ContentTemplate    = new DataTemplate(() => analytics);
        SettingsTab.ContentTemplate     = new DataTemplate(() => settings);
    }
}
