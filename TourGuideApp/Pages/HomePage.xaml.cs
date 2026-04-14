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
            if (status == PermissionStatus.Granted)
            {
                // Gọi lại LoadData để VM lấy được location thật sau khi đã có quyền
                await _vm.LoadData();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Permission check error: {ex.Message}");
        }
    }
}
