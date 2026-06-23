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

    public string Category { get; set; } = "";
    public int ConfirmationCount { get; set; }
    public long LastUpdatedTicks { get; set; }

    internal static LearnedMappingEntity From(LearnedMapping m) => new()
    {
        Id                = m.Id.ToString(),
        Key               = m.Key,
        Category          = m.Category,
        ConfirmationCount = m.ConfirmationCount,
        LastUpdatedTicks  = m.LastUpdated.UtcTicks,
    };

    internal LearnedMapping ToDomain() => new()
    {
        Id                = Guid.Parse(Id),
        Key               = Key,
        Category          = Category,
        ConfirmationCount = ConfirmationCount,
        LastUpdated       = new DateTimeOffset(LastUpdatedTicks, TimeSpan.Zero),
    };
}
