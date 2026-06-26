using Cheda.App.Pages.Dashboard;

namespace Cheda.App.Pages.Transactions;

public partial class TransactionEditPage : ContentPage
{
    public TransactionEditPage(TransactionEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        RefreshCategoryChip(vm.SelectedCategory);
    }

    private async void OnCategoryEditTapped(object? sender, EventArgs e)
    {
        var picker = new CategoryPickerPage();
        picker.Disappearing += async (_, _) =>
        {
            if (picker.SelectedCategory is null) return;
            var vm = (TransactionEditViewModel)BindingContext;
            vm.SelectedCategory = picker.SelectedCategory;
            RefreshCategoryChip(picker.SelectedCategory);
            await vm.SaveCategoryAsync();
        };
        // Push as normal navigation — the system back gesture pops it automatically.
        await Shell.Current.Navigation.PushAsync(picker);
    }

    private void RefreshCategoryChip(string? category)
    {
        var emoji = category is not null ? CategoryEmoji.For(category) : "💳";
        CategoryChipLabel.Text = $"{emoji}  {category ?? "Uncategorized"}";
    }

}
