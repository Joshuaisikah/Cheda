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
    [ObservableProperty] private string? _note;
    [ObservableProperty] private bool    _excludeFromTotals;

    // Static display fields (don't change after init)
    public string  Counterparty      => _tx.Counterparty ?? "—";
    public decimal Amount            => _tx.Amount;
    public string  TimestampDisplay  => _tx.Timestamp.LocalDateTime.ToString("MMM dd, yyyy 'at' HH:mm");
    public string  Code              => _tx.TransactionCode;
    public string  RawMessage        => _tx.RawMessage;
    public string  TypeLabel         => TypeLabelFor(_tx.Type);
    public string  TypeIcon          => TypeIconFor(_tx.Type);
    public Color   AccentColor       => AccentColorFor(_tx.Type);
    public string? BalanceAfterLabel => _tx.BalanceAfter.HasValue ? $"Ksh{_tx.BalanceAfter:N2}" : null;
    public string  SimLabel          => _tx.SimSlot.HasValue ? $"SIM {_tx.SimSlot}" : "—";
    public string  AmountPrefix      => IsInflow(_tx.Type) ? "+" : "-";
    public Color   AmountColor       => AccentColorFor(_tx.Type);
    public Transaction Transaction   => _tx;

    // Page title e.g. "Sent to Joshua Wambua" or "Money Received from Joshua Martin"
    public string PageTitle => _tx.Counterparty is null
        ? TypeLabel
        : IsInflow(_tx.Type)
            ? $"{TypeLabel} from {_tx.Counterparty}"
            : $"Sent to {_tx.Counterparty}";

    // Direction label for details row
    public string ToFromLabel => IsInflow(_tx.Type) ? "From" : "To";

    public TransactionEditViewModel(Transaction tx, ITransactionRepository repo, ICategorizer categorizer)
    {
        _tx              = tx;
        _repo            = repo;
        _categorizer     = categorizer;
        SelectedCategory = tx.Category;
        _note            = tx.Note;
        _excludeFromTotals = tx.IsNonExpenseTransfer;
    }

    partial void OnNoteChanged(string? value)
    {
        _tx.Note = value;
        _repo.Update(_tx);
    }

    partial void OnExcludeFromTotalsChanged(bool value)
    {
        _tx.IsNonExpenseTransfer = value;
        _repo.Update(_tx);
    }

    [RelayCommand]
    private void ToggleSms() => IsOriginalSmsExpanded = !IsOriginalSmsExpanded;

    [RelayCommand]
    private async Task CopyCodeAsync()
    {
        await Clipboard.SetTextAsync(_tx.TransactionCode);
    }

    // Auto-save: called when the category picker returns a selection.
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
        TransactionType.Received    => "⬇️",
        TransactionType.Deposit     => "⬇️",
        TransactionType.Reversal    => "↩️",
        TransactionType.PaidTill    => "🏪",
        TransactionType.PaidPaybill => "🧾",
        TransactionType.Withdrawn   => "🏧",
        TransactionType.Airtime     => "📱",
        TransactionType.Fuliza      => "⚡",
        TransactionType.MShwari or
        TransactionType.KcbMpesa or
        TransactionType.Zidii       => "🏦",
        _                           => "⬆️",
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
        _                           => Color.FromArgb("#F4A28A"),
    };

    private static bool IsInflow(TransactionType t) =>
        t is TransactionType.Received or TransactionType.Deposit or TransactionType.Reversal;
}
