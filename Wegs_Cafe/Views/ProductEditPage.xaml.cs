using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class ProductEditPage : ContentPage
{
    public ProductEditPage(ProductEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
