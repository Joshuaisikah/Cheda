using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Categorization;
using Cheda.Core.Models;
using Cheda.Core.Storage;
using Cheda.App.Pages.Dashboard;

namespace Cheda.App.Pages.Transactions;

public partial class TransactionsViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly ICategorizer           _categorizer;
    private IReadOnlyList<Transaction>      _all = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TypeChipLabel), nameof(TypeChipActive))]
    private string _typeFilter = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateChipLabel), nameof(DateChipActive))]
    private string _dateFilter = "All Time";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimChipLabel), nameof(SimChipActive))]
    private string _simFilter = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnyDropdownOpen))]
    private bool _showTypeDropdown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnyDropdownOpen))]
    private bool _showDateDropdown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnyDropdownOpen))]
    private bool _showSimDropdown;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private IReadOnlyList<TxGroup> _groups = [];

    public bool   TypeChipActive => TypeFilter != "All";
    public bool   DateChipActive => DateFilter != "All Time";
    public bool   SimChipActive  => SimFilter  != "All";
    public bool   AnyDropdownOpen => ShowTypeDropdown || ShowDateDropdown || ShowSimDropdown;
    public string TypeChipLabel  => TypeFilter == "All"      ? "Type"       : TypeFilter;
    public string DateChipLabel  => DateFilter == "All Time" ? "Date Range" : DateFilter;
    public string SimChipLabel   => SimFilter  == "All"      ? "Account"    : SimFilter;

    public string[] TypeOptions { get; } = ["All", "Received", "Sent", "Till", "Paybill",
        "Withdrawal", "Deposit", "Airtime", "Fuliza", "Savings", "Uncategorized"];
    public string[] DateOptions { get; } = ["All Time", "Today", "Previous Day",
        "Last 7 Days", "This Month", "Last Month"];
    public string[] SimOptions  { get; } = ["All", "SIM 1", "SIM 2"];

    // Set by Dashboard's uncategorized banner tap before navigating here.
    public string? PendingTypeFilter { get; set; }

    public TransactionsViewModel(ITransactionRepository repo, ICategorizer categorizer)
    {
        _repo        = repo;
        _categorizer = categorizer;
    }

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    [RelayCommand]
    private void ToggleTypeDropdown()
    {
        ShowTypeDropdown = !ShowTypeDropdown;
        ShowDateDropdown = false;
        ShowSimDropdown  = false;
    }

    [RelayCommand]
    private void ToggleDateDropdown()
    {
        ShowDateDropdown = !ShowDateDropdown;
        ShowTypeDropdown = false;
        ShowSimDropdown  = false;
    }

    [RelayCommand]
    private void ToggleSimDropdown()
    {
        ShowSimDropdown  = !ShowSimDropdown;
        ShowTypeDropdown = false;
        ShowDateDropdown = false;
    }

    [RelayCommand]
    private void SelectType(string t)
    {
        TypeFilter = t;
        ShowTypeDropdown = false;
        ApplyFilter();
    }

    [RelayCommand]
    private void SelectDate(string d)
    {
        DateFilter = d;
        ShowDateDropdown = false;
        ApplyFilter();
    }

    [RelayCommand]
    private void SelectSim(string s)
    {
        SimFilter       = s;
        ShowSimDropdown = false;
        ApplyFilter();
    }

    [RelayCommand]
    private void CloseDropdowns()
    {
        ShowTypeDropdown = false;
        ShowDateDropdown = false;
        ShowSimDropdown  = false;
    }

    [RelayCommand]
    private async Task OpenEditAsync(TxRow row)
    {
        ShowTypeDropdown = false;
        ShowDateDropdown = false;
        ShowSimDropdown  = false;
        var page = new TransactionEditPage(
            new TransactionEditViewModel(row.Tx, _repo, _categorizer));
        await Shell.Current.Navigation.PushAsync(page);
    }

    partial void OnSearchTextChanged(string _) => ApplyFilter();

    private Task LoadAsync()
    {
        _all = _repo.GetAll().OrderByDescending(t => t.Timestamp).ToList();
        if (PendingTypeFilter is not null)
        {
            TypeFilter        = PendingTypeFilter;
            PendingTypeFilter = null;
        }
        ApplyFilter();
        return Task.CompletedTask;
    }

    private void ApplyFilter()
    {
        var now   = DateTimeOffset.Now;
        var items = _all.AsEnumerable();

        items = TypeFilter switch
        {
            "Received"      => items.Where(t => t.Type == TransactionType.Received),
            "Sent"          => items.Where(t => t.Type == TransactionType.Sent),
            "Till"          => items.Where(t => t.Type == TransactionType.PaidTill),
            "Paybill"       => items.Where(t => t.Type == TransactionType.PaidPaybill),
            "Withdrawal"    => items.Where(t => t.Type == TransactionType.Withdrawn),
            "Deposit"       => items.Where(t => t.Type == TransactionType.Deposit),
            "Airtime"       => items.Where(t => t.Type == TransactionType.Airtime),
            "Fuliza"        => items.Where(t => t.Type == TransactionType.Fuliza),
            "Savings"       => items.Where(t => t.Type is TransactionType.MShwari
                                                       or TransactionType.KcbMpesa
                                                       or TransactionType.Zidii),
            "Uncategorized" => items.Where(t => string.IsNullOrEmpty(t.Category)
                                                || t.Category == "Uncategorized"),
            _               => items,
        };

        items = DateFilter switch
        {
            "Today"        => items.Where(t => t.Timestamp.LocalDateTime.Date == now.LocalDateTime.Date),
            "Previous Day" => items.Where(t => t.Timestamp.LocalDateTime.Date == now.LocalDateTime.Date.AddDays(-1)),
            "Last 7 Days"  => items.Where(t => t.Timestamp >= now.AddDays(-7)),
            "This Month"   => items.Where(t => t.Timestamp.Month == now.Month && t.Timestamp.Year == now.Year),
            "Last Month"   => items.Where(t => t.Timestamp.Month == now.AddMonths(-1).Month
                                              && t.Timestamp.Year == now.AddMonths(-1).Year),
            _              => items,
        };

        if (SimFilter != "All")
        {
            // subscription_id values vary by device (0/1 or 1/2 or arbitrary).
            // Rank the distinct slot values and match by position, not raw value.
            var slots = _all
                .Where(t => t.SimSlot.HasValue)
                .Select(t => t.SimSlot!.Value)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
            var idx = SimFilter == "SIM 1" ? 0 : 1;
            if (idx < slots.Count)
            {
                var target = slots[idx];
                items = items.Where(t => t.SimSlot == target);
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim().ToLowerInvariant();
            items = items.Where(t =>
                (t.Counterparty?.ToLowerInvariant().Contains(q) ?? false) ||
                (t.Category?.ToLowerInvariant().Contains(q) ?? false));
        }

        Groups = items
            .GroupBy(t => t.Timestamp.LocalDateTime.Date)
            .Select(g => new TxGroup(
                FormatDateHeader(g.Key),
                g.Select(t => new TxRow(t)).ToList()))
            .ToList();
    }

    private static string FormatDateHeader(DateTime date)
    {
        var today = DateTime.Today;
        if (date == today)             return $"Today, {date:MMM dd}";
        if (date == today.AddDays(-1)) return $"Yesterday, {date:MMM dd}";
        return date.ToString("dddd, MMM dd");
    }
}

