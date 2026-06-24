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
    private readonly ICategorizer           _categorizer;

    [ObservableProperty] private string? _selectedCategory;
    [ObservableProperty] private bool    _isOriginalSmsExpanded = false;

    // Static display fields (don't change after init)
    public string  Counterparty      => _tx.Counterparty ?? "—";
    public decimal Amount            => _tx.Amount;
    public string  TimestampDisplay  => _tx.Timestamp.LocalDateTime.ToString("dd MMM yyyy  ·  HH:mm");
    public string  Code              => _tx.TransactionCode;
    public string  RawMessage        => _tx.RawMessage;
    public string  TypeLabel         => TypeLabelFor(_tx.Type);
    public string  TypeIcon          => TypeIconFor(_tx.Type);
    public Color   AccentColor       => AccentColorFor(_tx.Type);
    public string  BalanceAfterLabel => _tx.BalanceAfter.HasValue ? $"Ksh {_tx.BalanceAfter:N2}" : "—";
    public string  SimLabel          => _tx.SimSlot.HasValue ? $"SIM {_tx.SimSlot + 1}" : "—";
    public string  AmountPrefix      => IsInflow(_tx.Type) ? "+" : "-";
    public Color   AmountColor       => AccentColorFor(_tx.Type);
    public Transaction Transaction   => _tx;

    public TransactionEditViewModel(Transaction tx, ITransactionRepository repo, ICategorizer categorizer)
    {
        _tx              = tx;
        _repo            = repo;
        _categorizer     = categorizer;
        SelectedCategory = tx.Category;
    }

    [RelayCommand]
    private void ToggleSms() => IsOriginalSmsExpanded = !IsOriginalSmsExpanded;

    // Auto-save: called when the category picker returns a selection.
    // Does NOT navigate away — stays on the detail page.
    [RelayCommand]
    public Task SaveCategoryAsync() => Task.Run(() =>
    {
        var category = SelectedCategory ?? DefaultCategories.Uncategorized;
        _tx.Category = category;
        _repo.Update(_tx);
        _categorizer.LearnFromCorrection(_tx, category);

        if (_tx.Counterparty is null) return;
        var key = RuleBasedCategorizer.MappingKey(_tx);
        foreach (var t in _repo.GetAll()
            .Where(t => t.Id != _tx.Id &&
                         t.Counterparty is not null &&
                         RuleBasedCategorizer.MappingKey(t) == key)
            .ToList())
        {
            t.Category = category;
            _repo.Update(t);
        }
    });

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.Navigation.PopAsync();

    public static string TypeLabelFor(TransactionType t) => t switch
    {
        TransactionType.Received    => "Money Received",
        TransactionType.Sent        => "Money Sent",
        TransactionType.PaidTill    => "Till Payment",
        TransactionType.PaidPaybill => "Paybill Payment",
        TransactionType.Withdrawn   => "Withdrawal",
        TransactionType.Deposit     => "Deposit",
        TransactionType.Airtime     => "Airtime Purchase",
        TransactionType.Fuliza      => "Fuliza",
        TransactionType.MShwari     => "M-Shwari Savings",
        TransactionType.KcbMpesa    => "KCB M-Pesa Savings",
        TransactionType.Zidii       => "Zidii Savings",
        TransactionType.Reversal    => "Reversal",
        _                           => "Transaction",
    };

    public static string TypeIconFor(TransactionType t) => t switch
    {
        TransactionType.Received    => "↓",
        TransactionType.Deposit     => "↓",
        TransactionType.Reversal    => "↩",
        TransactionType.PaidTill    => "🏪",
        TransactionType.Airtime     => "📱",
        TransactionType.Fuliza      => "⚡",
        TransactionType.MShwari or
        TransactionType.KcbMpesa or
        TransactionType.Zidii       => "🏦",
        _                           => "↑",
    };

    public static Color AccentColorFor(TransactionType t) => t switch
    {
        TransactionType.Received or
        TransactionType.Deposit or
        TransactionType.Reversal    => Color.FromArgb("#10B981"),
        TransactionType.MShwari or
        TransactionType.KcbMpesa or
        TransactionType.Zidii       => Color.FromArgb("#00C4B4"),
        TransactionType.Fuliza      => Color.FromArgb("#F59E0B"),
        _                           => Color.FromArgb("#EF4444"),
    };

    private static bool IsInflow(TransactionType t) =>
        t is TransactionType.Received or TransactionType.Deposit or TransactionType.Reversal;
}
