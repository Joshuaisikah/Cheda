using Cheda.App.Pages.Dashboard;
using Cheda.Core.Categorization;

namespace Cheda.App.Pages.Transactions;

public partial class CategoryPickerPage : ContentPage
{
    public string? SelectedCategory { get; private set; }

    private readonly List<CategoryListItem> _all;
    private readonly List<CategoryListItem> _flat;

    public CategoryPickerPage()
    {
        InitializeComponent();
        BindingContext = this;

        var grouped = DefaultCategories.All
            .GroupBy(c => c.Group)
            .SelectMany(g =>
            {
                var header = new CategoryListItem(groupLabel: GroupHeader(g.Key), isHeader: true);
                var items  = g.Select(c =>
                    new CategoryListItem(c.Name, CategoryEmoji.For(c.Name), isHeader: false));
                return Enumerable.Repeat(header, 1).Concat(items);
            })
            .ToList();

        _all  = grouped;
        _flat = DefaultCategories.All
            .Select(c => new CategoryListItem(c.Name, CategoryEmoji.For(c.Name), isHeader: false))
            .OrderBy(c => c.Name)
            .ToList();

        CategoryList.ItemsSource = _all;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        var q = (e.NewTextValue ?? "").Trim();
        ClearBtn.IsVisible = q.Length > 0;
        CategoryList.ItemsSource = string.IsNullOrEmpty(q)
            ? _all
            : _flat.Where(c => c.Name?.ToLowerInvariant().Contains(q.ToLowerInvariant()) ?? false).ToList();
    }

    private void OnClearSearch(object? sender, EventArgs e)
    {
        SearchEntry.Text = "";
        CategoryList.ItemsSource = _all;
        ClearBtn.IsVisible = false;
    }

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string name)
        {
            SelectedCategory = name;
            await Navigation.PopAsync();
        }
    }

    private static string GroupHeader(CategoryGroup group) => group switch
    {
        CategoryGroup.Income         => "💰  INCOME",
        CategoryGroup.Food           => "🍽️  FOOD & GROCERIES",
        CategoryGroup.Transport      => "🚌  TRANSPORT",
        CategoryGroup.Bills          => "🏠  BILLS & UTILITIES",
        CategoryGroup.Airtime        => "📱  AIRTIME & DATA",
        CategoryGroup.PersonalFamily => "👨‍👩‍👧  PERSONAL & FAMILY",
        CategoryGroup.Lifestyle      => "🎬  LIFESTYLE",
        CategoryGroup.Financial      => "🏦  FINANCIAL",
        CategoryGroup.Giving         => "🤲  GIVING",
        _                            => "📋  OTHER",
    };
}

public sealed class CategoryListItem
{
    public CategoryListItem(string? name = null, string? emoji = null, bool isHeader = false, string? groupLabel = null)
    {
        Name       = name;
        Emoji      = emoji ?? "💳";
        IsHeader   = isHeader;
        GroupLabel = groupLabel ?? name ?? "";
        IsCategory = !isHeader;
    }

    public string?  Name       { get; }
    public string   Emoji      { get; }
    public bool     IsHeader   { get; }
    public bool     IsCategory { get; }
    public string   GroupLabel { get; }
}