public sealed class TxGroup(string header, List<TxRow> rows) : List<TxRow>(rows)
{
    public string Header { get; } = header;
}

public sealed class TxRow(Transaction tx)
{
    public Transaction Tx             { get; } = tx;
    public string TypeLabel           { get; } = TransactionEditViewModel.TypeLabelFor(tx.Type);
    public string TypeIcon            { get; } = TransactionEditViewModel.TypeIconFor(tx.Type);
    public Color  AccentColor         { get; } = TransactionEditViewModel.AccentColorFor(tx.Type);
    public Color  AmountColor         { get; } = TransactionEditViewModel.AccentColorFor(tx.Type);
    public string Counterparty        { get; } = CleanName(tx.Counterparty);
    public string AmountLabel         { get; } = $"Ksh {tx.Amount:N0}";
    public string DateLabel           { get; } = tx.Timestamp.LocalDateTime.ToString("MMM dd, HH:mm");
    public string CategoryIcon        { get; } = CategoryEmoji.For(tx.Category);
    public string CategoryDisplay     { get; } = $"{CategoryEmoji.For(tx.Category)} {tx.Category ?? "Other"}";
    public string SimDisplay          { get; } = tx.SimSlot.HasValue ? $"📱 SIM {tx.SimSlot}" : "";
    public bool   HasSim              { get; } = tx.SimSlot.HasValue;

    private static string CleanName(string? raw)
    {
        if (raw is null) return "—";
        // Strip trailing phone number e.g. "JOSHUA MARTIN 0743369299" → "Joshua Martin"
        var name = Regex.Replace(raw.Trim(), @"\s+\d{9,12}$", "").Trim();
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
    }
}
