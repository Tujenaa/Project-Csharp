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

    private async void OnChangePasswordTapped(object sender, EventArgs e)
    {
        await DisplayAlert(
            LocalizationService.Get("notification"), 
            LocalizationService.Get("feature_under_development"), 
            LocalizationService.Get("ok"));
    }
}