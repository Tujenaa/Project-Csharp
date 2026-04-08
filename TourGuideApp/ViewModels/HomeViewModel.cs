using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.ViewModels;
using TourGuideApp.Utils;

namespace TourGuideApp.ViewModels;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly ApiService _poiService = new();
    public ObservableCollection<POI> TopPOIs { get; set; } = new();
    public ObservableCollection<POI> AllPOIs { get; set; } = new();

    public ICommand GoToDetailCommand { get; }
    public Command<POI> PlayAudioCommand { get; }
    public Command<POI> PauseAudioCommand { get; }
    
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
            await AudioPlaybackService.Instance.PlayAsync(poi);
        });

        PauseAudioCommand = new Command<POI>((poi) =>
        {
            AudioPlaybackService.Instance.Pause();
        });

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
            if (myLoc != null) DistanceUtils.UpdatePoiDistance(item, myLoc.Latitude, myLoc.Longitude);
            TopPOIs.Add(item);
        }

        AllPOIs.Clear();
        POI? nearest = null;
        double minDist = double.MaxValue;

        foreach (var item in all) 
        {
            if (myLoc != null) 
            {
                var d = DistanceUtils.UpdatePoiDistance(item, myLoc.Latitude, myLoc.Longitude);
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

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}