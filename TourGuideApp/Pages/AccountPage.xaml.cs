using TourGuideApp.Services;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class AccountPage : ContentPage
{
    public AccountPage(AccountViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel; 
    }

    private async void OnBackTapped(object sender, EventArgs e)
        => await Navigation.PopAsync();
}