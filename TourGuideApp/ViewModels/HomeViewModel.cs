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

    POI? currentPlayingPoi;
    bool isPlaying = false;
    CancellationTokenSource? ttsToken;

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
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    async Task HandlePlayPause(POI poi)
    {
        if (poi == null) return;

        // Pause if already playing this POI
        if (currentPlayingPoi?.Id == poi.Id && isPlaying)
        {
            ttsToken?.Cancel();
            isPlaying = false;
            poi.IsPlaying = false;
            currentPlayingPoi = null;
            return;
        }

        // Stop any currently playing POI
        StopAll();

        currentPlayingPoi = poi;
        isPlaying = true;
        poi.IsPlaying = true;

        ttsToken = new CancellationTokenSource();

        // FIX: Use Script first, fall back to Description only if Script is empty
        string text =
         !string.IsNullOrWhiteSpace(poi.Script) ? poi.Script :
         !string.IsNullOrWhiteSpace(poi.Description) ? poi.Description :
         "Không có dữ liệu";

        try
        {
            await ttsService.SpeakAsync(text, ttsToken.Token);
        }
        catch (OperationCanceledException) { /* user paused */ }
        catch (Exception ex) { Console.WriteLine(ex.Message); }

        isPlaying = false;
        poi.IsPlaying = false;
        if (currentPlayingPoi?.Id == poi.Id)
            currentPlayingPoi = null;
    }

    void StopAll()
    {
        ttsToken?.Cancel();
        foreach (var item in TopPOIs) item.IsPlaying = false;
        foreach (var item in AllPOIs) item.IsPlaying = false;
        isPlaying = false;
    }
}