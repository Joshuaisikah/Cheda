using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Analytics;
using Cheda.Core.Bills;
using Cheda.Core.Budgets;
using Cheda.Core.Models;
using Cheda.Core.Storage;
using Cheda.App.Pages.Dashboard;

namespace Cheda.App.Pages.Plan;

public partial class PlanViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly IBudgetStore           _budgetStore;
    private readonly IBillStore             _billStore;
    private readonly IBudgetEngine          _budgetEngine;
    private readonly IBillEngine            _billEngine;

    // Hero card
    [ObservableProperty] private string _monthLabel          = "";
    [ObservableProperty] private string _totalSpentLabel     = "Ksh 0";
    [ObservableProperty] private string _totalBudgetedLabel  = "Ksh 0";
    [ObservableProperty] private bool   _hasBudget;

    // Daily insights
    [ObservableProperty] private string _avgDailySpendLabel = "—";
    [ObservableProperty] private string _peakDayLabel       = "—";

    // Budgeted categories (have a budget set)
    [ObservableProperty] private IReadOnlyList<BudgetRow> _budgetedCategories = [];
    [ObservableProperty] private bool _hasBudgetedCategories;

    // Unbudgeted categories (spending exists but no budget set)
    [ObservableProperty] private IReadOnlyList<UnbudgetedRow> _unbudgetedCategories = [];
    [ObservableProperty] private int  _unbudgetedCount;
    [ObservableProperty] private bool _hasUnbudgetedCategories;
    [ObservableProperty] private bool _isEmpty;

    // Bills (still tracked)
    [ObservableProperty] private IReadOnlyList<UpcomingBill> _upcoming = [];
    [ObservableProperty] private IReadOnlyList<UpcomingBill> _overdue  = [];
    [ObservableProperty] private decimal                     _upcomingTotal;
    [ObservableProperty] private bool                        _hasOverdue;
    [ObservableProperty] private bool                        _hasBills;

    public PlanViewModel(
        ITransactionRepository repo,
        IBudgetStore budgetStore,
        IBillStore billStore,
        IBudgetEngine budgetEngine,
        IBillEngine billEngine)
    {
        _repo         = repo;
        _budgetStore  = budgetStore;
        _billStore    = billStore;
        _budgetEngine = budgetEngine;
        _billEngine   = billEngine;
    }

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    private Task LoadAsync()
    {
        var now   = DateTimeOffset.Now;
        var range = new DateRange(new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset), now);
        var month = _repo.GetInRange(range);

        MonthLabel = now.ToString("MMMM yyyy");

        // Total spent this month — only real expenditure types, never savings transfers
        var totalSpent = month
            .Where(t => !t.IsNonExpenseTransfer
                     && !AnalyticsEngine.IsSavingsTransfer(t)
                     && t.Type is TransactionType.Sent
                               or TransactionType.PaidTill
                               or TransactionType.PaidPaybill
                               or TransactionType.Airtime
                               or TransactionType.Withdrawn)
            .Sum(t => t.Amount);

        // Budgeted categories
        var budgets  = _budgetStore.GetBudgets();
        var statuses = _budgetEngine.GetStatuses(budgets, month, range);
        var budgetedRows = statuses.Select(s => new BudgetRow(s)).ToList();

        var totalBudgeted = budgets.Sum(b => b.MonthlyLimit);
        TotalSpentLabel    = $"Ksh {totalSpent:N0}";
        TotalBudgetedLabel = totalBudgeted > 0 ? $"Ksh {totalBudgeted:N0}" : "Ksh 0";
        HasBudget          = totalBudgeted > 0;

        BudgetedCategories    = budgetedRows;
        HasBudgetedCategories = budgetedRows.Count > 0;

        // Daily spend insights
        var daysPassed = Math.Max(1, now.Day);
        AvgDailySpendLabel = totalSpent > 0
            ? $"Ksh {totalSpent / daysPassed:N0}/day" : "—";

        var peakDay = month
            .Where(t => !t.IsNonExpenseTransfer
                     && !AnalyticsEngine.IsSavingsTransfer(t)
                     && t.Type is TransactionType.Sent
                               or TransactionType.PaidTill
                               or TransactionType.PaidPaybill
                               or TransactionType.Airtime
                               or TransactionType.Withdrawn)
            .GroupBy(t => t.Timestamp.Date)
            .Select(g => (Date: g.Key, Total: g.Sum(t => t.Amount)))
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();
        PeakDayLabel = peakDay != default
            ? $"{peakDay.Date:MMM dd} — Ksh {peakDay.Total:N0}" : "—";

        // Unbudgeted spending categories
        var budgetedNames = new HashSet<string>(budgets.Select(b => b.Category), StringComparer.OrdinalIgnoreCase);
        var unbudgeted = month
            .Where(t => !t.IsNonExpenseTransfer
                     && !AnalyticsEngine.IsSavingsTransfer(t)
                     && t.Type is TransactionType.Sent
                               or TransactionType.PaidTill
                               or TransactionType.PaidPaybill
                               or TransactionType.Airtime
                               or TransactionType.Withdrawn
                     && !budgetedNames.Contains(t.Category ?? "Other"))
            .GroupBy(t => t.Category ?? "Other")
            .Select(g => new UnbudgetedRow
            {
                Category   = g.Key,
                Emoji      = CategoryEmoji.For(g.Key),
                Spent      = g.Sum(t => t.Amount),
                SpentLabel = g.Sum(t => t.Amount) > 0
                    ? $"Ksh {g.Sum(t => t.Amount):N0} spent this month"
                    : "",
            })
            .OrderByDescending(r => r.Spent)
            .ToList();

        UnbudgetedCategories    = unbudgeted;
        UnbudgetedCount         = unbudgeted.Count;
        HasUnbudgetedCategories = unbudgeted.Count > 0;
        IsEmpty                 = budgetedRows.Count == 0 && unbudgeted.Count == 0;

        // Bills
        var bills       = _billStore.GetBills().Where(b => b.IsEnabled).ToList();
        var occurrences = _billStore.GetAllOccurrences();
        Upcoming      = _billEngine.GetUpcoming(bills, occurrences, now, days: 30);
        Overdue       = _billEngine.GetOverdue(bills, occurrences, now);
        UpcomingTotal = _billEngine.GetUpcomingTotal(bills, occurrences, now, days: 7);
        HasOverdue    = Overdue.Count > 0;
        HasBills      = Upcoming.Count > 0 || Overdue.Count > 0;

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SetBudgetAsync(UnbudgetedRow row)
    {
        var nav = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (nav is null) return;

        var dialog = new BudgetInputPage(
            emoji:    row.Emoji,
            title:    row.Category,
            subtitle: "Set a monthly limit for this category.");
        await nav.PushModalAsync(dialog, animated: true);
        // PushModalAsync returns when push animation finishes, NOT when dismissed.
        // WaitForResultAsync completes only after the user saves or cancels.
        var limit = await dialog.WaitForResultAsync();
        if (limit is null) return;

        await RunAsync(() =>
        {
            _budgetStore.Save(new Budget { Category = row.Category, MonthlyLimit = limit.Value });
            return LoadAsync();
        });
    }

    [RelayCommand]
    private async Task EditBudgetAsync(BudgetRow row)
    {
        var nav = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (nav is null) return;

        var dialog = new BudgetInputPage(
            emoji:    CategoryEmoji.For(row.Category),
            title:    row.Category,
            subtitle: $"Current: Ksh {row.Status.Budget.MonthlyLimit:N0}/mo",
            initial:  row.Status.Budget.MonthlyLimit);
        await nav.PushModalAsync(dialog, animated: true);
        var limit = await dialog.WaitForResultAsync();
        if (limit is null) return;

        await RunAsync(() =>
        {
            var budget = row.Status.Budget;
            budget.MonthlyLimit = limit.Value;
            _budgetStore.Save(budget);
            return LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteBudgetAsync(BudgetRow row)
    {
        await RunAsync(() =>
        {
            _budgetStore.Delete(row.Status.Budget.Id);
            return LoadAsync();
        });
    }
}

public sealed class BudgetRow(BudgetStatus status)
{
    public BudgetStatus Status         { get; } = status;
    public string       Category       { get; } = status.Budget.Category;
    public string       Emoji          { get; } = CategoryEmoji.For(status.Budget.Category);
    public string       LimitLabel     { get; } = $"Ksh {status.Budget.MonthlyLimit:N0}/mo";
    public string       SpentLabel     { get; } = $"Ksh {status.AmountSpent:N0} spent";
    public string       RemainingLabel { get; } = status.AmountRemaining >= 0
        ? $"Ksh {status.AmountRemaining:N0} left"
        : $"Ksh {-status.AmountRemaining:N0} over budget";
    public double       Progress       { get; } = Math.Min(status.ProgressPercent / 100.0, 1.0);
    public Color        ProgressColor  { get; } = status.AlertLevel switch
    {
        AlertLevel.Overspent => Color.FromArgb("#EF4444"),
        AlertLevel.Red       => Color.FromArgb("#EF4444"),
        AlertLevel.Amber     => Color.FromArgb("#F59E0B"),
        _                    => Color.FromArgb("#10B981"),
    };
    public bool IsOverBudget { get; } = status.AlertLevel == AlertLevel.Overspent;
}

public sealed class UnbudgetedRow
{
    public string  Category   { get; init; } = "";
    public string  Emoji      { get; init; } = "📦";
    public decimal Spent      { get; init; }
    public string  SpentLabel { get; init; } = "";
}
