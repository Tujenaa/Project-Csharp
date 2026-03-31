using System.Linq;
using Microsoft.Maui.ApplicationModel;

using TourGuideApp.Pages;
using TourGuideApp.Services;
using TourGuideApp.Models;

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

    public Command<POI> GoToDetailCommand { get; }


    private void OnPlaceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0)
            return;

        // Chỉ reset selection, không lưu history ở đây (tránh duplicate với TTS)
        if (sender is CollectionView cv)
        {
            cv.SelectedItem = null;
        }
    }

    // Lấy vị trí hiện tại và hiển thị địa chỉ
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
                    string city = place.Locality ?? "";
                    LocationLabel.Text = $"{street}, {city}".TrimStart(',', ' ');
                }
            }
        }
        catch
        {
            LocationLabel.Text = "Lỗi GPS";
        }
    }

}