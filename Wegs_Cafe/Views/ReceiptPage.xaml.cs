using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class ReceiptPage : ContentPage
{
    public ReceiptPage(ReceiptViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
