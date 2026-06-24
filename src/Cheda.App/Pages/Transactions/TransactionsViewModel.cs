using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Categorization;
using Cheda.Core.Models;
using Cheda.Core.Storage;

namespace Cheda.App.Pages.Transactions;

public partial class TransactionsViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly ICategorizer           _categorizer;
    private IReadOnlyList<Transaction>      _all = [];

    [ObservableProperty] private IReadOnlyList<TxRow> _transactions = [];
    [ObservableProperty] private string _searchText   = "";
    [ObservableProperty] private string _activeFilter = "All";

    public string[] Filters { get; } = ["All", "This Month", "Last Month", "Expenses", "Income"];

    public TransactionsViewModel(ITransactionRepository repo, ICategorizer categorizer)
    {
        _repo        = repo;
        _categorizer = categorizer;
    }

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    [RelayCommand]
    private void SetFilter(string filter)
    {
        ActiveFilter = filter;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task OpenEditAsync(TxRow row)
    {
        var page = new TransactionEditPage(
            new TransactionEditViewModel(row.Tx, _repo, _categorizer));
        await Shell.Current.Navigation.PushAsync(page);
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

        Transactions = items.Select(t => new TxRow(t)).ToList();
    }
}

public sealed class TxRow(Transaction tx)
{
    public Transaction Tx               { get; } = tx;
    public string TypeLabel             { get; } = TransactionEditViewModel.TypeLabelFor(tx.Type);
    public string TypeIcon              { get; } = TransactionEditViewModel.TypeIconFor(tx.Type);
    public Color  AccentColor           { get; } = TransactionEditViewModel.AccentColorFor(tx.Type);
    public Color  AmountColor           { get; } = TransactionEditViewModel.AccentColorFor(tx.Type);
    public string Counterparty          { get; } = tx.Counterparty ?? "—";
    public string AmountLabel           { get; } = $"Ksh {tx.Amount:N0}";
    public string DateLabel             { get; } = tx.Timestamp.LocalDateTime.ToString("dd MMM, HH:mm");
    public string CategoryDisplay       { get; } = tx.Category ?? "Uncategorized";
}
