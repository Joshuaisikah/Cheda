using System.Text.RegularExpressions;
using Cheda.Core.Models;

namespace Cheda.Core.Categorization;

/// <summary>
/// Resolves a category in five steps:
///   0. Built-in type rules (deterministic)
///   1. Recipient rules  (counterparty keyword match)
///   2. Pattern rules    (amount / time / type / day)
///   3. Learned memory   (user's past corrections, exact key)
///   4. Similarity guess (token-overlap against learned keys)
///   → NeedsReview if confidence falls below the threshold.
/// </summary>
public sealed partial class RuleBasedCategorizer : ICategorizer
{
    private readonly ICategorizerStore _store;
    private readonly double _reviewThreshold;

    public RuleBasedCategorizer(ICategorizerStore store, double reviewThreshold = 0.6)
    {
        _store = store;
        _reviewThreshold = reviewThreshold;
    }

    public CategorizationResult Categorize(Transaction tx)
    {
        var typeResult = ApplyTypeRules(tx);
        if (typeResult is not null) return typeResult;

        foreach (var rule in _store.GetRecipientRules().Where(r => r.IsEnabled).OrderBy(r => r.Priority))
        {
            if (MatchesRecipientRule(tx, rule))
                return Make(rule.Category, 0.95, $"Recipient rule: {rule.Label}");
        }

        foreach (var rule in _store.GetPatternRules().Where(r => r.IsEnabled).OrderBy(r => r.Priority))
        {
            if (MatchesPatternRule(tx, rule))
                return Make(rule.Category, 0.85, $"Pattern rule: {rule.Label}");
        }

        var key = MappingKey(tx);
        var mappings = _store.GetLearnedMappings();

        var exact = mappings.FirstOrDefault(m =>
            string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            // Each additional confirmation nudges confidence up slightly, capped at 0.99
            var confidence = Math.Min(0.90 + (exact.ConfirmationCount - 1) * 0.01, 0.99);
            return Make(exact.Category, confidence, $"Learned: {exact.Key}");
        }

        var similar = FindSimilar(key, mappings);
        if (similar is not null)
            return Make(similar.Category, 0.50, $"Similar to: {similar.Key}");

        return Make(DefaultCategories.Uncategorized, 0.0, null);
    }

    public void LearnFromCorrection(Transaction tx, string category)
    {
        var key = MappingKey(tx);
        var mappings = _store.GetLearnedMappings();
        var existing = mappings.FirstOrDefault(m =>
            string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Category = category;
            existing.ConfirmationCount++;
            existing.LastUpdated = DateTimeOffset.UtcNow;
            _store.UpsertLearnedMapping(existing);
        }
        else
        {
            _store.UpsertLearnedMapping(new LearnedMapping { Key = key, Category = category });
        }
    }

    // ── Step 0: type-based deterministic rules ───────────────────────────────

    private CategorizationResult? ApplyTypeRules(Transaction tx) => tx.Type switch
    {
        TransactionType.Airtime  => Make(DefaultCategories.Airtime,          1.0, "Type: Airtime"),
        TransactionType.Withdrawn => Make(DefaultCategories.Withdrawals,      1.0, "Type: Withdrawal"),
        TransactionType.MShwari  => Make(DefaultCategories.MShwari,           1.0, "Type: M-Shwari"),
        TransactionType.Fuliza   => Make(DefaultCategories.Fuliza,            1.0, "Type: Fuliza"),
        TransactionType.Reversal => Make(DefaultCategories.RefundsReversals,  1.0, "Type: Reversal"),
        TransactionType.Deposit  => Make(DefaultCategories.Uncategorized,     0.9, "Type: Deposit"),
        _                        => null,
    };

    // ── Step 1: recipient keyword match ─────────────────────────────────────

