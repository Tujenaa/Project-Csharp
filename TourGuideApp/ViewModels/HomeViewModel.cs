using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.ViewModels;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly ApiService _poiService = new();
    public ObservableCollection<POI> TopPOIs { get; set; } = new();
    public ObservableCollection<POI> AllPOIs { get; set; } = new();

    public ICommand GoToDetailCommand { get; }
    public Command<POI> PlayAudioCommand { get; }
    public Command<POI> PauseAudioCommand { get; }

    readonly TextToSpeechService ttsService = new();
    
    // ── GPS & Distance ────────────────────────────────────────────────────────
    private string _nearestPoiName = "Đang tìm...";
    public string NearestPoiName
    {
        get => _nearestPoiName;
        set { _nearestPoiName = value; OnPropertyChanged(nameof(NearestPoiName)); }
    }

    private string _nearestPoiDist = "-- m";
    public string NearestPoiDist
    {
        get => _nearestPoiDist;
        set { _nearestPoiDist = value; OnPropertyChanged(nameof(NearestPoiDist)); }
    }

    // ── TTS state ─────────────────────────────────────────────────────────────
    POI? currentPlayingPoi;
    bool isPlaying = false;
    CancellationTokenSource? ttsToken;

    string _currentLoadedLang = "";

    public HomeViewModel()
    {
        GoToDetailCommand = new Command<POI>(async (poi) =>
        {
            if (poi == null) return;
            await Shell.Current.GoToAsync("placeDetail", new Dictionary<string, object>
            {
                { "poi", poi }
            });
        });

        PlayAudioCommand = new Command<POI>(async (poi) =>
        {
            await HandlePlay(poi);
        });

        PauseAudioCommand = new Command<POI>((poi) =>
        {
            HandlePause(poi);
        });

        // Đăng ký event ghi lịch sử khi TTS phát xong
        ttsService.OnFinished += OnTtsFinished;
        ttsService.OnProgress += OnTtsProgress;

        System.Diagnostics.Debug.WriteLine($"HomeViewModel created. Language: {SettingService.Instance.Language}");
    }

    private int _poiCount;
    public int PoiCount
    {
        get => _poiCount;
        set { _poiCount = value; OnPropertyChanged(nameof(PoiCount)); }
    }

    private int _audioCount;
    public int AudioCount
    {
        get => _audioCount;
        set { _audioCount = value; OnPropertyChanged(nameof(AudioCount)); }
    }

    public async Task LoadData()
    {
        var top = await _poiService.GetTopPOI();
        var all = await _poiService.GetPOI();

        PoiCount = await _poiService.GetPoiCount();
        AudioCount = await _poiService.GetAudioCount();

        // Lấy vị trí GPS để tính khoảng cách
        Location? myLoc = null;
        try {
            myLoc = await Geolocation.GetLastKnownLocationAsync() 
                    ?? await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
        } catch { }

        TopPOIs.Clear();
        foreach (var item in top) 
        {
            if (myLoc != null) UpdatePoiDistance(item, myLoc);
            TopPOIs.Add(item);
        }

        AllPOIs.Clear();
        POI? nearest = null;
        double minDist = double.MaxValue;

        foreach (var item in all) 
        {
            if (myLoc != null) 
            {
                var d = UpdatePoiDistance(item, myLoc);
                if (d < minDist) { minDist = d; nearest = item; }
            }
            AllPOIs.Add(item);
        }

        if (nearest != null)
        {
            NearestPoiName = nearest.Name ?? "Chưa rõ";
            NearestPoiDist = nearest.DistanceText;
        }

        System.Diagnostics.Debug.WriteLine($"Loaded {TopPOIs.Count} top POIs and {AllPOIs.Count} all POIs. Nearest: {NearestPoiName}");
    }

    private double UpdatePoiDistance(POI poi, Location myLoc)
    {
        var target = new Location(poi.Latitude, poi.Longitude);
        var dist = myLoc.CalculateDistance(target, DistanceUnits.Kilometers) * 1000; // mét
        poi.DistanceText = dist < 1000 ? $"{(int)dist}m" : $"{(dist/1000.0):F1}km";
        return dist;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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
        System.Diagnostics.Debug.WriteLine($"[Home] TTS finished → saving history for {poi.Name}");

        // Ghi lịch sử local và tự động gọi API trong AddAsync
        await HistoryStore.AddAsync(poi);
    }

    // ── Nút Tạm Dừng ──────────────────────────────────────────────────────────

    void HandlePause(POI poi)
    {
        if (poi == null) return;
        if (currentPlayingPoi?.Id == poi.Id && isPlaying)
        {
            ttsToken?.Cancel();
            isPlaying = false;
            poi.IsPlaying = false;
        }
    }

    // ── Phát & Tiếp Tục (Play / Resume) ───────────────────────────────────────

    async Task HandlePlay(POI poi)
    {
        if (poi == null) return;

        // Nếu đang phát cùng POI thì không làm gì
        if (currentPlayingPoi?.Id == poi.Id && isPlaying) return;

        string currentLang = SettingService.Instance.Language;

        // ── RESUME: cùng POI, đang pause ─────────────────────────────────────
        if (currentPlayingPoi?.Id == poi.Id && !isPlaying)
        {
            if (currentLang != _currentLoadedLang)
            {
                string freshText = GetScriptForLang(poi, currentLang);
                ttsService.LoadText(freshText);
                _currentLoadedLang = currentLang;
            }

            isPlaying = true;
            poi.IsPlaying = true;

            ttsToken = new CancellationTokenSource();
            try
            {
                await ttsService.SpeakAsync(ttsToken.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RESUME ERROR: {ex.Message}");
            }

            isPlaying = false;
            poi.IsPlaying = false;
            return;
        }

        // ── PLAY MỚI: POI khác hoặc lần đầu ─────────────────────────────────
        StopAll();

        currentPlayingPoi = poi;
        isPlaying = true;
        poi.IsPlaying = true;

        string finalText = GetScriptForLang(poi, currentLang);

        ttsService.LoadText(finalText);
        _currentLoadedLang = currentLang;

        ttsToken = new CancellationTokenSource();
        try
        {
            await ttsService.SpeakAsync(ttsToken.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TTS ERROR: {ex.Message}");
        }

        isPlaying = false;
        poi.IsPlaying = false;
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
        foreach (var item in TopPOIs) item.IsPlaying = false;
        foreach (var item in AllPOIs) item.IsPlaying = false;
        isPlaying = false;
        currentPlayingPoi = null;
    }
}