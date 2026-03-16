using TourGuideApp.Pages;

namespace TourGuideApp.Pages;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
    }

    private async void OnMapButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//map");
    }

    private async void OnPlaceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0)
            return;

        string place = e.CurrentSelection[0].ToString();

        await DisplayAlert("Địa điểm", place, "OK");
    }
}