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
    [ObservableProperty] private string   _momLabel       = "";
    [ObservableProperty] private string   _chartMonthLabel = "";
    [ObservableProperty] private IReadOnlyList<CategoryRow>    _categories = [];
    [ObservableProperty] private IReadOnlyList<TopCounterparty> _top       = [];
    [ObservableProperty] private IReadOnlyList<DailyBar>       _dailyBars  = [];

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
        ChartMonthLabel = now.ToString("MMMM yyyy");

        var mom     = _analytics.GetMonthOverMonth(all, now.Year, now.Month, now.Offset);
        MomChange   = (decimal)mom.ExpenseChangePercent;
        MomLabel    = mom.ExpenseChangePercent >= 0
            ? $"+{mom.ExpenseChangePercent:N1}% vs last month"
            : $"{mom.ExpenseChangePercent:N1}% vs last month";

        var breakdown = _analytics.GetCategoryBreakdown(month, range);
        var catMax    = breakdown.Count > 0 ? breakdown.Max(b => b.Total) : 1m;
        Categories = breakdown
            .OrderByDescending(b => b.Total)
            .Select(b => new CategoryRow
            {
                Category = b.Category,
                Total    = b.Total,
                Share    = catMax > 0 ? (double)(b.Total / catMax) : 0,
            })
            .ToList();

        Top = _analytics.GetTopCounterparties(month, range, top: 5);

        // Daily spending chart — bar heights proportional to max daily spend
        var dailyPoints = _analytics.GetTrend(month, range, TrendGranularity.Daily);
        var maxDaily    = dailyPoints.Count > 0 ? dailyPoints.Max(p => p.Expenses) : 1m;
        const double MaxBarHeight = 72;

        // Fill every day of the month, even days with no data = zero bar
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var byDay = dailyPoints.ToDictionary(p => p.PeriodStart.Day, p => p.Expenses);
        DailyBars = Enumerable.Range(1, daysInMonth).Select(day =>
        {
            var spent  = byDay.GetValueOrDefault(day, 0m);
            var height = maxDaily > 0 ? (double)(spent / maxDaily) * MaxBarHeight : 0;
            var isFuture = day > now.Day;
            return new DailyBar
            {
                Day        = day,
                Spent      = spent,
                BarHeight  = isFuture ? 0 : Math.Max(height, spent > 0 ? 2 : 0),
                SpacerHeight = MaxBarHeight - (isFuture ? 0 : Math.Max(height, spent > 0 ? 2 : 0)),
                IsToday    = day == now.Day,
                DayLabel   = day % 5 == 0 || day == 1 ? day.ToString() : "",
                BarColor   = day == now.Day ? Color.FromArgb("#00875A")
                           : spent > 0     ? Color.FromArgb("#34D399")
                                           : Color.FromArgb("#1E293B"),
            };
        }).ToList();

        return Task.CompletedTask;
    }
}

public sealed class CategoryRow
{
    public string  Category { get; init; } = "";
    public decimal Total    { get; init; }
    public double  Share    { get; init; }
}

public sealed class DailyBar
{
    public int    Day          { get; init; }
    public decimal Spent       { get; init; }
    public double BarHeight    { get; init; }
    public double SpacerHeight { get; init; }
    public bool   IsToday      { get; init; }
    public string DayLabel     { get; init; } = "";
    public Color  BarColor     { get; init; } = Colors.Transparent;
}
