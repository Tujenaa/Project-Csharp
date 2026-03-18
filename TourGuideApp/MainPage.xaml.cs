using System.Net.Http.Json;
using TourGuideApp.Services;
using TourGuideApp.Models;

namespace TourGuideApp;

public partial class MainPage : ContentPage
{
    readonly ApiService api = new();

    public MainPage()
    {
        InitializeComponent();
        LoadData();
    }

    async void LoadData()
    {
        var pois = await api.GetPOI();

        foreach (var poi in pois)
        {
            Console.WriteLine(poi.Name);
        }
    }
    void OnCounterClicked(object sender, EventArgs e)
    {
        Console.WriteLine("Button clicked");
    }
}