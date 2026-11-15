using System.ComponentModel;
using Wegs_Cafe.Models;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class PosPage : ContentPage
{
    private const double WideBreakpoint = 800;
    private readonly PosViewModel _vm;
    private readonly CartPanelView _cart = new();
    private bool? _wide;

    public PosPage(PosViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
        SizeChanged += (_, _) => ApplyLayout();
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_vm.Cart.HasItems) _vm.ResetOrderInfo();
        await _vm.LoadAsync();
        BuildPills();
        ApplyLayout();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PosViewModel.SelectedCategory)) BuildPills();
    }

    // Phone = 2 columns + bottom cart sheet. Tablet / wide = 3-4 columns + live cart panel (65% / 35%).
    private void ApplyLayout()
    {
        if (Width <= 0) return;
        var wide = Width >= WideBreakpoint;
        ProductGrid.Span = !wide ? 2 : Width >= 1150 ? 4 : 3;

        if (_wide == wide) return;
        _wide = wide;
        _vm.IsWide = wide;

        MainGrid.ColumnDefinitions[0].Width = wide ? new GridLength(65, GridUnitType.Star) : GridLength.Star;
        MainGrid.ColumnDefinitions[1].Width = wide ? new GridLength(35, GridUnitType.Star) : new GridLength(0);

        TabletCartHost.Content = null;
        SheetHost.Content = null;
        if (wide) { _vm.IsCartOpen = false; TabletCartHost.Content = _cart; }
        else SheetHost.Content = _cart;
    }

    private void BuildPills()
    {
        PillHost.Children.Clear();
        var normal = (Style)Application.Current!.Resources["PillButton"];
        var selected = (Style)Application.Current!.Resources["PillButtonSelected"];
        foreach (var category in _vm.Categories)
        {
            PillHost.Children.Add(new Button
            {
                Text = category,
                Style = category == _vm.SelectedCategory ? selected : normal,
                HeightRequest = 40,
                Command = _vm.SetCategoryCommand,
                CommandParameter = category
            });
        }
    }

    private void OnProductTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is Product p) _vm.AddToCartCommand.Execute(p);
    }
}
