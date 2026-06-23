using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Models;
using Cheda.Core.Storage;

namespace Cheda.App.Pages.Transactions;

public partial class TransactionsViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private IReadOnlyList<Transaction>      _all = [];

    [ObservableProperty] private IReadOnlyList<Transaction> _transactions = [];
    [ObservableProperty] private string _searchText  = "";
    [ObservableProperty] private string _activeFilter = "All";

    public string[] Filters { get; } = ["All", "This Month", "Last Month", "Expenses", "Income"];

    public TransactionsViewModel(ITransactionRepository repo) => _repo = repo;

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    [RelayCommand]
    private void SetFilter(string filter)
    {
        ActiveFilter = filter;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task OpenEditAsync(Transaction tx)
    {
        var page = new TransactionEditPage(new TransactionEditViewModel(tx, _repo));
        await Shell.Current.Navigation.PushModalAsync(page);
        await RefreshAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private Task LoadAsync()
    {
        _all = _repo.GetAll().OrderByDescending(t => t.Timestamp).ToList();
        ApplyFilter();
        return Task.CompletedTask;
    }

    private void ApplyFilter()
    {
        var now   = DateTimeOffset.Now;
        var items = _all.AsEnumerable();

        items = ActiveFilter switch
        {
            "This Month"  => items.Where(t => t.Timestamp.Month == now.Month && t.Timestamp.Year == now.Year),
            "Last Month"  => items.Where(t => t.Timestamp.Month == now.AddMonths(-1).Month),
            "Expenses"    => items.Where(t => t.Type is TransactionType.PaidTill or TransactionType.PaidPaybill
                                               or TransactionType.Sent or TransactionType.Airtime),
            "Income"      => items.Where(t => t.Type == TransactionType.Received),
            _             => items,
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim().ToLowerInvariant();
            items = items.Where(t =>
                (t.Counterparty?.ToLowerInvariant().Contains(q) ?? false) ||
                (t.Category?.ToLowerInvariant().Contains(q) ?? false));
        }

        Transactions = items.ToList();
    }
}
