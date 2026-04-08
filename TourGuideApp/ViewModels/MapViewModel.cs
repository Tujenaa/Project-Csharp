using System.Collections.ObjectModel;
using System.Text.Json;
using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.Utils;
using TourGuideApp.ViewModels;

namespace TourGuideApp.ViewModels;

public class MapViewModel
{
    // ── Services ──────────────────────────────────────────────────────────────
    readonly MapService mapService = new();
    readonly LocationService locationService = new();
    readonly ApiService apiService = new();

    // ── State ─────────────────────────────────────────────────────────────────
    readonly List<POI> allPOIs = new();
    public ObservableCollection<POI> NearbyPOI { get; } = new();

    double? userLat;
    double? userLon;

    readonly HashSet<int> autoPlayedIds = new();

    public string MapHtml { get; }

    // ── TTS state ─────────────────────────────────────────────────────────────
    // ── TTS state ─────────────────────────────────────────────────────────────
    // Logic đã chuyển sang AudioPlaybackService

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action? POIUpdated;

    // ── Commands ──────────────────────────────────────────────────────────────
    public Command<POI> PlayAudioCommand { get; }
    public Command<POI> PauseAudioCommand { get; }
    public Command<POI> GoToDetailCommand { get; }
    public Command<POI> SelectRouteDestCommand { get; set; }

    public Func<string, Task<string?>>? EvalJs { get; set; }

    public MapViewModel()
    {
        MapHtml = mapService.BuildLeafletHtml();

        PlayAudioCommand = new Command<POI>(poi => _ = AudioPlaybackService.Instance.PlayAsync(poi));
        PauseAudioCommand = new Command<POI>(poi => AudioPlaybackService.Instance.Pause());

        GoToDetailCommand = new Command<POI>(async poi =>
        {
            if (poi == null) return;
            await Shell.Current.GoToAsync("placeDetail",
                new Dictionary<string, object> { { "poi", poi } });
        });

        SelectRouteDestCommand = new Command<POI>(_ => { });

        _ = InitAsync();
    }

    // ── TTS callbacks ─────────────────────────────────────────────────────────
    // Logic đã chuyển sang AudioPlaybackService

    // ── Init ──────────────────────────────────────────────────────────────────
    async Task InitAsync()
    {
        // Chờ đến khi người dùng đăng nhập mới khởi tạo
        while (!AuthService.IsLoggedIn)
        {
            await Task.Delay(1500);
        }

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

        allPOIs.Clear();
        allPOIs.AddRange(pois);

        RefreshNearbyOrder();

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

    private void RefreshNearbyOrder()
    {
        if (allPOIs.Count == 0) return;

        List<POI> sorted;
        if (userLat.HasValue && userLon.HasValue)
        {
            sorted = allPOIs
                .OrderBy(p => DistanceUtils.GetDistance(userLat.Value, userLon.Value, p.Latitude, p.Longitude))
                .ToList();
        }
        else
        {
            sorted = allPOIs.ToList();
        }

        // Cập nhật khoảng cách & IndexLabel
        for (int i = 0; i < sorted.Count; i++)
        {
            var p = sorted[i];
            p.IndexLabel = (i + 1).ToString();
            if (userLat.HasValue && userLon.HasValue)
            {
                DistanceUtils.UpdatePoiDistance(p, userLat.Value, userLon.Value);
            }
        }

        // Kiểm tra xem thứ tự Id có thay đổi không để tránh refresh UI quá nhiều
        var currentIds = NearbyPOI.Select(x => x.Id).ToList();
        var newIds = sorted.Select(x => x.Id).ToList();

        if (!currentIds.SequenceEqual(newIds))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                NearbyPOI.Clear();
                foreach (var p in sorted) NearbyPOI.Add(p);
                POIUpdated?.Invoke();
            });
        }
        else
        {
            // Chỉ cần báo cập nhật text (khoảng cách)
            MainThread.BeginInvokeOnMainThread(() => POIUpdated?.Invoke());
        }
    }

    // ── Tìm kiếm POI ─────────────────────────────────────────────────────────
    public List<POI> SearchPOI(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<POI>();

        var q = query.Trim().ToLowerInvariant();
        return allPOIs
            .Where(p => p.Name != null && p.Name.ToLowerInvariant().Contains(q))
            .OrderBy(p => userLat.HasValue && userLon.HasValue
                ? DistanceUtils.GetDistance(userLat.Value, userLon.Value, p.Latitude, p.Longitude)
                : 0)
            .ToList();
    }

    // ── Cập nhật khoảng cách ─────────────────────────────────────────────────
    public void RefreshDistances() => RefreshNearbyOrder();

    // ── Tạm Dừng (Pause) ──────────────────────────────────────────────────────
    // ── Tạm Dừng (Pause) ──────────────────────────────────────────────────────
    public void HandlePause(POI poi) => AudioPlaybackService.Instance.Pause();

    // ── Phát / Tiếp tục (Play / Resume) ───────────────────────────────────────
    public async Task HandlePlay(POI poi) => await AudioPlaybackService.Instance.PlayAsync(poi);

    void StopAll() => AudioPlaybackService.Instance.Stop();

    public void PlayPOIManually(POI poi) => _ = AudioPlaybackService.Instance.PlayAsync(poi);

    // ── Auto-play ─────────────────────────────────────────────────────────────
    void CheckNearbyPOI(double lat, double lon)
    {
        if (!SettingService.Instance.AutoPlay) return;

        POI? closestPoi = null;
        double minDistance = double.MaxValue;

        foreach (var poi in NearbyPOI)
        {
            double dist = DistanceUtils.GetDistance(lat, lon, poi.Latitude, poi.Longitude);
            
            if (dist > poi.Radius * 2 && autoPlayedIds.Contains(poi.Id))
                autoPlayedIds.Remove(poi.Id);

            if (dist < poi.Radius && !autoPlayedIds.Contains(poi.Id) && !AudioPlaybackService.Instance.IsPlaying)
            {
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestPoi = poi;
                }
            }
        }

        if (closestPoi != null)
        {
            autoPlayedIds.Add(closestPoi.Id);
            _ = AudioPlaybackService.Instance.PlayAsync(closestPoi);
        }
    }

    // ── Location tracking ─────────────────────────────────────────────────────
    async Task TrackUserLocationAsync()
    {
        while (true)
        {
            if (!AuthService.IsLoggedIn)
            {
                StopAll();
                await Task.Delay(3000);
                continue;
            }

            if (!SettingService.Instance.GpsEnabled)
            {
                await Task.Delay(3000);
                continue;
            }

            var loc = await locationService.GetCurrentLocationAsync();
            if (loc != null)
            {
                userLat = loc.Latitude;
                userLon = loc.Longitude;

                if (EvalJs != null)
                {
                    try
                    {
                        var lat = loc.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        var lon = loc.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        await EvalJs($"setUserLocation({lat},{lon})");
                    }
                    catch { /* Bỏ qua lỗi nếu WebView không active / disposed */ }
                }

                RefreshNearbyOrder();
                CheckNearbyPOI(loc.Latitude, loc.Longitude);
            }

            await Task.Delay(3000);
        }
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
                .OrderBy(p => DistanceUtils.GetDistance(curLat, curLon, p.Latitude, p.Longitude))
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