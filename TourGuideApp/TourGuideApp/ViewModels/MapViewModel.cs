using System.Collections.ObjectModel;
using TourGuideApp.Models;
using TourGuideApp.Services;

public class MapViewModel
{
    public Mapsui.Map Map { get; set; }

    public ObservableCollection<POI> NearbyPOI { get; set; } = new();

    readonly MapService mapService = new();
    readonly LocationService locationService = new();
    readonly ApiService apiService = new();

    public MapViewModel()
    {
        Map = mapService.CreateMap();
        LoadMap();
    }

    async void LoadMap()
    {
        var location = await locationService.GetCurrentLocationAsync();

        var pois = await apiService.GetPOI();

        foreach (var poi in pois)
        {
            NearbyPOI.Add(poi);

            mapService.AddMarker(Map, poi.Longitude, poi.Latitude);
        }
    }
}