    private static bool MatchesRecipientRule(Transaction tx, RecipientRule rule)
    {
        if (tx.Counterparty is null) return false;
        var counterparty = tx.Counterparty.ToUpperInvariant();
        return rule.Keywords.Any(k => counterparty.Contains(k.ToUpperInvariant()));
    }

    // ── Step 2: pattern match ────────────────────────────────────────────────

    private static bool MatchesPatternRule(Transaction tx, PatternRule rule)
    {
        if (rule.TransactionTypes is not null && !rule.TransactionTypes.Contains(tx.Type))
            return false;
        if (rule.AmountMin.HasValue && tx.Amount < rule.AmountMin.Value)
            return false;
        if (rule.AmountMax.HasValue && tx.Amount > rule.AmountMax.Value)
            return false;
        if (rule.DaysOfWeek is not null && !rule.DaysOfWeek.Contains(tx.Timestamp.LocalDateTime.DayOfWeek))
            return false;

        if (rule.TimeOfDayStart.HasValue || rule.TimeOfDayEnd.HasValue)
        {
            var t = TimeOnly.FromTimeSpan(tx.Timestamp.LocalDateTime.TimeOfDay);
            if (rule.TimeOfDayStart.HasValue && t < rule.TimeOfDayStart.Value) return false;
            if (rule.TimeOfDayEnd.HasValue   && t > rule.TimeOfDayEnd.Value)   return false;
        }

        return true;
    }

    // ── Step 4: similarity guess ─────────────────────────────────────────────

    private static LearnedMapping? FindSimilar(string key, IReadOnlyList<LearnedMapping> mappings)
    {
        var keyTokens = Tokenize(key);
        if (keyTokens.Count == 0) return null;

        LearnedMapping? best = null;
        double bestScore = 0.3; // minimum Jaccard threshold to qualify as "similar"

        foreach (var m in mappings)
        {
            var mTokens = Tokenize(m.Key);
            if (mTokens.Count == 0) continue;

            var intersection = keyTokens.Intersect(mTokens).Count();
            var union = keyTokens.Union(mTokens).Count();
            var score = (double)intersection / union;

            if (score > bestScore) { bestScore = score; best = m; }
        }

        return best;
    }

    private static HashSet<string> Tokenize(string key) =>
        key.ToLowerInvariant()
           .Split([' ', '-', '_', '/', ':', '.', ','], StringSplitOptions.RemoveEmptyEntries)
           .Where(t => t.Length > 2)
           .ToHashSet();

    // ── Mapping key extraction ───────────────────────────────────────────────
    // Key is stable across re-sends to the same recipient:
    //   Till payments   → "till:123456"
    //   Paybill payments → "paybill:888880/54321"
    //   Person transfers → normalized name (numbers stripped)

    [GeneratedRegex(@"\(Till\s+(\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex TillInCounterparty();

    [GeneratedRegex(@"\((\d+)/(\S+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex PaybillInCounterparty();

    [GeneratedRegex(@"[\d()\-+]")]
    private static partial Regex StripNoise();

    public static string MappingKey(Transaction tx)
    {
        if (tx.Counterparty is null) return $"type:{tx.Type}";

        var till = TillInCounterparty().Match(tx.Counterparty);
        if (till.Success) return $"till:{till.Groups[1].Value}";

        var paybill = PaybillInCounterparty().Match(tx.Counterparty);
        if (paybill.Success) return $"paybill:{paybill.Groups[1].Value}/{paybill.Groups[2].Value}";

        var normalized = StripNoise()
            .Replace(tx.Counterparty, "")
            .Trim()
            .ToLowerInvariant();
        normalized = string.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length > 0 ? normalized : $"type:{tx.Type}";
    }

    // ── Result factory ───────────────────────────────────────────────────────

    private CategorizationResult Make(string? category, double confidence, string? rule) =>
        new()
        {
            Category = category,
            Confidence = confidence,
            MatchedRule = rule,
            NeedsReview = confidence < _reviewThreshold,
        };
}
