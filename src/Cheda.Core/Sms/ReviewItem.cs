using Cheda.Core.Models;

namespace Cheda.Core.Sms;

/// <summary>
/// A parsed transaction whose categorization confidence fell below the review threshold.
/// The user must confirm or correct the category; their decision is fed back into
/// learned memory so the same pattern is not asked again.
/// </summary>
public sealed class ReviewItem
{
    public required Transaction Transaction { get; init; }
    public required double Confidence { get; init; }
    public string? SuggestedCategory { get; init; }
}
