using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Analytics;
using Cheda.Core.Categorization;
using Cheda.Core.Models;
using Cheda.Core.Storage;
using Microsoft.Maui.Controls;

namespace Cheda.App.Pages.Dashboard;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly IAnalyticsEngine       _analytics;
    private readonly ICategorizer           _categorizer;

    [ObservableProperty] private decimal  _balance;
    [ObservableProperty] private decimal  _spentThisMonth;
    [ObservableProperty] private decimal  _receivedThisMonth;
    [ObservableProperty] private string   _monthLabel    = "";
    [ObservableProperty] private string   _netFlowLabel  = "";
    [ObservableProperty] private Color    _netFlowColor  = Colors.White;
    [ObservableProperty] private IReadOnlyList<RecentRow> _recent = [];
    [ObservableProperty] private int      _reviewCount;
    [ObservableProperty] private bool     _hasReviewItems;

    public DashboardViewModel(ITransactionRepository repo, IAnalyticsEngine analytics, ICategorizer categorizer)
    {
        _repo        = repo;
        _analytics   = analytics;
        _categorizer = categorizer;
    }

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    [RelayCommand]
    private async Task ViewAllAsync()
    {
        // Switch to the Transactions tab (index 1)
        if (Shell.Current is Shell shell)
            await shell.GoToAsync("//TransactionsTab");
    }

    [RelayCommand]
    private async Task OpenDetailAsync(RecentRow row)
    {
        var page = new Pages.Transactions.TransactionEditPage(
            new Pages.Transactions.TransactionEditViewModel(row.Tx, _repo, _categorizer));
        await Shell.Current.Navigation.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenReviewAsync()
    {
        var reviewPage = IPlatformApplication.Current!.Services
            .GetRequiredService<Pages.Review.ReviewQueuePage>();
        await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(reviewPage, animated: true);
    }

    private Task LoadAsync()
    {
        var now    = DateTimeOffset.Now;
        var all    = _repo.GetAll();
        var range  = new DateRange(new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset), now);
        var month  = _repo.GetInRange(range);

        var summary        = _analytics.GetSummary(month, range);
        Balance            = _analytics.GetCurrentBalance(all) ?? 0m;
        SpentThisMonth     = summary.TotalExpenses;
        ReceivedThisMonth  = summary.TotalIncome;
        MonthLabel         = now.ToString("MMMM yyyy");

        var net = summary.TotalIncome - summary.TotalExpenses;
        NetFlowLabel  = net >= 0
            ? $"▲  Ksh {net:N0} net"
            : $"▼  Ksh {-net:N0} net";
        NetFlowColor  = net >= 0
            ? Color.FromArgb("#00875A")
            : Color.FromArgb("#EF4444");

        Recent = all
            .OrderByDescending(t => t.Timestamp)
            .Take(7)
            .Select(t => new RecentRow(t))
            .ToList();

        ReviewCount    = all.Count(t =>
            t.Category == Core.Categorization.DefaultCategories.Uncategorized ||
            t.CategoryConfidence < 0.6);
        HasReviewItems = ReviewCount > 0;

        return Task.CompletedTask;
    }
}

public sealed class RecentRow(Transaction tx)
{
    public Transaction Tx           { get; } = tx;
    public string  Counterparty     { get; } = tx.Counterparty ?? "—";
    public string  Category         { get; } = tx.Category ?? "Uncategorized";
    public string  TypeLabel        { get; } = Pages.Transactions.TransactionEditViewModel.TypeLabelFor(tx.Type);
    public string  TypeIcon         { get; } = Pages.Transactions.TransactionEditViewModel.TypeIconFor(tx.Type);
    public Color   AccentColor      { get; } = Pages.Transactions.TransactionEditViewModel.AccentColorFor(tx.Type);
    public Color   AmountColor      { get; } = Pages.Transactions.TransactionEditViewModel.AccentColorFor(tx.Type);
    public string  AmountLabel      { get; } = $"Ksh {tx.Amount:N0}";
    public string  TimeLabel        { get; } = tx.Timestamp.ToString("dd MMM");
}

public static class CategoryEmoji
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Groceries"]                    = "🛒",
        ["Eating Out/Delivery"]          = "🍽️",
        ["Mama Mboga/Market"]            = "🥬",
        ["Matatu/Bus Fare"]              = "🚌",
        ["Boda/Taxi"]                    = "🛵",
        ["Fuel"]                         = "⛽",
        ["Vehicle Maintenance"]          = "🔧",
        ["Rent"]                         = "🏠",
        ["Electricity"]                  = "💡",
        ["Water"]                        = "💧",
        ["Internet/WiFi"]                = "📶",
        ["Cooking Gas"]                  = "🔥",
        ["Airtime"]                      = "📱",
        ["Mobile Data"]                  = "📡",
        ["School Fees"]                  = "🎓",
        ["Medical/Pharmacy"]             = "💊",
        ["SHA/NHIF"]                     = "🏥",
        ["Family Support"]               = "👨‍👩‍👧",
        ["Childcare/House Help"]         = "🧹",
        ["Shopping/Clothing"]            = "👗",
        ["Entertainment"]                = "🎬",
        ["Subscriptions"]                = "📺",
        ["Personal Care/Salon"]          = "💈",
        ["Betting/Gambling"]             = "🎲",
        ["Savings"]                      = "🏦",
        ["M-Shwari/Locked Savings"]      = "🔒",
        ["SACCO/Chama"]                  = "🤝",
        ["Loan Repayment"]               = "💰",
        ["Fuliza"]                       = "📉",
        ["Insurance"]                    = "🛡️",
        ["Investments"]                  = "📈",
        ["Church/Mosque/Tithe"]          = "⛪",
        ["Donations/Harambee"]           = "🤲",
        ["Transfers (to people)"]        = "↗️",
        ["M-Pesa Charges/Fees"]          = "💸",
        ["Salary/Wages"]                 = "💼",
        ["Business Income"]              = "🏪",
        ["Received from Family/Friends"] = "🎁",
        ["Refunds/Reversals"]            = "↩️",
        ["Other Income"]                 = "💵",
        ["Withdrawals"]                  = "🏧",
    };

    public static string For(string? category) =>
        category is not null && Map.TryGetValue(category, out var e) ? e : "💳";
}
