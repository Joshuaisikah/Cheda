using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Analytics;
using Cheda.Core.Bills;
using Cheda.Core.Budgets;
using Cheda.Core.Storage;

namespace Cheda.App.Pages.Plan;

public partial class PlanViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly IBudgetStore           _budgetStore;
    private readonly IBillStore             _billStore;
    private readonly IBudgetEngine          _budgetEngine;
    private readonly IBillEngine            _billEngine;

    [ObservableProperty] private IReadOnlyList<BudgetRow>   _budgets   = [];
    [ObservableProperty] private IReadOnlyList<UpcomingBill> _upcoming = [];
    [ObservableProperty] private IReadOnlyList<UpcomingBill> _overdue  = [];
    [ObservableProperty] private decimal                     _upcomingTotal;
    [ObservableProperty] private string                      _newCategory   = "";
    [ObservableProperty] private string                      _newLimit      = "";
    [ObservableProperty] private string                      _budgetError   = "";
    [ObservableProperty] private bool                        _hasBudgetError;
    [ObservableProperty] private bool                        _hasOverdue;

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

        var budgets  = _budgetStore.GetBudgets();
        var statuses = _budgetEngine.GetStatuses(budgets, month, range);
        Budgets = statuses.Select(s => new BudgetRow(s)).ToList();

        var bills       = _billStore.GetBills().Where(b => b.IsEnabled).ToList();
        var occurrences = _billStore.GetAllOccurrences();
        Upcoming      = _billEngine.GetUpcoming(bills, occurrences, now, days: 30);
        Overdue       = _billEngine.GetOverdue(bills, occurrences, now);
        UpcomingTotal = _billEngine.GetUpcomingTotal(bills, occurrences, now, days: 7);
        HasOverdue    = Overdue.Count > 0;

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AddBudgetAsync()
    {
        BudgetError = "";
        if (string.IsNullOrWhiteSpace(NewCategory))
        {
            BudgetError    = "Category is required.";
            HasBudgetError = true;
            return;
        }
        if (!decimal.TryParse(NewLimit, out var limit) || limit <= 0)
        {
            BudgetError    = "Enter a valid monthly limit.";
            HasBudgetError = true;
            return;
        }

        await RunAsync(() =>
        {
            _budgetStore.Save(new Budget { Category = NewCategory.Trim(), MonthlyLimit = limit });
            NewCategory = "";
            NewLimit    = "";
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
    public BudgetStatus Status       { get; } = status;
    public string       Category     { get; } = status.Budget.Category;
    public string       LimitLabel   { get; } = $"Ksh {status.Budget.MonthlyLimit:N0}/mo";
    public string       SpentLabel   { get; } = $"Ksh {status.AmountSpent:N0} spent";
    public string       RemainingLabel { get; } = status.AmountRemaining >= 0
        ? $"Ksh {status.AmountRemaining:N0} left"
        : $"Ksh {-status.AmountRemaining:N0} over";
    public double       Progress     { get; } = Math.Min(status.ProgressPercent / 100.0, 1.0);
    public Color        ProgressColor { get; } = status.AlertLevel switch
    {
        AlertLevel.Overspent => Color.FromArgb("#EF4444"),
        AlertLevel.Red       => Color.FromArgb("#EF4444"),
        AlertLevel.Amber     => Color.FromArgb("#F59E0B"),
        _                    => Color.FromArgb("#00875A"),
    };
}
