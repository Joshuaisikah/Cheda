using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Categorization;
using Cheda.Core.Models;
using Cheda.Core.Storage;

namespace Cheda.App.Pages.Transactions;

public partial class TransactionEditViewModel : ViewModelBase
{
    private readonly Transaction            _tx;
    private readonly ITransactionRepository _repo;

    [ObservableProperty] private string? _selectedCategory;
    [ObservableProperty] private string  _counterparty = "";
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string  _timestamp    = "";

    public string[]     Categories  { get; } = DefaultCategories.All.Select(c => c.Name).ToArray();
    public Transaction  Transaction => _tx;

    public TransactionEditViewModel(Transaction tx, ITransactionRepository repo)
    {
        _tx              = tx;
        _repo            = repo;
        SelectedCategory = tx.Category;
        Counterparty     = tx.Counterparty ?? "—";
        Amount           = tx.Amount;
        Timestamp        = tx.Timestamp.LocalDateTime.ToString("dd MMM yyyy  HH:mm");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _tx.Category = SelectedCategory;
        _repo.Update(_tx);
        await Shell.Current.Navigation.PopModalAsync();
    }

    [RelayCommand]
    private async Task CancelAsync() =>
        await Shell.Current.Navigation.PopModalAsync();
}
