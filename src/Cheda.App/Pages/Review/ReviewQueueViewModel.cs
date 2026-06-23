using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.Core.Categorization;
using Cheda.Core.Models;
using Cheda.Core.Storage;

namespace Cheda.App.Pages.Review;

public partial class ReviewQueueViewModel : ViewModelBase
{
    private readonly ITransactionRepository _repo;
    private readonly ICategorizer           _categorizer;

    [ObservableProperty] private IReadOnlyList<ReviewItem> _items = [];

    public string[] Categories { get; } = DefaultCategories.All.Select(c => c.Name).ToArray();

    public ReviewQueueViewModel(ITransactionRepository repo, ICategorizer categorizer)
    {
        _repo        = repo;
        _categorizer = categorizer;
    }

    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    [RelayCommand]
    private void Confirm(ReviewItem item)
    {
        _categorizer.LearnFromCorrection(item.Transaction, item.SuggestedCategory ?? item.Transaction.Category ?? DefaultCategories.Uncategorized);
        item.Transaction.Category           = item.SuggestedCategory;
        item.Transaction.CategoryConfidence = 1.0;
        _repo.Update(item.Transaction);
        Items = Items.Where(i => i != item).ToList();
    }

    [RelayCommand]
    private void SetCategory(ReviewItemCategoryChange change)
    {
        change.Item.SuggestedCategory = change.Category;
        Confirm(change.Item);
    }

    private Task LoadAsync()
    {
        var all = _repo.GetAll();
        Items = all
            .Where(t => t.CategoryConfidence < 0.6 || t.Category == DefaultCategories.Uncategorized)
            .OrderByDescending(t => t.Timestamp)
            .Select(t => new ReviewItem
            {
                Transaction       = t,
                Confidence        = t.CategoryConfidence,
                SuggestedCategory = t.Category,
            })
            .ToList();
        return Task.CompletedTask;
    }
}

public sealed class ReviewItemCategoryChange
{
    public required ReviewItem Item     { get; init; }
    public required string     Category { get; init; }
}
