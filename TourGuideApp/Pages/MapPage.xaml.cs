using Mapsui;
using Mapsui.UI.Maui;
using Microsoft.Maui.Devices.Sensors;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class MapPage : ContentPage
{
    MapViewModel viewModel;

    public MapPage()
    {
        InitializeComponent();

        viewModel = new MapViewModel();

        BindingContext = viewModel;

        map.Map = viewModel.Map;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var location = await Geolocation.GetLocationAsync();

        if (location != null)
        {
            var point = new MPoint(location.Longitude, location.Latitude);

            map.Map.Navigator.CenterOn(point);
            map.Map.Navigator.ZoomTo(1000);
        }
    }

    private async void OnCurrentLocationTapped(object sender, EventArgs e)
    {
        try
        {
            var location = await Geolocation.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium));

            if (location != null)
            {
                var point = new MPoint(location.Longitude, location.Latitude);
                map.Map.Navigator.CenterOn(point);
                map.Map.Navigator.ZoomTo(1000);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }
}