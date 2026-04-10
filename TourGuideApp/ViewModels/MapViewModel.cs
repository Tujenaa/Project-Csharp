using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.Utils;

namespace TourGuideApp.ViewModels;

public class MapViewModel : INotifyPropertyChanged
{
    // ── Services ──────────────────────────────────────────────────────────────
    readonly MapService mapService = new();
    readonly LocationService locationService = new();
    readonly ApiService apiService = new();

    // ── State ─────────────────────────────────────────────────────────────────
    readonly List<POI> allPOIs = new();
    public ObservableCollection<POI> NearbyPOI { get; } = new();

    // Tour filtering
    public ObservableCollection<Tour> AllTours { get; } = new();
    private Tour? _activeTour;
    public Tour? ActiveTour
    {
        get => _activeTour;
        set 
        { 
            _activeTour = value; 
            OnPropertyChanged(nameof(ActiveTour));
            OnPropertyChanged(nameof(IsTourSelected));
            ActiveTourChanged?.Invoke(); 
        }
    }
    public bool IsTourSelected => ActiveTour != null;
    public event Action? ActiveTourChanged;
    public event Action<Tour?>? TourSelected;

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
    public Command<Tour?> SelectTourCommand { get; }
    public Command StartTourCommand { get; }

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

        SelectTourCommand = new Command<Tour?>(async (tour) =>
        {
            ActiveTour = tour;
            TourSelected?.Invoke(tour);

            // Refill NearbyPOI based on the new selection
            RefreshNearbyOrder();

            if (EvalJs == null) return;

            if (tour == null)
            {
                // Hiện tất cả POI
                await EvalJs($"setPOIs({BuildPOIJson(NearbyPOI)})");
            }
            else
            {
                // Chỉ hiện POI trong tour
                var tourPois = tour.POIs ?? new List<POI>();
                await EvalJs($"setPOIs({BuildPOIJson(tourPois)})");

                // Fly tới POI đầu tiên của tour
                var first = tourPois.FirstOrDefault();
                if (first != null)
                {
                    var lat = first.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var lon = first.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    await EvalJs($"flyTo({lat},{lon},15)");
                }
            }
        });

        StartTourCommand = new Command(() =>
        {
            if (ActiveTour == null) return;
            
            // Thông báo bắt đầu tour (có thể dùng event để View hiện Toast)
            MainThread.BeginInvokeOnMainThread(async () => {
                await Application.Current.MainPage.DisplayAlert("Bắt đầu tham quan", 
                    $"Chào mừng bạn đến với tour: {ActiveTour.Name}. GPS sẽ tự động thuyết minh các địa điểm trong hành trình này.", "OK");
            });

            // Tự động cuộn danh sách lên đầu và highlight điểm đầu tiên nếu cần
            RefreshNearbyOrder();
        });

        _ = InitAsync();
    }

    // ── Property Changed ──────────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
        var toursTask = apiService.GetTours();
        await Task.WhenAll(locationTask, poisTask, toursTask);

        var location = locationTask.Result;
        var pois = poisTask.Result;
        var tours = toursTask.Result;

        if (location != null)
        {
            userLat = location.Latitude;
            userLon = location.Longitude;
        }

        if (pois == null || pois.Count == 0) return;

        allPOIs.Clear();
        allPOIs.AddRange(pois);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AllTours.Clear();
            foreach (var t in tours) AllTours.Add(t);
        });

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
        // Chọn nguồn dữ liệu: Nếu có tour thì lấy POI tour, không thì lấy tất cả
        var sourceList = ActiveTour != null ? (ActiveTour.POIs ?? new List<POI>()) : allPOIs;
        
        if (sourceList.Count == 0)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                NearbyPOI.Clear();
                POIUpdated?.Invoke();
            });
            return;
        }

        List<POI> sorted;
        if (userLat.HasValue && userLon.HasValue)
        {
            sorted = sourceList
                .OrderBy(p => DistanceUtils.GetDistance(userLat.Value, userLon.Value, p.Latitude, p.Longitude))
                .ToList();
        }
        else
        {
            sorted = sourceList.ToList();
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

    /// <summary>Áp dụng tour đã chọn từ HomePage vào bản đồ.</summary>
    public async Task ApplyTourAsync(Tour tour)
    {
        ActiveTour = tour;
        RefreshNearbyOrder();

        if (EvalJs == null) return;

        var tourPois = tour.POIs ?? new List<POI>();
        await EvalJs($"setPOIs({BuildPOIJson(tourPois)})");

        var first = tourPois.FirstOrDefault();
        if (first != null)
        {
            var lat = first.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lon = first.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await EvalJs($"flyTo({lat},{lon},15)");
        }
    }
}