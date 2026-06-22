namespace Cheda.Core.Categorization;

public sealed record CategorizationResult
{
    public required string? Category { get; init; }
    public required double Confidence { get; init; }
    public string? MatchedRule { get; init; }
    public required bool NeedsReview { get; init; }
}
