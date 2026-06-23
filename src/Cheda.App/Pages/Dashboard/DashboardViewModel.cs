using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Analytics;
using Cheda.Core.Models;
using Cheda.Core.Storage;

namespace Cheda.App.Pages.Dashboard;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly IAnalyticsEngine       _analytics;

    [ObservableProperty] private decimal  _balance;
    [ObservableProperty] private decimal  _spentThisMonth;
    [ObservableProperty] private decimal  _receivedThisMonth;
    [ObservableProperty] private string   _monthLabel = "";
    [ObservableProperty] private IReadOnlyList<Transaction> _recent = [];
    [ObservableProperty] private int      _reviewCount;

    public DashboardViewModel(ITransactionRepository repo, IAnalyticsEngine analytics)
    {
        _repo      = repo;
        _analytics = analytics;
    }

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    private Task LoadAsync()
    {
        var now    = DateTimeOffset.Now;
        var all    = _repo.GetAll();
        var range  = new DateRange(new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset), now);
        var month  = _repo.GetInRange(range);

        var summary = _analytics.GetSummary(month, range);
        Balance           = _analytics.GetCurrentBalance(all) ?? 0m;
        SpentThisMonth    = summary.TotalExpenses;
        ReceivedThisMonth = summary.TotalIncome;
        MonthLabel        = now.ToString("MMMM yyyy");

        Recent = all.OrderByDescending(t => t.Timestamp).Take(5).ToList();
        ReviewCount = all.Count(t =>
            t.Category == Core.Categorization.DefaultCategories.Uncategorized ||
            t.CategoryConfidence < 0.6);

        return Task.CompletedTask;
    }

    public string FormatAmount(decimal amount) =>
        $"Ksh {amount:N0}";
}
