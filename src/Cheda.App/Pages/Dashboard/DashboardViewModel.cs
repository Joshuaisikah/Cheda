using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.App.Storage;
using Cheda.Core.Analytics;
using Cheda.Core.Categorization;
using Cheda.Core.Models;
using Cheda.Core.Sms;
using Cheda.Core.Storage;
using Microsoft.Maui.Controls;

namespace Cheda.App.Pages.Dashboard;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly IAnalyticsEngine       _analytics;
    private readonly ICategorizer           _categorizer;
    private readonly DatabaseService        _db;
    private readonly IImportService?        _import;
    private readonly ISmsReader?            _smsReader;
    private readonly ISettingsRepository    _settings;

    [ObservableProperty] private decimal  _balance;
    [ObservableProperty] private decimal  _spentThisMonth;
    [ObservableProperty] private decimal  _receivedThisMonth;
    [ObservableProperty] private string   _monthLabel    = "";
    [ObservableProperty] private string   _netFlowLabel  = "";
    [ObservableProperty] private Color    _netFlowColor  = Colors.White;
    [ObservableProperty] private IReadOnlyList<RecentRow> _recent = [];
    [ObservableProperty] private int      _reviewCount;
    [ObservableProperty] private bool     _hasReviewItems;

    // Balance card extras
    [ObservableProperty] private string _sim1Balance   = "–";
    [ObservableProperty] private string _sim2Balance   = "–";
    [ObservableProperty] private bool   _hasSim2;
    [ObservableProperty] private string _moneyInLabel  = "KES 0";
    [ObservableProperty] private string _moneyOutLabel = "KES 0";
    [ObservableProperty] private string _moneyInChange  = "";
    [ObservableProperty] private string _moneyOutChange = "";
    [ObservableProperty] private bool   _hasFulizaDebt;
    [ObservableProperty] private string _fulizaStatusText = "No Fuliza data";
    [ObservableProperty] private string _fulizaLimitInfo   = "";
    [ObservableProperty] private string _lastRefreshed     = "";

    // Insights strip
    [ObservableProperty] private IReadOnlyList<InsightCard> _insights = [];

    public DashboardViewModel(
        ITransactionRepository repo,
        IAnalyticsEngine       analytics,
        ICategorizer           categorizer,
        DatabaseService        db,
        ISettingsRepository    settings,
        IImportService?        import    = null,
        ISmsReader?            smsReader = null)
    {
        _repo      = repo;
        _analytics = analytics;
        _categorizer = categorizer;
        _db        = db;
        _settings  = settings;
        _import    = import;
        _smsReader = smsReader;
    }

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    [RelayCommand]
    private async Task ViewAllAsync()
    {
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
        var txVm = IPlatformApplication.Current!.Services
            .GetRequiredService<Pages.Transactions.TransactionsViewModel>();
        txVm.PendingTypeFilter = "Uncategorized";
        await Shell.Current.GoToAsync("//TransactionsTab");
    }

    private async Task LoadAsync()
    {
        // After biometric/PIN auth the dashboard is shown before the DB opens.
        // Wait here (non-blocking) until InitializeAsync completes.
        await _db.WhenReadyAsync();

        // Scan for any new SMS before loading data so the numbers are always current.
        // Uses a since-timestamp so subsequent loads only read new messages (fast).
        if (_import is not null && (_smsReader?.HasPermission ?? false))
        {
            try
            {
                var lastMs = _settings.Get("AutoScanLastMs");
                DateTimeOffset? since = long.TryParse(lastMs, out var ms)
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                    : null;
                _settings.Set("AutoScanLastMs", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                await _import.ImportInboxAsync(since);
            }
            catch { /* non-critical */ }
        }

        var now   = DateTimeOffset.Now;
        var all   = _repo.GetAll();
        var range = new DateRange(new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset), now);
        var month = _repo.GetInRange(range);

        var summary       = _analytics.GetSummary(month, range);
        Balance           = _analytics.GetCurrentBalance(all) ?? 0m;
        SpentThisMonth    = summary.TotalExpenses;
        ReceivedThisMonth = summary.TotalIncome;
        MonthLabel        = now.ToString("MMMM yyyy");

        var net = summary.TotalIncome - summary.TotalExpenses;
        NetFlowLabel = net >= 0
            ? $"▲  Ksh {net:N0} net"
            : $"▼  Ksh {-net:N0} net";
        NetFlowColor = net >= 0
            ? Color.FromArgb("#10B981")
            : Color.FromArgb("#EF4444");

        // Money In / Out labels
        MoneyInLabel  = FormatKes(summary.TotalIncome);
        MoneyOutLabel = FormatKes(summary.TotalExpenses);

        // Compare vs previous 30-day window
        var prevStart = now.AddDays(-60);
        var prevEnd   = now.AddDays(-30);
        var prevRange = new DateRange(prevStart, prevEnd);
        var prev      = _repo.GetInRange(prevRange);
        var prevSummary = _analytics.GetSummary(prev, prevRange);

        MoneyInChange  = ChangeLabel(summary.TotalIncome,   prevSummary.TotalIncome);
        MoneyOutChange = ChangeLabel(summary.TotalExpenses, prevSummary.TotalExpenses);

        // SIM balances: build a flat (slot, balance, timestamp) list from all transactions.
        // Each transaction contributes its own slot+balance, and self-transfer records also
        // contribute the OTHER SIM's balance captured at detection time. This ensures a
        // cross-SIM transfer doesn't leave one SIM showing a stale balance.
        // Exclude Fuliza drawdowns whose BalanceAfter is outstanding debt, not M-Pesa balance.
        var simBalances = new List<(int Slot, decimal Balance, DateTimeOffset Timestamp)>();
        foreach (var t in all)
        {
            if (t.Type == TransactionType.Fuliza && !t.IsNonExpenseTransfer) continue;
            if (t.SimSlot.HasValue && t.BalanceAfter.HasValue)
                simBalances.Add((t.SimSlot.Value, t.BalanceAfter.Value, t.Timestamp));
            if (t.SelfTransferSimSlot.HasValue && t.SelfTransferBalanceAfter.HasValue)
                simBalances.Add((t.SelfTransferSimSlot.Value, t.SelfTransferBalanceAfter.Value, t.Timestamp));
        }

        var simGroups = simBalances
            .GroupBy(x => x.Slot)
            .OrderBy(g => g.Key)
            .ToList();

        if (simGroups.Count >= 2)
        {
            var s1 = simGroups[0].OrderByDescending(x => x.Timestamp).First();
            var s2 = simGroups[1].OrderByDescending(x => x.Timestamp).First();
            Sim1Balance = $"Ksh{s1.Balance:N2}";
            Sim2Balance = $"Ksh{s2.Balance:N2}";
            HasSim2     = true;
        }
        else if (simGroups.Count == 1)
        {
            var s1 = simGroups[0].OrderByDescending(x => x.Timestamp).First();
            Sim1Balance = $"Ksh{s1.Balance:N2}";
            Sim2Balance = "–";
            HasSim2     = false;
        }
        else
        {
            // No live SMS yet — use most recent imported balance as SIM 1
            var anyBal = all.FirstOrDefault(t => t.BalanceAfter.HasValue);
            Sim1Balance = anyBal?.BalanceAfter is decimal b ? $"Ksh{b:N2}" : "–";
            Sim2Balance = "–";
            HasSim2     = false;
        }

        // Fuliza: outstanding = most recent drawdown BalanceAfter; limit = from latest repayment
        var fulizaTxs    = all.Where(t => t.Type == TransactionType.Fuliza).ToList();
        var lastDrawdown = fulizaTxs
            .Where(t => !t.IsNonExpenseTransfer && t.BalanceAfter.HasValue)
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefault();
        var lastRepaid   = fulizaTxs
            .Where(t => t.IsNonExpenseTransfer && t.FulizaLimit.HasValue)
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefault();

        var fulizaLimit       = lastRepaid?.FulizaLimit;
        var fulizaOutstanding = lastDrawdown?.BalanceAfter ?? 0m;

        // If the last event was a repayment (more recent than last drawdown), outstanding is 0
        if (lastRepaid is not null &&
            (lastDrawdown is null || lastRepaid.Timestamp > lastDrawdown.Timestamp))
            fulizaOutstanding = 0m;

        if (fulizaTxs.Count > 0)
        {
            HasFulizaDebt    = fulizaOutstanding > 0;
            FulizaStatusText = HasFulizaDebt
                ? $"Ksh {fulizaOutstanding:N2} outstanding"
                : "No outstanding balance";
            FulizaLimitInfo  = fulizaLimit.HasValue
                ? $"Limit: Ksh {fulizaLimit:N0}  •  Available: Ksh {(fulizaLimit.Value - fulizaOutstanding):N0}"
                : "";
        }
        else
        {
            HasFulizaDebt    = false;
            FulizaStatusText = "No Fuliza data";
            FulizaLimitInfo  = "";
        }

        // Last refreshed
        LastRefreshed = $"{(int)(DateTimeOffset.Now - now).TotalMinutes + 1} min ago";
        if (DateTimeOffset.Now.Subtract(now).TotalSeconds < 60)
            LastRefreshed = "just now";

        // Recent transactions
        Recent = all
            .OrderByDescending(t => t.Timestamp)
            .Take(7)
            .Select(t => new RecentRow(t))
            .ToList();

        ReviewCount    = all.Count(t =>
            t.Category == Core.Categorization.DefaultCategories.Uncategorized ||
            t.CategoryConfidence < 0.6);
        HasReviewItems = ReviewCount > 0;

        // Insights
        Insights = BuildInsights(all, month, summary);

        return;
    }

    private static IReadOnlyList<InsightCard> BuildInsights(
        IReadOnlyList<Transaction> all,
        IReadOnlyList<Transaction> month,
        PeriodSummary summary)
    {
        var cards = new List<InsightCard>();

        // Top spending category
        var topCat = month
            .Where(t => t.Type is not TransactionType.Received and
                              not TransactionType.Deposit and
                              not TransactionType.Reversal)
            .GroupBy(t => t.Category ?? "Other")
            .Select(g => (Cat: g.Key, Total: g.Sum(t => t.Amount)))
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();

        if (topCat != default && summary.TotalExpenses > 0)
        {
            var pct = (int)(topCat.Total / summary.TotalExpenses * 100);
            cards.Add(new InsightCard(
                "🏆",
                $"{topCat.Cat} — {pct}% of expenses",
                $"Ksh{topCat.Total:N0} in the last 30 days"));
        }

        // M-Pesa fees
        var fees = month
            .Where(t => t.TransactionCost.HasValue && t.TransactionCost > 0)
            .Sum(t => t.TransactionCost!.Value);
        if (fees > 0)
            cards.Add(new InsightCard("💸", "M-Pesa fees", $"Ksh{fees:N0} over the last 30 days"));

        return cards;
    }

    private static string FormatKes(decimal amount)
    {
        if (amount >= 1_000_000) return $"KES {amount / 1_000_000:F1}M";
        if (amount >= 1_000)     return $"KES {amount / 1_000:F1}K";
        return $"KES {amount:N0}";
    }

    private static string ChangeLabel(decimal current, decimal previous)
    {
        if (previous == 0) return "";
        var pct = (int)((current - previous) / previous * 100);
        return pct >= 0
            ? $"↑ {pct}% vs prev 30d"
            : $"↓ {-pct}% vs prev 30d";
    }
}

