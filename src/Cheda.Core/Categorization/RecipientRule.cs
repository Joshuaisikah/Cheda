namespace Cheda.Core.Categorization;

/// <summary>
/// Maps a counterparty keyword (name, till, paybill) to a category.
/// Priority-ordered; first match wins.
/// </summary>
public sealed class RecipientRule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Priority { get; set; }
    public required string Label { get; init; }
    public required IReadOnlyList<string> Keywords { get; init; }
    public required string Category { get; init; }
    public bool IsEnabled { get; set; } = true;
}
