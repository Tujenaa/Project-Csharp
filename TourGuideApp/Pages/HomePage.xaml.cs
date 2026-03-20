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
    }

    private async void OnMapButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//map");
    }
    private async void OnPlaceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0)
            return;

        var poi = e.CurrentSelection[0] as POI;

 
        var api = new ApiService();
        await api.SaveHistory(poi.Id);

        await DisplayAlert("Địa điểm", poi.Name, "OK");
    }

}