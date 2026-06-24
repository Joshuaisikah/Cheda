using Cheda.App.Pages.Dashboard;
using Cheda.Core.Categorization;
using CommunityToolkit.Mvvm.Input;

namespace Cheda.App.Pages.Transactions;

public partial class CategoryPickerPage : ContentPage
{
    public string? SelectedCategory { get; private set; }

    private readonly List<CategoryItem> _all;

    public CategoryPickerPage()
    {
        BackgroundColor = Color.FromArgb("#0F172A");
        InitializeComponent();
        BindingContext = this;

        _all = DefaultCategories.All
            .OrderBy(c => c.Name)
            .Select(c => new CategoryItem(c.Name, CategoryEmoji.For(c.Name)))
            .ToList();

        CategoryList.ItemsSource = _all;
    }

    [RelayCommand]
    private async Task Cancel() => await Navigation.PopModalAsync();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Focus search box so the keyboard appears and user can type immediately.
        Dispatcher.Dispatch(() => SearchEntry.Focus());
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        var q = (e.NewTextValue ?? "").Trim().ToLowerInvariant();
        CategoryList.ItemsSource = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(c => c.Name.ToLowerInvariant().Contains(q)).ToList();
    }

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string name)
        {
            SelectedCategory = name;
            await Navigation.PopModalAsync();
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = Navigation.PopModalAsync();
        return true;
    }
}

public sealed record CategoryItem(string Name, string Emoji);
