using System.Linq;
using Microsoft.Maui.ApplicationModel;
using TourGuideApp.Pages;
using TourGuideApp.Services;
using TourGuideApp.Models;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm = new();

    public HomePage()
    {
        InitializeComponent();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadData();

        if (DeviceInfo.Platform == DevicePlatform.Android ||
            DeviceInfo.Platform == DevicePlatform.iOS)
        {
            await LoadLocation();
        }
    }

    private async void OnMapButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//map");
    }

    private async void OnScanQrClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new QrScannerPage());
    }

    private async void OnViewAllToursTapped(object sender, EventArgs e)
    {
        // Mở bản đồ không chọn tour cụ thể (xem tất cả)
        MapTourState.SelectedTour = null;
        await Shell.Current.GoToAsync("//map");
    }

    async Task LoadLocation()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android &&
            DeviceInfo.Platform != DevicePlatform.iOS)
            return;

        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                LocationLabel.Text = "Chưa cấp quyền";
                return;
            }

            var location = await Geolocation.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium));

            if (location != null)
            {
                var placemarks = await Geocoding.GetPlacemarksAsync(location);
                var place = placemarks?.FirstOrDefault();

                if (place != null)
                {
                    string street = place.Thoroughfare ?? "";
                    string ward = place.SubLocality ?? "";
                    string city = place.Locality ?? "";

                    var parts = new List<string> { street, ward, city }
                        .Where(s => !string.IsNullOrWhiteSpace(s));

                    LocationLabel.Text = string.Join(", ", parts);
                }
            }
        }
        catch
        {
            LocationLabel.Text = "Lỗi GPS";
        }
    }
}
