using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Analytics;
using Cheda.Core.Bills;
using Cheda.Core.Budgets;
using Cheda.Core.Categorization;
using Cheda.Core.Insights;
using Cheda.Core.Models;
using Cheda.Core.Storage;

namespace Cheda.App.Pages.Insights;

public partial class InsightsViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly IBudgetStore           _budgetStore;
    private readonly IBillStore             _billStore;
    private readonly IInsightsEngine        _engine;
    private readonly IAnalyticsEngine       _analytics;

    // ── Month selector ────────────────────────────────────────────────────
    public List<MonthOption>  MonthOptions   { get; } = BuildMonthOptions();
    [ObservableProperty] private MonthOption? _selectedMonth;

    // ── Analytics summary ─────────────────────────────────────────────────
    [ObservableProperty] private decimal  _totalSpent;
    [ObservableProperty] private decimal  _totalIncome;
    [ObservableProperty] private decimal  _totalFees;
    [ObservableProperty] private decimal  _avgDailySpend;
    [ObservableProperty] private decimal  _totalSavings;
    [ObservableProperty] private string   _momLabel        = "";
    [ObservableProperty] private string   _chartMonthLabel = "";
    [ObservableProperty] private IReadOnlyList<CategoryRow>     _categories   = [];
    [ObservableProperty] private IReadOnlyList<CategoryCompRow> _catCompare   = [];
    [ObservableProperty] private IReadOnlyList<TopCounterparty> _top          = [];
    [ObservableProperty] private IReadOnlyList<TopCounterparty> _topSent      = [];
    [ObservableProperty] private IReadOnlyList<DailyBar>        _dailyBars    = [];

    // ── Insights ──────────────────────────────────────────────────────────
    [ObservableProperty] private IReadOnlyList<InsightRow> _insights = [];
    [ObservableProperty] private string _periodLabel = "";

    public InsightsViewModel(
        ITransactionRepository repo,
        IBudgetStore budgetStore,
        IBillStore billStore,
        IInsightsEngine engine,
        IAnalyticsEngine analytics)
    {
        _repo        = repo;
        _budgetStore = budgetStore;
        _billStore   = billStore;
        _engine      = engine;
        _analytics   = analytics;

        // Default to the most recent month.
        _selectedMonth = MonthOptions[0];
    }

    partial void OnSelectedMonthChanged(MonthOption? value)
    {
        if (!IsBusy) _ = RunAsync(LoadAsync);
    }

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    private Task LoadAsync()
    {
        var now    = DateTimeOffset.Now;
        var option = SelectedMonth ?? MonthOptions[0];

        // Selected month boundaries
        var thisStart  = new DateTimeOffset(option.Year, option.Month, 1, 0, 0, 0, now.Offset);
        var thisEnd    = thisStart.AddMonths(1);
        var prevStart  = thisStart.AddMonths(-1);
        var current    = new DateRange(thisStart, thisEnd < now ? thisEnd : now);
        var previous   = new DateRange(prevStart, thisStart);

        var all   = _repo.GetAll();
        var month = _repo.GetInRange(current);

        // ── Summary ───────────────────────────────────────────────────────
        var summary = _analytics.GetSummary(month, current);
        TotalSpent  = summary.TotalExpenses;
        TotalIncome = summary.TotalIncome;
        TotalFees   = _analytics.GetFeeAnalytics(month, current).TotalFees;
        ChartMonthLabel = option.Label;

        // Average daily spend: divide by days elapsed in the month (or full month if past).
        var daysPassed = option.Year == now.Year && option.Month == now.Month
            ? now.Day
            : DateTime.DaysInMonth(option.Year, option.Month);
        AvgDailySpend = daysPassed > 0 ? TotalSpent / daysPassed : 0m;

        // ── Savings: M-Shwari + KCB M-Pesa + Zidii + anything categorized as savings ──
        TotalSavings = month
            .Where(t => t.Type == TransactionType.MShwari ||
                        t.Type == TransactionType.KcbMpesa ||
                        t.Type == TransactionType.Zidii ||
                        t.Category == DefaultCategories.Savings ||
                        t.Category == DefaultCategories.MShwari ||
                        t.Category == DefaultCategories.KcbMpesa ||
                        t.Category == DefaultCategories.ZidiiSavings ||
                        t.Category == DefaultCategories.SaccoChama)
            .Sum(t => t.Amount);

        // ── Month-over-month change label ─────────────────────────────────
        var mom  = _analytics.GetMonthOverMonth(all, option.Year, option.Month, now.Offset);
        MomLabel = mom.ExpenseChangePercent >= 0
            ? $"+{mom.ExpenseChangePercent:N1}% vs {prevStart:MMMM}"
            : $"{mom.ExpenseChangePercent:N1}% vs {prevStart:MMMM}";

        // ── Category breakdown ────────────────────────────────────────────
        var breakdown = _analytics.GetCategoryBreakdown(month, current);
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

        // ── Category comparison vs previous month ─────────────────────────
        var prevMonth  = _repo.GetInRange(previous);
        var prevBreak  = _analytics.GetCategoryBreakdown(prevMonth, previous)
            .ToDictionary(b => b.Category, b => b.Total);
        CatCompare = breakdown
            .OrderByDescending(b => b.Total)
            .Take(6)
            .Select(b =>
            {
                var prev  = prevBreak.GetValueOrDefault(b.Category, 0m);
                var delta = prev > 0 ? (b.Total - prev) / prev * 100m : 0m;
                return new CategoryCompRow
                {
                    Category  = b.Category,
                    Total     = b.Total,
                    Delta     = delta,
                    DeltaText = delta == 0m ? "—" : delta > 0 ? $"+{delta:N0}%" : $"{delta:N0}%",
                    DeltaColor = delta <= 0 ? Color.FromArgb("#22C55E")
                               : delta > 30  ? Color.FromArgb("#EF4444")
                                              : Color.FromArgb("#F59E0B"),
                };
            })
            .ToList();

        // ── Top merchants (till / paybill) ────────────────────────────────
        Top = _analytics.GetTopCounterparties(month, current, top: 5);

        // ── Top send-money recipients ─────────────────────────────────────
        TopSent = month
            .Where(t => t.Type == TransactionType.Sent && t.Counterparty is not null)
            .GroupBy(t => t.Counterparty!)
            .Select(g => new TopCounterparty(g.Key, g.Sum(t => t.Amount), g.Count()))
            .OrderByDescending(c => c.Total)
            .Take(5)
            .ToList();

        // ── Daily bar chart ───────────────────────────────────────────────
        var dailyPoints = _analytics.GetTrend(month, current, TrendGranularity.Daily);
        var maxDaily    = dailyPoints.Count > 0 ? dailyPoints.Max(p => p.Expenses) : 1m;
        const double MaxBarH = 72;
        var byDay = dailyPoints.ToDictionary(p => p.PeriodStart.Day, p => p.Expenses);
        var daysInMonth = DateTime.DaysInMonth(option.Year, option.Month);
        var isCurrentMonth = option.Year == now.Year && option.Month == now.Month;
        DailyBars = Enumerable.Range(1, daysInMonth).Select(day =>
        {
            var spent  = byDay.GetValueOrDefault(day, 0m);
            var h      = maxDaily > 0 ? (double)(spent / maxDaily) * MaxBarH : 0;
            var future = isCurrentMonth && day > now.Day;
            var barH   = future ? 0 : Math.Max(h, spent > 0 ? 2 : 0);
            return new DailyBar
            {
                Day          = day,
                Spent        = spent,
                BarHeight    = barH,
                SpacerHeight = MaxBarH - barH,
                IsToday      = isCurrentMonth && day == now.Day,
                DayLabel     = day % 5 == 0 || day == 1 ? day.ToString() : "",
                BarColor     = isCurrentMonth && day == now.Day ? Color.FromArgb("#00875A")
                             : spent > 0                        ? Color.FromArgb("#34D399")
                                                                : Color.FromArgb("#1E293B"),
            };
        }).ToList();

        // ── Insights (always based on current calendar month) ─────────────
        var curNow    = DateTimeOffset.Now;
        var curStart  = new DateTimeOffset(curNow.Year, curNow.Month, 1, 0, 0, 0, curNow.Offset);
        var curRange  = new DateRange(curStart, curNow);
        var curPrev   = new DateRange(curStart.AddMonths(-1), curStart);
        var curLbl    = curStart.ToString("MMMM yyyy");
        var prvLbl    = curStart.AddMonths(-1).ToString("MMMM yyyy");
        PeriodLabel   = $"{curLbl}  vs  {prvLbl}";

        var budgets     = _budgetStore.GetBudgets();
        var bills       = _billStore.GetBills();
        var occurrences = _billStore.GetAllOccurrences();

        var curMonthTx = _repo.GetInRange(curRange);
        var raw  = _engine.Generate(all, curRange, curPrev, budgets, bills, occurrences, curNow, curLbl, prvLbl);
        Insights = raw.Select(i => new InsightRow(i)).ToList();

        return Task.CompletedTask;
    }

    private static List<MonthOption> BuildMonthOptions()
    {
        var now  = DateTimeOffset.Now;
        var list = new List<MonthOption>();
        for (var i = 0; i < 6; i++)
        {
            var d = now.AddMonths(-i);
            list.Add(new MonthOption(d.Year, d.Month, d.ToString("MMM yy")));
        }
        return list;
    }
}

