using CommunityToolkit.Mvvm.ComponentModel;
using Cheda.Core.Models;

namespace Cheda.App.Pages.Review;

public partial class ReviewItem : ObservableObject
{
    [ObservableProperty] private string? _suggestedCategory;

    public required Transaction Transaction { get; init; }
    public          double       Confidence  { get; init; }
}
