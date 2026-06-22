namespace Cheda.Core.Categorization;

/// <summary>
/// Stores a user's past categorization decision keyed by a normalized counterparty identifier.
/// This is the "self-learning" — fully offline, no ML.
/// </summary>
public sealed class LearnedMapping
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Key { get; init; }
    public required string Category { get; set; }
    public int ConfirmationCount { get; set; } = 1;
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
