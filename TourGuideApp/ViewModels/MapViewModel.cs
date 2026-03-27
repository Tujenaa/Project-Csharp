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

    readonly HashSet<int> autoPlayedIds = new();

    public string MapHtml { get; }

    // ── Sự kiện thông báo UI cập nhật ─────────────────────────────────────────
    public event Action? POIUpdated;

    // ── Commands ──────────────────────────────────────────────────────────────
    public Command<POI> PlayAudioCommand { get; }
    public Command<POI> GoToDetailCommand { get; }

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

        var sorted = SortByNearestNeighbor(allPOIs, startLat, startLon);
        UpdateIndexAndDistance(sorted);
        RebuildNearbyPOI(sorted);

        await PushMapDataAsync();
        _ = TrackUserLocationAsync();
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

    // ── Cập nhật khoảng cách (gọi thủ công sau khi lấy vị trí mới) ───────────
    public void RefreshDistances()
    {
        if (!userLat.HasValue || !userLon.HasValue) return;
        foreach (var poi in NearbyPOI)
            poi.DistanceText = FormatDistance(
                DistanceHelper.GetDistance(userLat.Value, userLon.Value, poi.Latitude, poi.Longitude));
        POIUpdated?.Invoke();
    }

    // ── Play / Pause ──────────────────────────────────────────────────────────
    async Task HandlePlayPause(POI poi)
    {
        if (poi == null) return;

        if (currentPlayingPoi?.Id == poi.Id && isPlaying)
        {
            ttsToken?.Cancel();
            isPlaying = false;
            poi.IsPlaying = false;
            currentPlayingPoi = null;
            return;
        }

        StopAll();

        currentPlayingPoi = poi;
        isPlaying = true;
        poi.IsPlaying = true;

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

        isPlaying = false;
        poi.IsPlaying = false;
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

    // ── Auto-play ─────────────────────────────────────────────────────────────
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

            // Cập nhật khoảng cách cho tất cả POI
            foreach (var poi in NearbyPOI)
                poi.DistanceText = FormatDistance(
                    DistanceHelper.GetDistance(loc.Latitude, loc.Longitude, poi.Latitude, poi.Longitude));

            POIUpdated?.Invoke();
            CheckNearbyPOI(loc.Latitude, loc.Longitude);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void RebuildNearbyPOI(IEnumerable<POI> sorted)
    {
        NearbyPOI.Clear();
        foreach (var p in sorted) NearbyPOI.Add(p);
        POIUpdated?.Invoke();
    }

    /// Gán IndexLabel (1, 2, 3…) và DistanceText cho mỗi POI
    void UpdateIndexAndDistance(IList<POI> sorted)
    {
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].IndexLabel = (i + 1).ToString();
            if (userLat.HasValue && userLon.HasValue)
                sorted[i].DistanceText = FormatDistance(
                    DistanceHelper.GetDistance(
                        userLat.Value, userLon.Value,
                        sorted[i].Latitude, sorted[i].Longitude));
        }
    }

    /// Định dạng khoảng cách: < 1000 m hiện "x m", >= 1000 m hiện "x.x km"
    static string FormatDistance(double meters)
    {
        if (meters < 1000)
            return $"{(int)meters} m";
        return $"{(meters / 1000.0):F1} km";
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