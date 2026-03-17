using MapsuiMap = Mapsui.Map;
using TourGuideApp.Models;
using TourGuideApp.Services;

namespace TourGuideApp.ViewModels;

public class MapViewModel
{
    public Mapsui.Map Map { get; set; }

    readonly MapService mapService = new();
    readonly LocationService locationService = new();

    public MapViewModel()
    {
        Map = mapService.CreateMap();

        LoadMap();
    }

    async void LoadMap()
    {
        var location = await locationService.GetCurrentLocationAsync();

        // danh sách nhà hàng
        var restaurants = new List<Restaurant>
        {
            new Restaurant
            {
                Name = "Phở Hòa Pasteur",
                Latitude = 10.7840,
                Longitude = 106.6992
            },
            new Restaurant
            {
                Name = "Bún Chả Hà Nội 26",
                Latitude = 10.7804,
                Longitude = 106.7008
            },
            new Restaurant
            {
                Name = "Bánh mì Huỳnh Hoa",
                Latitude = 10.7725,
                Longitude = 106.6980
            }
        };

        // marker vị trí người dùng
        if (location != null)
        {
            mapService.AddMarker(Map, location.Longitude, location.Latitude);

            mapService.ZoomToLocation(Map, location.Longitude, location.Latitude);
        }
        else
        {
            // fallback nếu không lấy được GPS
            mapService.ZoomToLocation(Map, restaurants[0].Longitude, restaurants[0].Latitude);
        }

        // marker nhà hàng
        foreach (var r in restaurants)
        {
            mapService.AddMarker(Map, r.Longitude, r.Latitude);
        }

        // vẽ đường đi giữa các nhà hàng
        var route = restaurants
            .Select(r => (r.Longitude, r.Latitude))
            .ToList();

        mapService.DrawRoute(Map, route);
        mapService.ZoomToLocation(Map, 106.6992, 10.7840);
    }

}