using Wegs_Cafe.Models;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class StaffPage : ContentPage
{
    private readonly StaffViewModel _vm;

    public StaffPage(StaffViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
        SizeChanged += OnSizeChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    // Phone: stack the form under the list. Tablet: side by side.
    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (Content is not ScrollView { Content: Grid grid }) return;
        var narrow = Width < 700;
        grid.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : GridLength.Star;
        if (narrow)
        {
            Grid.SetColumn(FormCard, 0);
            Grid.SetRow(FormCard, 1);
            if (grid.RowDefinitions.Count < 2) { grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); }
        }
        else
        {
            Grid.SetColumn(FormCard, 1);
            Grid.SetRow(FormCard, 0);
        }
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: AppUser user })
            await _vm.ToggleActiveCommand.ExecuteAsync(user);
    }
}
