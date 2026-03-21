using System.Collections.ObjectModel;
using System.Text.Json;
using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.Utils;

namespace TourGuideApp.ViewModels;

public class MapViewModel
{
    // ── Services ──────────────────────────────────────────────────────────────
    readonly MapService mapService = new();
    readonly LocationService locationService = new();
    readonly ApiService apiService = new();
    readonly AudioService audioService = new();

    // ── Raw list – dùng để re-sort khi đổi điểm đầu ──────────────────────────
    readonly List<POI> allPOIs = new();

    // ── State ─────────────────────────────────────────────────────────────────
    public ObservableCollection<POI> NearbyPOI { get; } = new();

    double? userLat;
    double? userLon;

    public string MapHtml { get; }

    // ── Commands ──────────────────────────────────────────────────────────────
    public Command<POI> PlayAudioCommand { get; }
    public Command<POI> GoToDetailCommand { get; }
    public Command<POI> ChangeDestinationCommand { get; }

    public Func<string, Task<string?>>? EvalJs { get; set; }

    bool isPlaying = false;
    readonly TextToSpeechService ttsService = new();

   

    public MapViewModel()
    {
        MapHtml = mapService.BuildLeafletHtml();

        PlayAudioCommand = new Command<POI>(poi => PlayPOIAudio(poi));

        GoToDetailCommand = new Command<POI>(async poi =>
        {
            if (poi == null) return;
            await Shell.Current.GoToAsync("placeDetail",
                new Dictionary<string, object> { { "poi", poi } });
        });

        // Đổi điểm đầu: re-sort list + xóa đường cũ + vẽ đường mới
        ChangeDestinationCommand = new Command<POI>(async poi =>
        {
            if (poi == null) return;
            await ApplyNewStartPOI(poi);
        });

        _ = InitAsync();
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    async Task InitAsync()
    {
        var locationTask = locationService.GetCurrentLocationAsync();
        var poisTask = apiService.GetPOI();

        await Task.WhenAll(locationTask, poisTask);

        var location = locationTask.Result;
        var pois = poisTask.Result;

        if (location != null)
        {
            userLat = location.Latitude;
            userLon = location.Longitude;
        }

        if (pois == null || pois.Count == 0) return;

        allPOIs.AddRange(pois);

        // Mặc định: sort bắt đầu từ POI gần user nhất
        double startLat = userLat ?? pois[0].Latitude;
        double startLon = userLon ?? pois[0].Longitude;

        RebuildNearbyPOI(SortByNearestNeighbor(allPOIs, startLat, startLon));

        await PushMapDataAsync();
        _ = TrackUserLocationAsync();
    }

    // ── Đổi điểm đầu ─────────────────────────────────────────────────────────

    async Task ApplyNewStartPOI(POI startPoi)
    {
        // Re-sort: startPoi cố định đứng đầu, các POI còn lại nearest-neighbor
        var sorted = SortByNearestNeighbor(allPOIs, startPoi.Latitude, startPoi.Longitude,
                                           forcedFirst: startPoi);
        RebuildNearbyPOI(sorted);

        if (EvalJs == null) return;

        // Xóa tất cả đường + marker cũ, vẽ lại theo thứ tự mới
        await EvalJs("clearRoutes()");

        await EvalJs($"setPOIs({BuildPOIJson(sorted)})");

        // Vẽ đường chỉ đường từ user → startPoi (đường đỏ OSRM)
        if (userLat.HasValue && userLon.HasValue)
        {
            var uLat = userLat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var uLon = userLon.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var dLat = startPoi.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var dLon = startPoi.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await EvalJs($"drawNavigationRoute({uLat},{uLon},{dLat},{dLon})");
        }
    }

    // ── Push data to WebView (lần đầu) ───────────────────────────────────────

    public async Task PushMapDataAsync()
    {
        if (EvalJs == null) return;

        if (userLat.HasValue && userLon.HasValue)
        {
            var lat = userLat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lon = userLon.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await EvalJs($"setUserLocation({lat},{lon})");
            await EvalJs($"flyTo({lat},{lon},14)");
        }

        if (NearbyPOI.Count > 0)
            await EvalJs($"setPOIs({BuildPOIJson(NearbyPOI)})");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void RebuildNearbyPOI(IEnumerable<POI> sorted)
    {
        NearbyPOI.Clear();
        foreach (var p in sorted)
            NearbyPOI.Add(p);
    }

    static string BuildPOIJson(IEnumerable<POI> pois) =>
        JsonSerializer.Serialize(pois.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            latitude = p.Latitude,
            longitude = p.Longitude
        }));

    /// Nearest-neighbor sort.
    /// Nếu <paramref name="forcedFirst"/> != null, POI đó luôn đứng vị trí 0.
    static List<POI> SortByNearestNeighbor(
        IEnumerable<POI> source,
        double startLat, double startLon,
        POI? forcedFirst = null)
    {
        var remaining = new List<POI>(source);
        var sorted = new List<POI>(remaining.Count);

        if (forcedFirst != null)
        {
            var match = remaining.FirstOrDefault(p => p.Id == forcedFirst.Id);
            if (match != null)
            {
                sorted.Add(match);
                remaining.Remove(match);
                startLat = match.Latitude;
                startLon = match.Longitude;
            }
        }

        double curLat = startLat, curLon = startLon;

        while (remaining.Count > 0)
        {
            var nearest = remaining
                .OrderBy(p => DistanceHelper.GetDistance(curLat, curLon, p.Latitude, p.Longitude))
                .First();

            sorted.Add(nearest);
            remaining.Remove(nearest);
            curLat = nearest.Latitude;
            curLon = nearest.Longitude;
        }

        return sorted;
    }

    // ── Location tracking ─────────────────────────────────────────────────────

    async Task TrackUserLocationAsync()
    {
        while (true)
        {
            await Task.Delay(5000);
            var loc = await locationService.GetCurrentLocationAsync();
            if (loc == null) continue;

            userLat = loc.Latitude;
            userLon = loc.Longitude;

            if (EvalJs != null)
            {
                var lat = loc.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var lon = loc.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await EvalJs($"setUserLocation({lat},{lon})");
            }

            CheckNearbyPOI(loc.Latitude, loc.Longitude);
        }
    }

    void CheckNearbyPOI(double lat, double lon)
    {
        foreach (var poi in NearbyPOI)
        {
            if (DistanceHelper.GetDistance(lat, lon, poi.Latitude, poi.Longitude) < poi.Radius)
            {
                PlayPOIAudio(poi);
                break;
            }
        }
    }

    // ── Audio ─────────────────────────────────────────────────────────────────

    CancellationTokenSource? ttsToken;

    async void PlayPOIAudio(POI poi)
    {
        if (isPlaying || poi == null) return;
        isPlaying = true;

        try
        {
            if (!string.IsNullOrEmpty(poi.AudioUrl))
            {
                await audioService.PlayAudio(poi.AudioUrl);
            }
            else
            {
                string text = poi.Script ?? poi.Description ?? "Không có dữ liệu";

                ttsToken?.Cancel();
                ttsToken = new CancellationTokenSource();

                await TextToSpeech.SpeakAsync(text, cancelToken: ttsToken.Token);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        await Task.Delay(5000);
        isPlaying = false;
    }

    public void PlayPOIManually(POI poi) => PlayPOIAudio(poi);

    public async Task<(double Lat, double Lon)?> GetCurrentLocationFastAsync()
    {
        var loc = await locationService.GetCurrentLocationAsync();
        if (loc == null) return null;
        userLat = loc.Latitude;
        userLon = loc.Longitude;
        return (loc.Latitude, loc.Longitude);
    }
}