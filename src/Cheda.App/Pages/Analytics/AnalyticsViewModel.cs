using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Analytics;
using Cheda.Core.Models;
using Cheda.Core.Storage;
using Cheda.App.Pages.Dashboard;

namespace Cheda.App.Pages.Analytics;

public partial class AnalyticsViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly IAnalyticsEngine       _analytics;

    public string[] Periods { get; } =
        ["Last 7 Days", "This Month", "Last 30 Days", "Last 3 Months", "Last Year", "All Time"];
    public string[] Tabs { get; } = ["Overview", "Trends", "Categories", "Payees"];

    [ObservableProperty] private int _selectedPeriodIndex = 1;
    [ObservableProperty] private int _selectedTabIndex    = 0;

    // Savings rate
    [ObservableProperty] private string _savingsRateText  = "—";
    [ObservableProperty] private string _savingsRateDesc  = "No data";
    [ObservableProperty] private string _savingsNetAmount = "";
    [ObservableProperty] private string _savingsNetLabel  = "";
    [ObservableProperty] private double _savingsProgress  = 0.0;
    [ObservableProperty] private Color  _savingsRateColor = Color.FromArgb("#F59E0B");

    // Income card
    [ObservableProperty] private string _incomeTotalLabel  = "Ksh 0";
    [ObservableProperty] private string _incomeCountLabel  = "0 transactions";
    [ObservableProperty] private string _incomeChangeLabel = "";

    // Expenses card
    [ObservableProperty] private string _expenseTotalLabel  = "Ksh 0";
    [ObservableProperty] private string _expenseCountLabel  = "0 transactions";
    [ObservableProperty] private string _expenseChangeLabel = "";

    // Quick insights
    [ObservableProperty] private string _dailySpendLabel  = "—";
    [ObservableProperty] private string _topCategoryLabel = "—";
    [ObservableProperty] private string _feesPaidLabel    = "—";
    [ObservableProperty] private string _avgTxLabel       = "—";

    // Savings accounts
    [ObservableProperty] private string _mShwariBalance = "—";
    [ObservableProperty] private string _kcbBalance     = "—";
    [ObservableProperty] private string _zidiiBalance   = "—";
    [ObservableProperty] private string _equityBalance  = "—";
    [ObservableProperty] private bool   _hasSavingsData;

    // Chart / category / payees
    [ObservableProperty] private IReadOnlyList<MonthlyBar>       _monthlyBars   = [];
    [ObservableProperty] private IReadOnlyList<CategoryChartRow> _categoryChart = [];
    [ObservableProperty] private IReadOnlyList<TopCounterparty>  _top           = [];

    public AnalyticsViewModel(ITransactionRepository repo, IAnalyticsEngine analytics)
    {
        _repo      = repo;
        _analytics = analytics;
    }

    partial void OnSelectedPeriodIndexChanged(int value) => _ = RunAsync(LoadAsync);

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    [RelayCommand]
    private void SelectPeriod(string param)
    {
        if (int.TryParse(param, out var index))
            SelectedPeriodIndex = index;
    }

    [RelayCommand]
    private void SelectTab(string param)
    {
        if (int.TryParse(param, out var index))
            SelectedTabIndex = index;
    }

    private Task LoadAsync()
    {
        var now             = DateTimeOffset.Now;
        var (range, prev)   = PeriodRanges(now);
        var all             = _repo.GetAll();
        var period          = _repo.GetInRange(range);
        var prevTx          = _repo.GetInRange(prev);

        var summary     = _analytics.GetSummary(period, range);
        var prevSummary = _analytics.GetSummary(prevTx, prev);

        // ── Savings rate ──────────────────────────────────────────────────
        if (summary.TotalIncome > 0)
        {
            var net  = summary.TotalIncome - summary.TotalExpenses;
            var rate = (int)(net / summary.TotalIncome * 100);
            SavingsRateText  = $"{(rate >= 0 ? "+" : "")}{rate}%";
            SavingsRateDesc  = rate >= 0 ? "of income saved this period" : "spending exceeds income";
            SavingsNetAmount = $"Ksh {Math.Abs(net):N0}";
            SavingsNetLabel  = net >= 0 ? "net surplus" : "net deficit";
            SavingsProgress  = Math.Clamp((double)(summary.TotalExpenses / summary.TotalIncome), 0, 1);
            SavingsRateColor = rate >= 0 ? Color.FromArgb("#F59E0B") : Color.FromArgb("#EF4444");
        }
        else
        {
            SavingsRateText  = "—";
            SavingsRateDesc  = "No income data for this period";
            SavingsNetAmount = "";
            SavingsNetLabel  = "";
            SavingsProgress  = 0;
            SavingsRateColor = Color.FromArgb("#7A5250");
        }

        // ── Income card ───────────────────────────────────────────────────
        IncomeTotalLabel = $"Ksh {summary.TotalIncome:N0}";
        var incomeCount  = period.Count(t =>
            t.Type is TransactionType.Received or TransactionType.Deposit or TransactionType.Reversal);
        IncomeCountLabel  = $"{incomeCount} transaction{(incomeCount == 1 ? "" : "s")}";
        var incomeChg     = PercentChange(summary.TotalIncome, prevSummary.TotalIncome);
        IncomeChangeLabel = incomeChg.label;

        // ── Expense card ──────────────────────────────────────────────────
        ExpenseTotalLabel = $"Ksh {summary.TotalExpenses:N0}";
        var expenseCount  = period.Count(t =>
            t.Type is not (TransactionType.Received or TransactionType.Deposit or TransactionType.Reversal)
            && !t.IsNonExpenseTransfer);
        ExpenseCountLabel  = $"{expenseCount} transaction{(expenseCount == 1 ? "" : "s")}";
        var expChg         = PercentChange(summary.TotalExpenses, prevSummary.TotalExpenses);
        ExpenseChangeLabel = expChg.label;

        // ── Quick insights ────────────────────────────────────────────────
        var days = Math.Max(1, (int)(range.End - range.Start).TotalDays);
        DailySpendLabel = summary.TotalExpenses > 0
            ? $"Ksh {summary.TotalExpenses / days:N2}" : "—";

        var topCat = period
            .Where(t => !t.IsNonExpenseTransfer
                     && !AnalyticsEngine.IsSavingsTransfer(t)
                     && t.Type is TransactionType.Sent
                               or TransactionType.PaidTill
                               or TransactionType.PaidPaybill
                               or TransactionType.Airtime
                               or TransactionType.Withdrawn)
            .GroupBy(t => t.Category ?? "Other")
            .Select(g => (Cat: g.Key, Total: g.Sum(t => t.Amount)))
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();
        TopCategoryLabel = topCat != default ? topCat.Cat : "—";

        var fees = period
            .Where(t => t.TransactionCost.HasValue && t.TransactionCost > 0)
            .Sum(t => t.TransactionCost!.Value);
        FeesPaidLabel = fees > 0 ? $"Ksh {fees:N0}" : "—";

        var expenseTxList = period
            .Where(t => !t.IsNonExpenseTransfer && !AnalyticsEngine.IsSavingsTransfer(t)
                     && t.Type is TransactionType.Sent or TransactionType.PaidTill
                               or TransactionType.PaidPaybill or TransactionType.Airtime)
            .ToList();
        AvgTxLabel = expenseTxList.Count > 0
            ? $"Ksh {expenseTxList.Average(t => t.Amount):N0}" : "—";

        // ── Category breakdown (for both Categories tab) ──────────────────
        var breakdown = _analytics.GetCategoryBreakdown(period, range);
        var catTotal  = breakdown.Count > 0 ? breakdown.Sum(b => b.Total) : 1m;
        CategoryChart = breakdown
            .OrderByDescending(b => b.Total)
            .Take(10)
            .Select((b, i) => new CategoryChartRow
            {
                Category   = b.Category,
                Emoji      = CategoryEmoji.For(b.Category),
                Total      = b.Total,
                Pct        = catTotal > 0 ? (double)(b.Total / catTotal * 100) : 0,
                PctLabel   = catTotal > 0 ? $"{(int)(b.Total / catTotal * 100)}%" : "—",
                TotalLabel = $"Ksh {b.Total:N0}",
                AccentColor = CategoryPalette[i % CategoryPalette.Length],
                Share       = catTotal > 0 ? (double)(b.Total / catTotal) : 0,
            })
            .ToList();

        // ── Top counterparties (Payees tab) ───────────────────────────────
        Top = _analytics.GetTopCounterparties(period, range, top: 20);

        // ── Monthly trend bars (Trends tab) ───────────────────────────────
        BuildMonthlyChart(period, range);

        // ── Savings accounts ──────────────────────────────────────────────
        var mshwari = all.Where(t => t.Type == TransactionType.MShwari).MaxBy(t => t.Timestamp);
        var kcb     = all.Where(t => t.Type == TransactionType.KcbMpesa).MaxBy(t => t.Timestamp);
        var zidii   = all.Where(t => t.Type == TransactionType.Zidii).MaxBy(t => t.Timestamp);
        var equity  = all.Where(t => t.Source == TransactionSource.Equity).MaxBy(t => t.Timestamp);

        MShwariBalance = FormatBalance(mshwari);
        KcbBalance     = FormatBalance(kcb);
        ZidiiBalance   = FormatBalance(zidii);
        EquityBalance  = FormatBalance(equity);
        HasSavingsData = mshwari is not null || kcb is not null
                      || zidii  is not null || equity is not null;

        return Task.CompletedTask;
    }

    private void BuildMonthlyChart(IReadOnlyList<Transaction> period, DateRange range)
    {
        var points = _analytics.GetTrend(period, range, TrendGranularity.Monthly);
        if (points.Count == 0) { MonthlyBars = []; return; }

        var maxVal  = points.Max(p => Math.Max(p.Income, p.Expenses));
        const double MaxBarH = 120.0;

        MonthlyBars = points.Select(p =>
        {
            var ih = maxVal > 0 ? (double)(p.Income   / maxVal) * MaxBarH : 0;
            var eh = maxVal > 0 ? (double)(p.Expenses / maxVal) * MaxBarH : 0;
            ih = p.Income   > 0 ? Math.Max(ih, 4) : 0;
            eh = p.Expenses > 0 ? Math.Max(eh, 4) : 0;
            return new MonthlyBar
            {
                MonthLabel    = p.PeriodStart.ToString("MMM"),
                Income        = p.Income,
                Expenses      = p.Expenses,
                IncomeHeight  = ih,
                ExpenseHeight = eh,
                IncomeSpacer  = MaxBarH - ih,
                ExpenseSpacer = MaxBarH - eh,
            };
        }).ToList();
    }

    private static string FormatBalance(Transaction? tx)
    {
        if (tx is null) return "—";
        if (tx.BalanceAfter.HasValue) return $"Ksh {tx.BalanceAfter.Value:N2}";
        return $"Ksh {tx.Amount:N0}";
    }

    private (DateRange current, DateRange prev) PeriodRanges(DateTimeOffset now) =>
        SelectedPeriodIndex switch
        {
            0 => (new DateRange(now.AddDays(-7), now),
                  new DateRange(now.AddDays(-14), now.AddDays(-7))),
            1 => (new DateRange(new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset), now),
                  new DateRange(
                      new DateTimeOffset(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 1, 0, 0, 0, now.Offset),
                      new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset))),
            2 => (new DateRange(now.AddDays(-30), now),
                  new DateRange(now.AddDays(-60), now.AddDays(-30))),
            3 => (new DateRange(now.AddMonths(-3), now),
                  new DateRange(now.AddMonths(-6), now.AddMonths(-3))),
            4 => (new DateRange(now.AddYears(-1), now),
                  new DateRange(now.AddYears(-2), now.AddYears(-1))),
            _ => (new DateRange(new DateTimeOffset(2020, 1, 1, 0, 0, 0, now.Offset), now),
                  new DateRange(new DateTimeOffset(2019, 1, 1, 0, 0, 0, now.Offset),
                                new DateTimeOffset(2020, 1, 1, 0, 0, 0, now.Offset))),
        };

    private static (string label, bool up) PercentChange(decimal current, decimal previous)
    {
        if (previous == 0) return ("new", true);
        var pct = (int)((current - previous) / previous * 100);
        return pct >= 0
            ? ($"↑ {pct}% vs last period", true)
            : ($"↓ {-pct}% vs last period", false);
    }

    private static readonly Color[] CategoryPalette =
    [
        Color.FromArgb("#7C3AED"),
        Color.FromArgb("#10B981"),
        Color.FromArgb("#F59E0B"),
        Color.FromArgb("#3B82F6"),
        Color.FromArgb("#EC4899"),
        Color.FromArgb("#EF4444"),
        Color.FromArgb("#06B6D4"),
        Color.FromArgb("#84CC16"),
        Color.FromArgb("#F97316"),
        Color.FromArgb("#8B5CF6"),
    ];
}

public sealed class MonthlyBar
{
    public string  MonthLabel    { get; init; } = "";
    public decimal Income        { get; init; }
    public decimal Expenses      { get; init; }
    public double  IncomeHeight  { get; init; }
    public double  ExpenseHeight { get; init; }
    public double  IncomeSpacer  { get; init; }
    public double  ExpenseSpacer { get; init; }
}

public sealed class CategoryChartRow
{
    public string  Category    { get; init; } = "";
    public string  Emoji       { get; init; } = "";
    public decimal Total       { get; init; }
    public double  Pct         { get; init; }
    public string  PctLabel    { get; init; } = "";
    public string  TotalLabel  { get; init; } = "";
    public Color   AccentColor { get; init; } = Colors.Gray;
    public double  Share       { get; init; }
}