public sealed record MonthOption(int Year, int Month, string Label);

public sealed class CategoryRow
{
    public string  Category { get; init; } = "";
    public decimal Total    { get; init; }
    public double  Share    { get; init; }
}

public sealed class CategoryCompRow
{
    public string Category   { get; init; } = "";
    public decimal Total     { get; init; }
    public decimal Delta     { get; init; }
    public string DeltaText  { get; init; } = "";
    public Color DeltaColor  { get; init; } = Colors.Transparent;
}

public sealed class DailyBar
{
    public int     Day          { get; init; }
    public decimal Spent        { get; init; }
    public double  BarHeight    { get; init; }
    public double  SpacerHeight { get; init; }
    public bool    IsToday      { get; init; }
    public string  DayLabel     { get; init; } = "";
    public Color   BarColor     { get; init; } = Colors.Transparent;
}

public sealed class InsightRow(Insight insight)
{
    public string Title  { get; } = insight.Title;
    public string Body   { get; } = insight.Body;
    public Color  Accent { get; } = insight.Severity switch
    {
        InsightSeverity.Alert   => Color.FromArgb("#EF4444"),
        InsightSeverity.Warning => Color.FromArgb("#F59E0B"),
        _                       => Color.FromArgb("#22C55E"),
    };
    public string Icon { get; } = insight.Severity switch
    {
        InsightSeverity.Alert   => "🔴",
        InsightSeverity.Warning => "🟡",
        _                       => "🟢",
    };
}
