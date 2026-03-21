using TourGuideApp.Models;

namespace TourGuideApp.Pages; 

[QueryProperty(nameof(Poi), "poi")]
public partial class PlaceDetailPage : ContentPage
{
    public POI Poi
    {
        set
        {
            BindingContext = value;
        }
    }

    public PlaceDetailPage()
    {
        InitializeComponent();
    }
    private async void OnBackTapped(object sender, EventArgs e)
    {
        await this.FadeTo(0.5, 100);
        await Shell.Current.GoToAsync("..");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is POI poi)
        {
            await TextToSpeech.Default.SpeakAsync(poi.Description);
        }
    }
}