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
            if (poi == null) return;

            await ttsService.SpeakAsync(poi.Description ?? "");
        });
    }

    private int _poiCount;
    public int PoiCount
    {
        get => _poiCount;
        set
        {
            _poiCount = value;
            OnPropertyChanged(nameof(PoiCount));
        }
    }

    private int _audioCount;
    public int AudioCount
    {
        get => _audioCount;
        set
        {
            _audioCount = value;
            OnPropertyChanged(nameof(AudioCount));
        }
    }

    public async Task LoadData()
    {
        var top = await _poiService.GetTopPOI();
        var all = await _poiService.GetPOI();

        PoiCount = await _poiService.GetPoiCount();
        AudioCount = await _poiService.GetAudioCount();

        TopPOIs.Clear();
        foreach (var item in top)
            TopPOIs.Add(item);

        AllPOIs.Clear();
        foreach (var item in all)
            AllPOIs.Add(item);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}