using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TourGuideApp.Models;
using TourGuideApp.Services;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly ApiService _poiService = new();
    public ObservableCollection<POI> TopPOIs { get; set; } = new();
    public ObservableCollection<POI> AllPOIs { get; set; } = new();

    public ICommand GoToDetailCommand { get; }
    public Command<POI> PlayAudioCommand { get; }

    readonly TextToSpeechService ttsService = new();
    readonly TranslateService translateService = new();

    // ── TTS state ─────────────────────────────────────────────────────────────
    POI? currentPlayingPoi;
    bool isPlaying = false;
    CancellationTokenSource? ttsToken;

    // Ngôn ngữ đang được load trong ttsService
    string _currentLoadedLang = "";

    // Cache bản dịch: tránh dịch lại khi resume (key = poiId_lang)
    readonly Dictionary<string, string> translationCache = new();

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
            await HandlePlayPause(poi);
        });

        System.Diagnostics.Debug.WriteLine($"HomeViewModel created. Current language: {SettingService.Instance.Language}");
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

        TopPOIs.Clear();
        foreach (var item in top) TopPOIs.Add(item);

        AllPOIs.Clear();
        foreach (var item in all) AllPOIs.Add(item);

        System.Diagnostics.Debug.WriteLine($"Loaded {TopPOIs.Count} top POIs and {AllPOIs.Count} all POIs");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    async Task HandlePlayPause(POI poi)
    {
        if (poi == null) return;

        string currentLang = SettingService.Instance.Language;
        System.Diagnostics.Debug.WriteLine($"HandlePlayPause: POI={poi.Name}, Lang={currentLang}");

        // ── PAUSE: đang phát cùng POI → dừng, ghi nhớ vị trí câu ────────────
        if (currentPlayingPoi?.Id == poi.Id && isPlaying)
        {
            System.Diagnostics.Debug.WriteLine("Pausing playback");
            ttsToken?.Cancel();
            isPlaying = false;
            poi.IsPlaying = false;
            // ttsService giữ nguyên _sentenceIndex → resume tiếp tục đúng chỗ
            return;
        }

        // ── RESUME: cùng POI, đang pause ─────────────────────────────────────
        if (currentPlayingPoi?.Id == poi.Id && !isPlaying)
        {
            string langNow = SettingService.Instance.Language;

            // Nếu ngôn ngữ đổi kể từ lần load → dịch lại, phát từ đầu với ngôn ngữ mới
            if (langNow != _currentLoadedLang)
            {
                System.Diagnostics.Debug.WriteLine($"Language changed: {_currentLoadedLang} → {langNow}, reloading text");
                string srcText =
                    !string.IsNullOrWhiteSpace(poi.Script) ? poi.Script :
                    !string.IsNullOrWhiteSpace(poi.Description) ? poi.Description :
                    "Không có dữ liệu";

                string freshText = await GetTranslatedTextAsync(poi.Id, srcText, langNow);
                ttsService.LoadText(freshText);
                _currentLoadedLang = langNow;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Resuming playback (same language)");
            }

            isPlaying = true;
            poi.IsPlaying = true;

            ttsToken = new CancellationTokenSource();
            try
            {
                // Tiếp tục từ _sentenceIndex; nếu IsFinished thì replay từ đầu
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
        System.Diagnostics.Debug.WriteLine("Starting new playback");
        StopAll();

        currentPlayingPoi = poi;
        isPlaying = true;
        poi.IsPlaying = true;

        // Lấy text gốc (tiếng Việt)
        string originalText =
            !string.IsNullOrWhiteSpace(poi.Script) ? poi.Script :
            !string.IsNullOrWhiteSpace(poi.Description) ? poi.Description :
            "Không có dữ liệu";

        string finalText = await GetTranslatedTextAsync(poi.Id, originalText, currentLang);

        // Load text mới vào TTS service (reset sentence index về 0)
        ttsService.LoadText(finalText);
        _currentLoadedLang = currentLang;   // ghi nhớ ngôn ngữ đang được load

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

    /// Lấy text đã dịch; cache theo poiId + lang để không dịch lại khi resume
    async Task<string> GetTranslatedTextAsync(int poiId, string originalText, string lang)
    {
        if (lang == "vi") return originalText;

        string cacheKey = $"{poiId}_{lang}";
        if (translationCache.TryGetValue(cacheKey, out var cached))
        {
            System.Diagnostics.Debug.WriteLine($"Using cached translation for {cacheKey}");
            return cached;
        }

        System.Diagnostics.Debug.WriteLine($"Translating POI {poiId} to {lang}...");
        string translated = await translateService.TranslateWithRetryAsync(originalText, lang);
        translationCache[cacheKey] = translated;
        System.Diagnostics.Debug.WriteLine($"Translation cached for {cacheKey}");
        return translated;
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