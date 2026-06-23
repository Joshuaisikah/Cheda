using Cheda.Core.Models;

namespace Cheda.Core.Categorization;

/// <summary>
/// Stores a user's past categorization decision keyed by a normalized counterparty identifier.
/// Also accumulates an amount band and time-of-day fingerprint from every observed transaction
/// (auto-updated by ObserveTransaction for high-confidence matches, and by LearnFromCorrection
/// for user corrections). These temporal signals are soft confidence modifiers — they never
/// override explicit rules and never create a new mapping on their own.
/// </summary>
public sealed class LearnedMapping
{
    public Guid   Id                { get; init; } = Guid.NewGuid();
    public required string Key      { get; init; }
    public required string Category { get; set; }
    public int    ConfirmationCount { get; set; } = 1;
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    // ── Temporal profile ─────────────────────────────────────────────────────
    // Built from observed transactions; never stored alone without a category.

    /// <summary>Running minimum of observed amounts.</summary>
    public decimal TypicalAmountLow  { get; set; }
    /// <summary>Running maximum of observed amounts.</summary>
    public decimal TypicalAmountHigh { get; set; }
    /// <summary>Total observations used to build the temporal profile.</summary>
    public int SampleCount           { get; set; }
    /// <summary>Bitmask: bit (d-1) is set when day d of month was observed.</summary>
    public int DayOfMonthMask        { get; set; }
    /// <summary>Bitmask: bit h is set when hour-of-day h was observed (local time).</summary>
    public int HourMask              { get; set; }

    /// <summary>
    /// Records one transaction's amount, day-of-month, and hour-of-day into the profile.
    /// Call this from ObserveTransaction (high-confidence auto) and LearnFromCorrection (user).
    /// </summary>
    public void UpdateTemporalProfile(Transaction tx)
    {
        if (tx.Amount > 0)
        {
            if (SampleCount == 0)
            {
                TypicalAmountLow  = tx.Amount;
                TypicalAmountHigh = tx.Amount;
            }
            else
            {
                if (tx.Amount < TypicalAmountLow)  TypicalAmountLow  = tx.Amount;
                if (tx.Amount > TypicalAmountHigh) TypicalAmountHigh = tx.Amount;
            }
        }

        var local = tx.Timestamp.LocalDateTime;
        DayOfMonthMask |= 1 << (local.Day - 1);   // bits 0-30 for days 1-31
        HourMask       |= 1 << local.Hour;          // bits 0-23 for hours 0-23
        SampleCount++;
    }
}
