using Mapsui;
using Mapsui.UI;
using Mapsui.Projections;
using Microsoft.Maui.Devices.Sensors;
using TourGuideApp.ViewModels;
using TourGuideApp.Models; 

namespace TourGuideApp.Pages;

public partial class MapPage : ContentPage
{
    readonly MapViewModel viewModel;

    public MapPage()
    {
        InitializeComponent();

        viewModel = new MapViewModel();

        BindingContext = viewModel;

        map.Map = viewModel.Map;
        map.Info += Map_Info;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var location = await Geolocation.GetLocationAsync();

        if (location != null)
        {
            viewModel.AddCurrentLocationMarker(location.Longitude, location.Latitude);

            var (x, y) = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

            var point = new MPoint(x, y);

            map.Map.Navigator.CenterOnAndZoomTo(point, 5);
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
                viewModel.AddCurrentLocationMarker(location.Longitude, location.Latitude);

                var (x, y) = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

                var point = new MPoint(x, y);

                map.Map.Navigator.CenterOnAndZoomTo(point, 5);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }

    private void Map_Info(object? sender, Mapsui.MapInfoEventArgs e)
    {
        if (e.MapInfo?.Feature == null)
            return;

        var poi = e.MapInfo.Feature["POI"] as POI;

        if (poi != null)
        {
            viewModel.PlayPOIManually(poi);
        }
    }

}