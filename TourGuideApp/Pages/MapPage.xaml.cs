using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class MapPage : ContentPage
{
    MapViewModel viewModel;

    public MapPage()
    {
        InitializeComponent();

        viewModel = new MapViewModel();

        RouteMap.Map = viewModel.Map;
    }

    private async void OnCurrentLocationTapped(object sender, EventArgs e)
    {
        await DisplayAlert("GPS", "Đang lấy vị trí hiện tại", "OK");
    }
}