public sealed class RecentRow(Transaction tx)
{
    public Transaction Tx           { get; } = tx;
    public string  Counterparty     { get; } = CleanName(tx.Counterparty);
    public string  Category         { get; } = tx.Category ?? "Uncategorized";
    public string  CategoryIcon     { get; } = CategoryEmoji.For(tx.Category);
    public string  CategoryDisplay  { get; } = $"{CategoryEmoji.For(tx.Category)} {tx.Category ?? "Other"}";
    public string  SimDisplay       { get; } = tx.SimSlot.HasValue ? $"📱 SIM {tx.SimSlot}" : "";
    public bool    HasSim           { get; } = tx.SimSlot.HasValue;
    public string  TypeLabel        { get; } = Pages.Transactions.TransactionEditViewModel.TypeLabelFor(tx.Type);
    public string  TypeIcon         { get; } = Pages.Transactions.TransactionEditViewModel.TypeIconFor(tx.Type);
    public Color   AccentColor      { get; } = Pages.Transactions.TransactionEditViewModel.AccentColorFor(tx.Type);
    public Color   AmountColor      { get; } = Pages.Transactions.TransactionEditViewModel.AccentColorFor(tx.Type);
    public string  AmountLabel      { get; } = $"Ksh {tx.Amount:N0}";
    public string  TimeLabel        { get; } = tx.Timestamp.LocalDateTime.ToString("MMM dd, HH:mm");

    private static string CleanName(string? raw)
    {
        if (raw is null) return "—";
        var name = System.Text.RegularExpressions.Regex.Replace(raw.Trim(), @"\s+\d{9,12}$", "").Trim();
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
    }
}

public sealed record InsightCard(string Icon, string Title, string Subtitle);

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
        ["Transfers (to people)"]        = "💸",
        ["M-Pesa Charges/Fees"]          = "💸",
        ["Salary/Wages"]                 = "💼",
        ["Business Income"]              = "🏪",
        ["Received from Family/Friends"] = "🎁",
        ["Refunds/Reversals"]            = "↩️",
        ["Other Income"]                 = "💵",
        ["Withdrawals"]                  = "🏧",
        ["Own Transfer"]                 = "🔄",
        ["Fuliza Repayment"]             = "📉",
    };

    public static string For(string? category) =>
        category is not null && Map.TryGetValue(category, out var e) ? e : "💳";
}
