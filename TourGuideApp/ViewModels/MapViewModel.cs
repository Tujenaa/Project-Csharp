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

    private Tour? _currentTour;
    public Tour? CurrentTour
    {
        get => _currentTour;
        set { _currentTour = value; OnPropertyChanged(nameof(CurrentTour)); }
    }

    private string _currentDestinationName = string.Empty;
    public string CurrentDestinationName
    {
        get => string.IsNullOrEmpty(_currentDestinationName) ? LocalizationService.Get("select_destination_placeholder") : _currentDestinationName;
        set { _currentDestinationName = value; OnPropertyChanged(nameof(CurrentDestinationName)); }
    }

    public bool IsTourSelected => ActiveTour != null;
    public event Action? ActiveTourChanged;
    public event Action<Tour?>? TourSelected;

    double? userLat;
    double? userLon;
    double? userCourse; // Lần cập nhật hướng di chuyển gần nhất
 
    private readonly Dictionary<int, DateTime> _poiCooldowns = new();
    private const int AutoPlayCooldownMinutes = 5;
    private readonly HashSet<int> _promptedCooldownPoiIds = new();
 
    private bool _isLastPoiAudioPending = false;
    private bool _hasPromptedOnStartup = false;
    private bool _isPromptActive = false;
    private int _lastNearestId = -1;

    private const int AutoPlayDelaySeconds = 5;
    private readonly Dictionary<int, CancellationTokenSource> _pendingAutoPlay = new();


    private bool _isTourActive;
    public bool IsTourActive
    {
        get => _isTourActive;
        set { _isTourActive = value; OnPropertyChanged(nameof(IsTourActive)); }
    }

    private bool _isNavigationActive;
    public bool IsNavigationActive
    {
        get => _isNavigationActive;
        set { _isNavigationActive = value; OnPropertyChanged(nameof(IsNavigationActive)); }
    }

    public string MapHtml { get; }
    private string? _currentTourPointsJson; // Lưu tọa độ tour hiện tại [[lon,lat], ...]
    private List<POI> _currentTourSequence = new(); // Thứ tự POI trong tour thực tế đang đi



    // ── Events ────────────────────────────────────────────────────────────────
    public event Action? POIUpdated;

    // ── Commands ──────────────────────────────────────────────────────────────
    public Command<POI> PlayAudioCommand { get; }
    public Command<POI> PauseAudioCommand { get; }
    public Command<POI> GoToDetailCommand { get; }
    public Command<POI> SelectRouteDestCommand { get; set; }
    public Command<Tour?> SelectTourCommand { get; }
    public Command StartTourCommand { get; }
    public Command CancelTourCommand { get; }
    public Command StopAudioCommand { get; }
    public Command<POI> ShowPOIOnMapCommand { get; set; }

    public Func<string, Task<string?>>? EvalJs { get; set; }

    public MapViewModel()
    {
        MapHtml = mapService.BuildLeafletHtml();

        PlayAudioCommand = new Command<POI>(poi => _ = AudioPlaybackService.Instance.PlayAsync(poi));
        PauseAudioCommand = new Command<POI>(poi => AudioPlaybackService.Instance.Pause());
        StopAudioCommand = new Command(() => _ = AudioPlaybackService.Instance.StopAsync());

        GoToDetailCommand = new Command<POI>(async poi =>
        {
            if (poi == null) return;
            await Shell.Current.GoToAsync("placeDetail",
                new Dictionary<string, object> { { "poi", poi } });
        });

        SelectRouteDestCommand = new Command<POI>(_ => { });
        ShowPOIOnMapCommand = new Command<POI>(_ => { });

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
                // Chỉ xóa đường đi nếu KHÔNG có tour nào đang chạy
                if (!IsTourActive) await EvalJs("clearDirections()");
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

        StartTourCommand = new Command(async () =>
        {
            if (ActiveTour == null || !userLat.HasValue || !userLon.HasValue) return;

            CurrentTour = ActiveTour;
            IsTourActive = true;

            // 1. Tìm POI gần nhất trong tour
            var tourPois = (CurrentTour.POIs ?? new List<POI>()).Where(p => p.IsApprovedInTour).ToList();
            if (tourPois.Count == 0) return;

            var nearest = tourPois
                .OrderBy(p => DistanceUtils.GetDistance(userLat.Value, userLon.Value, p.Latitude, p.Longitude))
                .First();

            // 2. Sắp xếp lại thứ tự theo khoảng cách tối ưu (Nearest Neighbor)
            _currentTourSequence = SortByNearestNeighbor(tourPois, userLat.Value, userLon.Value);

            // Cập nhật điểm đến đầu tiên
            CurrentDestinationName = _currentTourSequence.FirstOrDefault()?.Name ?? string.Empty;

            // 3. Chuẩn bị danh sách tọa độ cho JS (Bao gồm vị trí User đầu tiên)
            var routingPoints = new List<object> { new[] { userLon.Value, userLat.Value } };
            foreach (var p in _currentTourSequence)
            {
                routingPoints.Add(new[] { p.Longitude, p.Latitude });
            }
            _currentTourPointsJson = JsonSerializer.Serialize(routingPoints);

            // 4. Vẽ đường trên bản đồ
            if (EvalJs != null)
            {
                await EvalJs($"drawTourRoute({_currentTourPointsJson})");
            }

            // Thông báo bắt đầu tour
            MainThread.BeginInvokeOnMainThread(async () => {
                await Application.Current.MainPage.DisplayAlert(
                    LocalizationService.Get("tour_start_title"),
                    LocalizationService.Get("tour_start_msg", CurrentTour.Name), 
                    LocalizationService.Get("ok"));
            });

            RefreshNearbyOrder();
        });

        CancelTourCommand = new Command(() =>
        {
            IsTourActive = false;
            CurrentTour = null;
            CurrentDestinationName = string.Empty;
            _isLastPoiAudioPending = false;
            ResetAutoPlayState();
            if (EvalJs != null) _ = EvalJs("clearDirections()");
            RefreshNearbyOrder();
        });





        _ = InitAsync();

        // Làm mới bản dịch khi đổi ngôn ngữ
        LocalizationDataManager.Instance.PropertyChanged += (s, e) => 
        {
            OnPropertyChanged(string.Empty);
            // RefreshNearbyOrder để cập nhật các nhãn khoảng cách nếu cần
            RefreshNearbyOrder();
        };

        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    private void OnPlaybackStateChanged()
    {
        // Cập nhật Cooldown khi bắt đầu phát một POI mới
        if (AudioPlaybackService.Instance.IsPlaying && AudioPlaybackService.Instance.CurrentPlayingPoi != null)
        {
            _poiCooldowns[AudioPlaybackService.Instance.CurrentPlayingPoi.Id] = DateTime.Now;
        }

        if (!AudioPlaybackService.Instance.IsPlaying && _isLastPoiAudioPending)
        {
            _isLastPoiAudioPending = false;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Application.Current.MainPage.DisplayAlert(
                    LocalizationService.Get("tour_completed_title"),
                    LocalizationService.Get("tour_completed_msg"),
                    LocalizationService.Get("ok")
                );
                CancelTourCommand.Execute(null);
            });
        }
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

        if (tours != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AllTours.Clear();
                foreach (var t in tours) AllTours.Add(t);
            });
        }

        allPOIs.Clear();
        if (pois != null) allPOIs.AddRange(pois);

        if (pois != null && tours != null)
        {
            var tourPoiIds = tours.SelectMany(t => t.POIs ?? new List<POI>()).Select(p => p.Id).ToHashSet();
            foreach (var p in allPOIs)
            {
                p.IsInAnyTour = tourPoiIds.Contains(p.Id);
            }
        }

        if (pois == null || pois.Count == 0) return;

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

    public void RefreshNearbyOrder()
    {
        // LỌC:
        // - Nếu có Tour: Dùng IsApprovedInTour (Check cả trạng thái POI và trạng thái trong Tour)
        // - Nếu không có Tour: Dùng IsReady (Check trạng thái POI và audio)
        var sourceList = (ActiveTour != null)
            ? (ActiveTour.POIs ?? new List<POI>()).Where(p => p.IsApprovedInTour).ToList()
            : allPOIs.Where(p => p.IsReady).ToList();

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

    private void UpdateOnlyDistances()
    {
        if (!userLat.HasValue || !userLon.HasValue || _isPromptActive) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            POI? nearest = null;
            double minDist = double.MaxValue;
 
            foreach (var p in NearbyPOI)
            {
                double d = DistanceUtils.UpdatePoiDistance(p, userLat.Value, userLon.Value);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = p;
                }
            }
 
            foreach (var p in NearbyPOI)
            {
                p.IsNearest = (p == nearest);
            }
 
            if (nearest != null && nearest.Id != _lastNearestId && EvalJs != null)
            {
                _lastNearestId = nearest.Id;
                _ = EvalJs($"highlightNearest({nearest.Id})");
            }
        });
    }

    // ── Tìm kiếm POI ─────────────────────────────────────────────────────────
    public List<POI> SearchPOI(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<POI>();

        var q = query.Trim().ToLowerInvariant();
        return allPOIs
            .Where(p => p.IsReady && p.Name != null && p.Name.ToLowerInvariant().Contains(q))
            .OrderBy(p => userLat.HasValue && userLon.HasValue
                ? DistanceUtils.GetDistance(userLat.Value, userLon.Value, p.Latitude, p.Longitude)
                : 0)
            .ToList();
    }

    // ── Cập nhật khoảng cách ─────────────────────────────────────────────────

    public void ResetStartupFlag() => _hasPromptedOnStartup = false;


    // ── Auto-play ─────────────────────────────────────────────────────────────
    private void CheckNearbyPOI(double lat, double lon)
    {
        if (_isPromptActive) return;
        
        var candidates = new List<(POI Poi, double AngleDiff)>();
        
        // 1. Phân loại theo Tour (nếu đang đi Tour)
        var sourceList = IsTourActive ? _currentTourSequence : allPOIs.Where(p => p.IsReady);

        foreach (var poi in sourceList)
        {
            double dist = DistanceUtils.GetDistance(lat, lon, poi.Latitude, poi.Longitude);
            
            // Xóa đánh dấu đã hỏi "nghe lại" nếu đi xa ra khỏi POI
            if (dist > poi.Radius * 2 && _promptedCooldownPoiIds.Contains(poi.Id))
            {
                _promptedCooldownPoiIds.Remove(poi.Id);
            }

            // Logic Cooldown: Kiểm tra xem POI đã được phát trong vòng X phút chưa
            bool isCoolingDown = false;
            if (_poiCooldowns.TryGetValue(poi.Id, out var lastPlayed))
            {
                if (DateTime.Now - lastPlayed < TimeSpan.FromMinutes(AutoPlayCooldownMinutes))
                {
                    isCoolingDown = true;
                }
            }

            double angleDiff = 0;
            if (userCourse.HasValue)
            {
                double bearing = DistanceUtils.GetBearing(lat, lon, poi.Latitude, poi.Longitude);
                angleDiff = DistanceUtils.GetAngleDifference(userCourse.Value, bearing);
            }

            if (dist < poi.Radius)
            {
                if (!isCoolingDown)
                {
                    // Chỉ tự động phát nếu POI nằm trong góc quan sát phía trước mặt (45 độ mỗi bên)
                    if (userCourse.HasValue && angleDiff > 45) continue;

                    if (!_pendingAutoPlay.ContainsKey(poi.Id))
                        candidates.Add((poi, angleDiff));
                }
                else if (!_promptedCooldownPoiIds.Contains(poi.Id) && !_isPromptActive)
                {
                    // Trường hợp trong cooldown: Hỏi người dùng có muốn nghe lại không
                    ProcessCooldownPrompt(poi);
                }
            }
            else
            {
                // Nếu đi ra ngoài bán kính trong lúc đang chờ phát -> Hủy task chờ
                if (_pendingAutoPlay.TryGetValue(poi.Id, out var cts))
                {
                    cts.Cancel();
                    _pendingAutoPlay.Remove(poi.Id);
                    System.Diagnostics.Debug.WriteLine($"[MapViewModel] Cancelled pending audio for {poi.Name} (left radius).");
                }
            }
        }

        // 4. Sắp xếp candidates theo độ lệch góc (góc nhỏ nhất đứng trước)
        var sortedCandidates = candidates.OrderBy(c => c.AngleDiff).Select(c => c.Poi).ToList();

        foreach (var poi in sortedCandidates)
        {
            // Nếu là startup -> Giữ nguyên logic prompt
            if (!_hasPromptedOnStartup)
            {
                ProcessStartupPrompt(sortedCandidates);
                break; // Startup prompt chỉ xử lý 1 lần
            }

            // Nếu không phải startup -> Chạy logic trì hoãn
            if (SettingService.Instance.AutoPlay)
            {
                StartDelayedAutoPlay(poi);
            }
        }
    }

    private void ProcessStartupPrompt(List<POI> inRange)
    {
        _isPromptActive = true;
        _hasPromptedOnStartup = true;
        var first = inRange[0];
        
        // Đánh dấu cooldown cho toàn bộ các điểm trong vùng để không nhắc lại ngay
        foreach (var p in inRange) 
            _poiCooldowns[p.Id] = DateTime.Now;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                bool confirm = await Application.Current.MainPage.DisplayAlert(
                    LocalizationService.Get("autoplay_title"),
                    LocalizationService.Get("autoplay_msg", first.Name), 
                    LocalizationService.Get("start_now"), 
                    LocalizationService.Get("skip"));

                if (confirm && SettingService.Instance.AutoPlay)
                {
                    await AudioPlaybackService.Instance.EnqueueRangeAsync(inRange);
                }
            }
            finally
            {
                _isPromptActive = false;
            }
        });
    }

    private void StartDelayedAutoPlay(POI poi)
    {
        var cts = new CancellationTokenSource();
        _pendingAutoPlay[poi.Id] = cts;
        var token = cts.Token;

        Task.Run(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[MapViewModel] Entered {poi.Name}. Waiting {AutoPlayDelaySeconds}s...");
                await Task.Delay(AutoPlayDelaySeconds * 1000, token);

                if (!token.IsCancellationRequested)
                {
                    _poiCooldowns[poi.Id] = DateTime.Now;
                    _pendingAutoPlay.Remove(poi.Id);

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        // Cập nhật tiến độ tour (nếu cần)
                        if (IsTourActive && _currentTourSequence.Count > 0 && _currentTourPointsJson != null)
                        {
                            int idx = _currentTourSequence.FindIndex(x => x.Id == poi.Id);
                            if (idx != -1 && EvalJs != null)
                            {
                                _ = EvalJs($"updateProgress({idx + 1}, '{_currentTourPointsJson}')");

                                // Cập nhật tên điểm đến kế tiếp
                                if (idx + 1 < _currentTourSequence.Count)
                                {
                                    CurrentDestinationName = _currentTourSequence[idx + 1].Name ?? string.Empty;
                                }
                                else
                                {
                                    // Sắp đến điểm cuối -> đánh dấu để báo hoàn thành sau khi nghe xong
                                    CurrentDestinationName = LocalizationService.Get("tour_completed_label");
                                    _isLastPoiAudioPending = true;
                                }
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"[MapViewModel] Delay finished for {poi.Name}. Starting audio.");
                        await AudioPlaybackService.Instance.EnqueueAsync(poi);
                    });
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _pendingAutoPlay.Remove(poi.Id);
            }
        });
    }


    private void ResetAutoPlayState()
    {
        _ = AudioPlaybackService.Instance.StopAsync();
        foreach (var cts in _pendingAutoPlay.Values) cts.Cancel();
        _pendingAutoPlay.Clear();
        _promptedCooldownPoiIds.Clear();
    }

    private void ProcessCooldownPrompt(POI poi)
    {
        _isPromptActive = true;
        _promptedCooldownPoiIds.Add(poi.Id);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                bool confirm = await Application.Current.MainPage.DisplayAlert(
                    LocalizationService.Get("re_listen_title"),
                    LocalizationService.Get("re_listen_msg", poi.Name),
                    LocalizationService.Get("re_listen_confirm"),
                    LocalizationService.Get("skip"));

                if (confirm)
                {
                    await AudioPlaybackService.Instance.EnqueueAsync(poi);
                }
            }
            finally
            {
                _isPromptActive = false;
            }
        });
    }

    // ── Location tracking ─────────────────────────────────────────────────────
    async Task TrackUserLocationAsync()
    {
        while (true)
        {
            if (!AuthService.IsLoggedIn)
            {
                ResetAutoPlayState();
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
                // Cập nhật hướng di chuyển (Course) nếu có dữ liệu hợp lệ
                if (loc.Course.HasValue && loc.Speed.HasValue && loc.Speed.Value > 0.5)
                {
                    userCourse = loc.Course.Value;
                }

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

                UpdateOnlyDistances();
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


    public async Task RerouteTourToPOI(POI target)
    {
        if (ActiveTour == null || !userLat.HasValue || !userLon.HasValue) return;

        IsTourActive = true;

        var tourPois = (ActiveTour.POIs ?? new List<POI>()).Where(p => p.IsApprovedInTour).ToList();
        if (tourPois.Count == 0) return;

        // Sắp xếp lại: User -> Target -> Nearest Neighbor của các điểm còn lại
        _currentTourSequence = SortByNearestNeighbor(tourPois, userLat.Value, userLon.Value, target);

        // Chuẩn bị danh sách tọa độ cho JS
        var routingPoints = new List<object> { new[] { userLon.Value, userLat.Value } };
        foreach (var p in _currentTourSequence)
        {
            routingPoints.Add(new[] { p.Longitude, p.Latitude });
        }
        _currentTourPointsJson = JsonSerializer.Serialize(routingPoints);

        if (EvalJs != null)
        {
            await EvalJs($"drawTourRoute({_currentTourPointsJson})");
        }
        
        RefreshNearbyOrder();
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