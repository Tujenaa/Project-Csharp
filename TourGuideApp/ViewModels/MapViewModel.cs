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
    readonly TextToSpeechService ttsService = new();
    readonly TranslateService translateService = new();

    // ── State ─────────────────────────────────────────────────────────────────
    readonly List<POI> allPOIs = new();
    public ObservableCollection<POI> NearbyPOI { get; } = new();

    double? userLat;
    double? userLon;

    readonly HashSet<int> autoPlayedIds = new();

    public string MapHtml { get; }

    // ── TTS state ─────────────────────────────────────────────────────────────
    POI? currentPlayingPoi;
    bool isPlaying = false;
    CancellationTokenSource? ttsToken;

    string _currentLoadedLang = "";

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

        PlayAudioCommand = new Command<POI>(poi => _ = HandlePlay(poi));
        PauseAudioCommand = new Command<POI>(poi => HandlePause(poi));

        GoToDetailCommand = new Command<POI>(async poi =>
        {
            if (poi == null) return;
            await Shell.Current.GoToAsync("placeDetail",
                new Dictionary<string, object> { { "poi", poi } });
        });

        SelectRouteDestCommand = new Command<POI>(_ => { });

        // Đăng ký event ghi lịch sử khi TTS phát xong
        ttsService.OnFinished += OnTtsFinished;
        ttsService.OnProgress += OnTtsProgress;

        _ = InitAsync();
    }

    // ── TTS callbacks ─────────────────────────────────────────────────────────

    void OnTtsProgress(int current, int total)
    {
        if (currentPlayingPoi == null || total == 0) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            currentPlayingPoi.AudioProgress = (double)current / total;
            currentPlayingPoi.AudioDuration = $"{current}/{total} câu";
        });
    }

    async void OnTtsFinished()
    {
        if (currentPlayingPoi == null) return;

        var poi = currentPlayingPoi;
        System.Diagnostics.Debug.WriteLine($"[Map] TTS finished → saving history for {poi.Name}");

        await HistoryStore.AddAsync(poi);

        // Cập nhật UI icon sau khi kết thúc
        MainThread.BeginInvokeOnMainThread(() => POIUpdated?.Invoke());
    }

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
                .OrderBy(p => DistanceHelper.GetDistance(userLat.Value, userLon.Value, p.Latitude, p.Longitude))
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
                double d = DistanceHelper.GetDistance(userLat.Value, userLon.Value, p.Latitude, p.Longitude);
                p.DistanceText = FormatDistance(d);
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
                ? DistanceHelper.GetDistance(userLat.Value, userLon.Value, p.Latitude, p.Longitude)
                : 0)
            .ToList();
    }

    // ── Cập nhật khoảng cách ─────────────────────────────────────────────────
    public void RefreshDistances() => RefreshNearbyOrder();

    // ── Tạm Dừng (Pause) ──────────────────────────────────────────────────────
    void HandlePause(POI poi)
    {
        if (poi == null) return;
        if (currentPlayingPoi?.Id == poi.Id && isPlaying)
        {
            ttsToken?.Cancel();
            isPlaying = false;
            poi.IsPlaying = false;
            POIUpdated?.Invoke();
        }
    }

    // ── Phát / Tiếp tục (Play / Resume) ───────────────────────────────────────
    async Task HandlePlay(POI poi)
    {
        if (poi == null) return;

        if (currentPlayingPoi?.Id == poi.Id && isPlaying) return;

        // ── RESUME ───────────────────────────────────────────────────────────
        if (currentPlayingPoi?.Id == poi.Id && !isPlaying)
        {
            string langNow = SettingService.Instance.Language;

            if (langNow != _currentLoadedLang)
            {
                string freshText = GetScriptForLang(poi, langNow);
                ttsService.LoadText(freshText);
                _currentLoadedLang = langNow;
            }

            isPlaying = true;
            poi.IsPlaying = true;
            POIUpdated?.Invoke();

            ttsToken = new CancellationTokenSource();
            try
            {
                await ttsService.SpeakAsync(ttsToken.Token);
            }
            catch (OperationCanceledException) { }
            catch { }

            isPlaying = false;
            poi.IsPlaying = false;
            POIUpdated?.Invoke();
            return;
        }

        // ── PLAY MỚI ─────────────────────────────────────────────────────────
        StopAll();

        currentPlayingPoi = poi;
        isPlaying = true;
        poi.IsPlaying = true;
        POIUpdated?.Invoke();

        string lang = SettingService.Instance.Language;
        string finalText = GetScriptForLang(poi, lang);

        ttsService.LoadText(finalText);
        _currentLoadedLang = lang;

        ttsToken = new CancellationTokenSource();
        try
        {
            await ttsService.SpeakAsync(ttsToken.Token);
        }
        catch (OperationCanceledException) { }
        catch { }

        isPlaying = false;
        poi.IsPlaying = false;
        POIUpdated?.Invoke();
    }

    private string GetScriptForLang(POI poi, string lang)
    {
        string? script = lang switch
        {
            "en" => poi.ScriptEn,
            "ja" => poi.ScriptJa,
            "zh" => poi.ScriptZh,
            _ => poi.ScriptVi
        };

        if (string.IsNullOrWhiteSpace(script)) 
            script = poi.Description ?? "Không có dữ liệu thuyết minh";
            
        return script;
    }

    void StopAll()
    {
        ttsToken?.Cancel();
        foreach (var p in NearbyPOI) p.IsPlaying = false;
        isPlaying = false;
    }

    public void PlayPOIManually(POI poi) => _ = HandlePlay(poi);

    // ── Auto-play ─────────────────────────────────────────────────────────────
    void CheckNearbyPOI(double lat, double lon)
    {
        if (!SettingService.Instance.AutoPlay) return;

        POI? closestPoi = null;
        double minDistance = double.MaxValue;

        foreach (var poi in NearbyPOI)
        {
            double dist = DistanceHelper.GetDistance(lat, lon, poi.Latitude, poi.Longitude);
            
            if (dist > poi.Radius * 2 && autoPlayedIds.Contains(poi.Id))
                autoPlayedIds.Remove(poi.Id);

            if (dist < poi.Radius && !autoPlayedIds.Contains(poi.Id) && !isPlaying)
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
            _ = HandlePlay(closestPoi);
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


    static string FormatDistance(double meters)
    {
        if (meters < 1000) return $"{(int)meters} m";
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