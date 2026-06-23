using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Analytics;
using Cheda.Core.Storage;

namespace Cheda.App.Pages.Analytics;

public partial class AnalyticsViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly IAnalyticsEngine       _analytics;

    [ObservableProperty] private decimal  _totalSpent;
    [ObservableProperty] private decimal  _totalIncome;
    [ObservableProperty] private decimal  _totalFees;
    [ObservableProperty] private decimal  _momChange;
    [ObservableProperty] private string   _momLabel     = "";
    [ObservableProperty] private IReadOnlyList<CategoryRow> _categories = [];
    [ObservableProperty] private IReadOnlyList<TopCounterparty> _top    = [];

    public AnalyticsViewModel(ITransactionRepository repo, IAnalyticsEngine analytics)
    {
        _repo      = repo;
        _analytics = analytics;
    }

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    private Task LoadAsync()
    {
        var now   = DateTimeOffset.Now;
        var range = new DateRange(new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset), now);
        var all   = _repo.GetAll();
        var month = _repo.GetInRange(range);

        var summary  = _analytics.GetSummary(month, range);
        TotalSpent   = summary.TotalExpenses;
        TotalIncome  = summary.TotalIncome;
        TotalFees    = _analytics.GetFeeAnalytics(month, range).TotalFees;

        var mom     = _analytics.GetMonthOverMonth(all, now.Year, now.Month, now.Offset);
        MomChange   = (decimal)mom.ExpenseChangePercent;
        MomLabel    = mom.ExpenseChangePercent >= 0
            ? $"+{mom.ExpenseChangePercent:N1}% vs last month"
            : $"{mom.ExpenseChangePercent:N1}% vs last month";

        var breakdown = _analytics.GetCategoryBreakdown(month, range);
        var max       = breakdown.Count > 0 ? breakdown.Max(b => b.Total) : 1m;
        Categories = breakdown
            .OrderByDescending(b => b.Total)
            .Select(b => new CategoryRow
            {
                Category = b.Category,
                Total    = b.Total,
                Share    = max > 0 ? (double)(b.Total / max) : 0,
            })
            .ToList();

        Top = _analytics.GetTopCounterparties(month, range, top: 5);
        return Task.CompletedTask;
    }
}

public sealed class CategoryRow
{
    public string  Category { get; init; } = "";
    public decimal Total    { get; init; }
    public double  Share    { get; init; }
}
