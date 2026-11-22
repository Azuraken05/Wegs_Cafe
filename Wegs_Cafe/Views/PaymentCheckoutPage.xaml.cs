using Wegs_Cafe.Helpers;
using Wegs_Cafe.ViewModels;

namespace Wegs_Cafe.Views;

public partial class PaymentCheckoutPage : ContentPage
{
    public PaymentCheckoutPage(PaymentCheckoutViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }
}
