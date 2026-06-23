using Cheda.Core.Categorization;
using SQLite;

namespace Cheda.App.Storage.Entities;

[Table("LearnedMappings")]
internal sealed class LearnedMappingEntity
{
    [PrimaryKey]
    public string Id { get; set; } = "";

    [Unique]
    public string Key { get; set; } = "";

    public string Category        { get; set; } = "";
    public int    ConfirmationCount { get; set; }
    public long   LastUpdatedTicks  { get; set; }

    // ── Temporal profile columns (added for amount+time pattern learning) ─────
    public decimal TypicalAmountLow  { get; set; }
    public decimal TypicalAmountHigh { get; set; }
    public int     SampleCount       { get; set; }
    public int     DayOfMonthMask    { get; set; }
    public int     HourMask          { get; set; }

    internal static LearnedMappingEntity From(LearnedMapping m) => new()
    {
        Id                = m.Id.ToString(),
        Key               = m.Key,
        Category          = m.Category,
        ConfirmationCount = m.ConfirmationCount,
        LastUpdatedTicks  = m.LastUpdated.UtcTicks,
        TypicalAmountLow  = m.TypicalAmountLow,
        TypicalAmountHigh = m.TypicalAmountHigh,
        SampleCount       = m.SampleCount,
        DayOfMonthMask    = m.DayOfMonthMask,
        HourMask          = m.HourMask,
    };

    internal LearnedMapping ToDomain() => new()
    {
        Id                = Guid.Parse(Id),
        Key               = Key,
        Category          = Category,
        ConfirmationCount = ConfirmationCount,
        LastUpdated       = new DateTimeOffset(LastUpdatedTicks, TimeSpan.Zero),
        TypicalAmountLow  = TypicalAmountLow,
        TypicalAmountHigh = TypicalAmountHigh,
        SampleCount       = SampleCount,
        DayOfMonthMask    = DayOfMonthMask,
        HourMask          = HourMask,
    };
}
