namespace Cheda.Core.Insights;

/// <summary>
/// A single rule-based observation about the user's own data.
/// Framed as neutral observations — no advisor persona, no investment advice.
/// </summary>
public sealed record Insight
{
    public required string Id { get; init; }           // stable key, e.g. "high-fees"
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required InsightSeverity Severity { get; init; }
    public string? Category { get; init; }             // related category if applicable
    public decimal? Amount { get; init; }              // the figure driving the insight
    public required DateTimeOffset GeneratedAt { get; init; }
}
