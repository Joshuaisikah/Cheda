using Cheda.Core.Models;

namespace Cheda.Core.Categorization;

/// <summary>
/// Matches transactions by amount range, time of day, day of week, and/or type.
/// Designed to catch recurring contextual patterns (e.g. morning matatu fares).
/// </summary>
public sealed class PatternRule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Priority { get; set; }
    public required string Label { get; init; }
    public IReadOnlyList<TransactionType>? TransactionTypes { get; init; }
    public decimal? AmountMin { get; init; }
    public decimal? AmountMax { get; init; }
    public TimeOnly? TimeOfDayStart { get; init; }
    public TimeOnly? TimeOfDayEnd { get; init; }
    public IReadOnlySet<DayOfWeek>? DaysOfWeek { get; init; }
    public required string Category { get; init; }
    public bool IsEnabled { get; set; } = true;
}
