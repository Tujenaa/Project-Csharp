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

    POI? currentPlayingPoi;
    bool isPlaying = false;
    CancellationTokenSource? ttsToken;

    int currentPosition = 0;
    string currentText = "";

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

        // Log để debug
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
        var currentLang = SettingService.Instance.Language;
        System.Diagnostics.Debug.WriteLine($"HandlePlayPause called. POI: {poi?.Name}, Current Language: {currentLang}");

        if (poi == null) return;

        // ───────────── PAUSE ─────────────
        if (currentPlayingPoi?.Id == poi.Id && isPlaying)
        {
            System.Diagnostics.Debug.WriteLine("Pausing playback");
            ttsToken?.Cancel();
            isPlaying = false;
            poi.IsPlaying = false;
            return;
        }

        // ───────────── RESUME ─────────────
        if (currentPlayingPoi?.Id == poi.Id && !isPlaying)
        {
            System.Diagnostics.Debug.WriteLine("Resuming playback");
            isPlaying = true;
            poi.IsPlaying = true;

            ttsToken = new CancellationTokenSource();

            try
            {
                string remainingText = currentText.Substring(currentPosition);
                await ttsService.SpeakAsync(remainingText, ttsToken.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RESUME ERROR: {ex.Message}");
            }

            isPlaying = false;
            poi.IsPlaying = false;
            return;
        }

        // ───────────── PLAY MỚI ─────────────
        System.Diagnostics.Debug.WriteLine("Starting new playback");
        StopAll();

        currentPlayingPoi = poi;
        isPlaying = true;
        poi.IsPlaying = true;

        // Lấy text gốc (tiếng Việt)
        string originalText = !string.IsNullOrWhiteSpace(poi.Script) ? poi.Script :
                              !string.IsNullOrWhiteSpace(poi.Description) ? poi.Description :
                              "Không có dữ liệu";

        System.Diagnostics.Debug.WriteLine($"Original text: {originalText?.Substring(0, Math.Min(100, originalText?.Length ?? 0))}...");

        string targetLang = SettingService.Instance.Language;
        System.Diagnostics.Debug.WriteLine($"Target language: {targetLang}");

        string finalText;

        try
        {
            if (targetLang == "vi")
            {
                finalText = originalText;
                System.Diagnostics.Debug.WriteLine("Playing in Vietnamese (no translation)");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Translating from Vietnamese to {targetLang}...");
                finalText = await translateService.TranslateWithRetryAsync(originalText, targetLang);
                System.Diagnostics.Debug.WriteLine($"Translation completed. Result: {finalText?.Substring(0, Math.Min(100, finalText?.Length ?? 0))}...");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TRANSLATE ERROR: {ex.Message}");
            finalText = originalText;
        }

        currentText = finalText;
        currentPosition = 0;

        ttsToken = new CancellationTokenSource();

        try
        {
            await ttsService.SpeakAsync(finalText, ttsToken.Token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TTS ERROR: {ex.Message}");
        }

        isPlaying = false;
        poi.IsPlaying = false;
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