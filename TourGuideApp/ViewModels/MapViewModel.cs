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

    // Ngôn ngữ đang được load trong ttsService
    // Dùng để phát hiện khi user đổi ngôn ngữ giữa chừng
    string _currentLoadedLang = "";

    // Cache bản dịch: tránh dịch lại khi resume (key = poiId_lang)
    readonly Dictionary<string, string> translationCache = new();

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action? POIUpdated;

    // ── Commands ──────────────────────────────────────────────────────────────
    public Command<POI> PlayAudioCommand { get; }
    public Command<POI> GoToDetailCommand { get; }

    /// Được gán lại từ MapPage (để xử lý vẽ đường trên UI thread)
    public Command<POI> SelectRouteDestCommand { get; set; }

    public Func<string, Task<string?>>? EvalJs { get; set; }

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

        // Default no-op; MapPage sẽ gán lại trong constructor
        SelectRouteDestCommand = new Command<POI>(_ => { });

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

    // ── Tìm kiếm POI theo tên ────────────────────────────────────────────────
    public List<POI> SearchPOI(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<POI>();

        var q = query.Trim().ToLowerInvariant();
        return allPOIs
            .Where(p => p.Name.ToLowerInvariant().Contains(q))
            .OrderBy(p => userLat.HasValue && userLon.HasValue
                ? DistanceHelper.GetDistance(userLat.Value, userLon.Value, p.Latitude, p.Longitude)
                : 0)
            .ToList();
    }

    // ── Cập nhật khoảng cách ─────────────────────────────────────────────────
    public void RefreshDistances()
    {
        if (!userLat.HasValue || !userLon.HasValue) return;
        foreach (var poi in NearbyPOI)
            poi.DistanceText = FormatDistance(
                DistanceHelper.GetDistance(userLat.Value, userLon.Value, poi.Latitude, poi.Longitude));
        POIUpdated?.Invoke();
    }

    // ── Play / Pause / Resume ─────────────────────────────────────────────────
    async Task HandlePlayPause(POI poi)
    {
        if (poi == null) return;

        // ── PAUSE: đang phát cùng POI → dừng, ghi nhớ vị trí câu ────────────
        if (currentPlayingPoi?.Id == poi.Id && isPlaying)
        {
            ttsToken?.Cancel();
            isPlaying = false;
            poi.IsPlaying = false;
            POIUpdated?.Invoke(); // sync card icon ngay
            // ttsService đã giữ nguyên _sentenceIndex → resume sẽ tiếp tục đúng chỗ
            return;
        }

        // ── RESUME: cùng POI, đang pause ─────────────────────────────────────
        if (currentPlayingPoi?.Id == poi.Id && !isPlaying)
        {
            string langNow = SettingService.Instance.Language;

            // Nếu ngôn ngữ đổi kể từ lần load → dịch lại, phát từ đầu với ngôn ngữ mới
            if (langNow != _currentLoadedLang)
            {
                string srcText =
                    !string.IsNullOrWhiteSpace(poi.Script) ? poi.Script :
                    !string.IsNullOrWhiteSpace(poi.Description) ? poi.Description :
                    "Không có dữ liệu";

                string freshText = await GetTranslatedTextAsync(poi.Id, srcText, langNow);
                ttsService.LoadText(freshText);
                _currentLoadedLang = langNow;
            }

            isPlaying = true;
            poi.IsPlaying = true;
            POIUpdated?.Invoke(); // sync card icon ngay

            ttsToken = new CancellationTokenSource();
            try
            {
                // Tiếp tục từ _sentenceIndex; nếu IsFinished thì replay từ đầu
                await ttsService.SpeakAsync(ttsToken.Token);
            }
            catch (OperationCanceledException) { }
            catch { }

            isPlaying = false;
            poi.IsPlaying = false;
            POIUpdated?.Invoke(); // sync card icon khi kết thúc
            return;
        }

        // ── PLAY MỚI: POI khác hoặc lần đầu ─────────────────────────────────
        StopAll();

        currentPlayingPoi = poi;
        isPlaying = true;
        poi.IsPlaying = true;
        POIUpdated?.Invoke(); // sync card icon ngay

        // Lấy text gốc
        string originalText =
            !string.IsNullOrWhiteSpace(poi.Script) ? poi.Script :
            !string.IsNullOrWhiteSpace(poi.Description) ? poi.Description :
            "Không có dữ liệu";

        string lang = SettingService.Instance.Language;
        string finalText = await GetTranslatedTextAsync(poi.Id, originalText, lang);

        // Load text mới vào TTS service (reset sentence index)
        ttsService.LoadText(finalText);
        _currentLoadedLang = lang;   // ghi nhớ ngôn ngữ đang được load

        ttsToken = new CancellationTokenSource();
        try
        {
            await ttsService.SpeakAsync(ttsToken.Token);
        }
        catch (OperationCanceledException) { }
        catch { }

        isPlaying = false;
        poi.IsPlaying = false;
        POIUpdated?.Invoke(); // sync card icon khi kết thúc
    }

    /// Lấy text đã dịch; cache theo poiId + lang để không dịch lại khi resume
    async Task<string> GetTranslatedTextAsync(int poiId, string originalText, string lang)
    {
        if (lang == "vi") return originalText;

        string cacheKey = $"{poiId}_{lang}";
        if (translationCache.TryGetValue(cacheKey, out var cached))
            return cached;

        string translated = await translateService.TranslateWithRetryAsync(originalText, lang);
        translationCache[cacheKey] = translated;
        return translated;
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