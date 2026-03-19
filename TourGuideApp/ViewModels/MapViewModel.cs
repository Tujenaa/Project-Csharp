using System.Collections.ObjectModel;
using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.ViewModels; // for HistoryStore

public class MapViewModel
{
    public Mapsui.Map Map { get; set; }

    // Command to play audio from UI
    public Command<POI> PlayAudioCommand { get; }

    readonly AudioService audioService = new();

    public ObservableCollection<POI> NearbyPOI { get; set; } = new();

    readonly MapService mapService = new();
    readonly LocationService locationService = new();
    readonly ApiService apiService = new();

    // Anti-spam guard
    bool isPlaying = false;

    public MapViewModel()
    {
        Map = mapService.CreateMap();

        LoadMap();

        PlayAudioCommand = new Command<POI>(poi =>
        {
            PlayPOIManually(poi);
        });

        _ = TrackUserLocation();
    }

    /// Load POI list, show markers and draw route
    async Task LoadMap()
    {
        var routePoints = new List<(double lon, double lat)>();

        var pois = await apiService.GetPOI();

        if (pois == null) return;

        foreach (var poi in pois)
        {
            NearbyPOI.Add(poi);
            mapService.AddMarker(Map, poi);
            routePoints.Add((poi.Longitude, poi.Latitude));
        }

        mapService.DrawRoute(Map, routePoints);
    }

    // Add current location marker for user
    public void AddCurrentLocationMarker(double lon, double lat)
    {
        mapService.AddCurrentLocationMarker(Map, lon, lat);
    }

    // Haversine distance in metres
    double GetDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double R = 6371e3;
        double φ1 = lat1 * Math.PI / 180;
        double φ2 = lat2 * Math.PI / 180;
        double Δφ = (lat2 - lat1) * Math.PI / 180;
        double Δλ = (lon2 - lon1) * Math.PI / 180;

        double a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                   Math.Cos(φ1) * Math.Cos(φ2) *
                   Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    // Continuously track user location (every 5 s)
    async Task TrackUserLocation()
    {
        while (true)
        {
            var location = await locationService.GetCurrentLocationAsync();

            if (location != null)
            {
                CheckNearbyPOI(location.Latitude, location.Longitude);
            }

            await Task.Delay(5000);
        }
    }

    // Check nearest POI and auto-play audio
    void CheckNearbyPOI(double lat, double lon)
    {
        foreach (var poi in NearbyPOI)
        {
            var distance = GetDistance(lat, lon, poi.Latitude, poi.Longitude);

            if (distance < poi.Radius)
            {
                PlayPOIAudio(poi);
                break;
            }
        }
    }

    // Play POI audio (with anti-spam)
    async void PlayPOIAudio(POI poi)
    {
        if (isPlaying) return;
        if (poi == null) return;

        isPlaying = true;

        try
        {
            if (!string.IsNullOrEmpty(poi.AudioUrl))
            {
                await audioService.PlayAudio(poi.AudioUrl);
            }

            if (poi.Id > 0)
            {
                // Save to remote API (existing behaviour)
                await apiService.SaveHistory(poi.Id);

                // Also record locally for HistoryPage (new – no logic change)
                HistoryStore.Add(poi);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        await Task.Delay(10000);

        isPlaying = false;
    }

    /// Play audio when user taps manually
    public void PlayPOIManually(POI poi)
    {
        PlayPOIAudio(poi);
    }
}
