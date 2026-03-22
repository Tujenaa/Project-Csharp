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
    readonly TextToSpeechService ttsService = new();

    // ── State ─────────────────────────────────────────────────────────────────
    readonly List<POI> allPOIs = new();
    public ObservableCollection<POI> NearbyPOI { get; } = new();

    double? userLat;
    double? userLon;

    // Track which POIs have been auto-played this session to avoid repeat
    readonly HashSet<int> autoPlayedIds = new();

    public string MapHtml { get; }

    // ── Commands ──────────────────────────────────────────────────────────────
    public Command<POI> PlayAudioCommand { get; }
    public Command<POI> GoToDetailCommand { get; }
    public Command<POI> ChangeDestinationCommand { get; }

    public Func<string, Task<string?>>? EvalJs { get; set; }

    POI? currentPlayingPoi;
    bool isPlaying = false;
    CancellationTokenSource? ttsToken;

    public MapViewModel()
    {
        MapHtml = mapService.BuildLeafletHtml();

        PlayAudioCommand = new Command<POI>(poi => _ = HandlePlayPause(poi));

        GoToDetailCommand = new Command<POI>(async poi =>
        {
            if (poi == null) return;
            await Shell.Current.GoToAsync("placeDetail",
                new Dictionary<string, object> { { "poi", poi } });
        });

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

        double startLat = userLat ?? pois[0].Latitude;
        double startLon = userLon ?? pois[0].Longitude;

        RebuildNearbyPOI(SortByNearestNeighbor(allPOIs, startLat, startLon));

        await PushMapDataAsync();
        _ = TrackUserLocationAsync();
    }

    // ── Đổi điểm đầu ─────────────────────────────────────────────────────────
    async Task ApplyNewStartPOI(POI startPoi)
    {
        var sorted = SortByNearestNeighbor(allPOIs, startPoi.Latitude, startPoi.Longitude,
                                           forcedFirst: startPoi);
        RebuildNearbyPOI(sorted);

        if (EvalJs == null) return;

        await EvalJs("clearRoutes()");
        await EvalJs($"setPOIs({BuildPOIJson(sorted)})");

        if (userLat.HasValue && userLon.HasValue)
        {
            var uLat = userLat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var uLon = userLon.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var dLat = startPoi.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var dLon = startPoi.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await EvalJs($"drawNavigationRoute({uLat},{uLon},{dLat},{dLon})");
        }
    }

    // ── Push data to WebView ──────────────────────────────────────────────────
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

    // ── Play / Pause ──────────────────────────────────────────────────────────
    async Task HandlePlayPause(POI poi)
    {
        if (poi == null) return;

        // Pause current
        if (currentPlayingPoi?.Id == poi.Id && isPlaying)
        {
            ttsToken?.Cancel();
            isPlaying = false;
            poi.IsPlaying = false;
            currentPlayingPoi = null;
            return;
        }

        // Stop previous
        StopAll();

        currentPlayingPoi = poi;
        isPlaying = true;
        poi.IsPlaying = true;  // triggers UI: icon → pause, label → "Đang phát âm thanh"

        try
        {
            string text = !string.IsNullOrWhiteSpace(poi.Script)
                ? poi.Script
                : "Không có dữ liệu";

            ttsToken = new CancellationTokenSource();
            await ttsService.SpeakAsync(text, ttsToken.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine(ex.Message); }

        // FIX: Always reset state after playback ends so UI reverts to Play state
        isPlaying = false;
        poi.IsPlaying = false;  // triggers UI: icon → play, label → "Audio giới thiệu"
        if (currentPlayingPoi?.Id == poi.Id)
            currentPlayingPoi = null;
    }

    void StopAll()
    {
        ttsToken?.Cancel();
        foreach (var p in NearbyPOI) p.IsPlaying = false;
        isPlaying = false;
    }

    public void PlayPOIManually(POI poi) => _ = HandlePlayPause(poi);

    // ── Auto-play: called from location tracking ───────────────────────────────
    void CheckNearbyPOI(double lat, double lon)
    {
        if (!SettingService.Instance.AutoPlay) return;

        foreach (var poi in NearbyPOI)
        {
            double dist = DistanceHelper.GetDistance(lat, lon, poi.Latitude, poi.Longitude);
            if (dist < poi.Radius && !autoPlayedIds.Contains(poi.Id) && !isPlaying)
            {
                autoPlayedIds.Add(poi.Id);
                _ = HandlePlayPause(poi);
                break;
            }

            // Reset auto-play flag when user moves away (> 2× radius)
            if (dist > poi.Radius * 2 && autoPlayedIds.Contains(poi.Id))
                autoPlayedIds.Remove(poi.Id);
        }
    }

    // ── Location tracking ─────────────────────────────────────────────────────
    async Task TrackUserLocationAsync()
    {
        while (true)
        {
            await Task.Delay(5000);

            if (!SettingService.Instance.GpsEnabled) continue;

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

    // ── Helpers ───────────────────────────────────────────────────────────────
    void RebuildNearbyPOI(IEnumerable<POI> sorted)
    {
        NearbyPOI.Clear();
        foreach (var p in sorted) NearbyPOI.Add(p);
    }

    static string BuildPOIJson(IEnumerable<POI> pois) =>
        JsonSerializer.Serialize(pois.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            latitude = p.Latitude,
            longitude = p.Longitude
        }));

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

    public async Task<(double Lat, double Lon)?> GetCurrentLocationFastAsync()
    {
        var loc = await locationService.GetCurrentLocationAsync();
        if (loc == null) return null;
        userLat = loc.Latitude;
        userLon = loc.Longitude;
        return (loc.Latitude, loc.Longitude);
    }
}