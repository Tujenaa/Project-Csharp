namespace TourGuideApp.Pages;

public partial class PlaceDetailPage : ContentPage
{
    public PlaceDetailPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}