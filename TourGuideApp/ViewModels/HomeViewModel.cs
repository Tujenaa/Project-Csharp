using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.Utils;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.ApplicationModel;

namespace TourGuideApp.ViewModels;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService = new();

    public ObservableCollection<POI> TopPOIs { get; set; } = new();
    public ObservableCollection<POI> AllPOIs { get; set; } = new();
    public ObservableCollection<Tour> FeaturedTours { get; set; } = new();

    public ICommand GoToDetailCommand { get; }
    public Command<POI> PlayAudioCommand { get; }
    public Command<POI> PauseAudioCommand { get; }
    public Command StopAudioCommand { get; }
    public Command<Tour> GoToTourMapCommand { get; }
    public Command<POI> GoToMapWithPoiCommand { get; }

    private POI? _currentPlayedPoi;
    public POI? CurrentPlayedPoi
    {
        get => _currentPlayedPoi;
        set { _currentPlayedPoi = value; OnPropertyChanged(nameof(CurrentPlayedPoi)); OnPropertyChanged(nameof(IsPoiPlaying)); }
    }

    public bool IsPoiPlaying => CurrentPlayedPoi != null;

    private int _tourCount;
    public int TourCount
    {
        get => _tourCount;
        set { _tourCount = value; OnPropertyChanged(nameof(TourCount)); }
    }

    private string _nearestPoiName = "...";
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

    private string _currentAddress = LocalizationService.Get("determining_location");
    public string CurrentAddress
    {
        get => _currentAddress;
        set { _currentAddress = value; OnPropertyChanged(nameof(CurrentAddress)); }
    }

    public HomeViewModel()
    {
        GoToDetailCommand = new Command<POI>(async (poi) =>
        {
            if (poi == null) return;
            await Shell.Current.GoToAsync("placeDetail", new Dictionary<string, object> { { "poi", poi } });
        });

        PlayAudioCommand = new Command<POI>(async (poi) =>
        {
            await AudioPlaybackService.Instance.PlayAsync(poi);
        });

        PauseAudioCommand = new Command<POI>((poi) =>
        {
            AudioPlaybackService.Instance.Pause();
        });

        StopAudioCommand = new Command(() =>
        {
            _ = AudioPlaybackService.Instance.StopAsync();
        });

        GoToTourMapCommand = new Command<Tour>(async (tour) =>
        {
            if (tour == null) return;
            MapTourState.SelectedTour = tour;
            await Shell.Current.GoToAsync("//map");
        });

        GoToMapWithPoiCommand = new Command<POI>(async (poi) =>
        {
            if (poi == null) return;
            MapTourState.FocusPoiId = poi.Id;
            await Shell.Current.GoToAsync("//map");
        });

        AudioPlaybackService.Instance.PlaybackStateChanged += () =>
        {
            CurrentPlayedPoi = AudioPlaybackService.Instance.CurrentPlayingPoi;
            // Cập nhật lại list IsPlaying để icon UI load lại
            foreach (var p in TopPOIs) p.IsPlaying = (CurrentPlayedPoi?.Id == p.Id && AudioPlaybackService.Instance.IsPlaying);
            foreach (var p in AllPOIs) p.IsPlaying = (CurrentPlayedPoi?.Id == p.Id && AudioPlaybackService.Instance.IsPlaying);
        };

        // Làm mới bản dịch trong ViewModel khi đổi ngôn ngữ
        LocalizationDataManager.Instance.PropertyChanged += (s, e) => 
        {
            OnPropertyChanged(string.Empty);
            // Re-load để cập nhật các text từ localization service nếu cần
            _ = LoadData(); 
        };
    }

    public async Task LoadData()
    {
        var topTask = _apiService.GetTopPOI();
        var allTask = _apiService.GetPOI();
        var toursTask = _apiService.GetTours();
        await Task.WhenAll(topTask, allTask, toursTask);

        var top = topTask.Result;
        var all = allTask.Result;
        var tours = toursTask.Result;

        TourCount = tours.Count;

        Location? myLoc = null;
        try
        {
            myLoc = await Geolocation.Default.GetLastKnownLocationAsync()
                    ?? await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
        }
        catch { }

        // Top 2 POI nổi bật
        TopPOIs.Clear();
        foreach (var item in top.Take(2))
        {
            if (myLoc != null) DistanceUtils.UpdatePoiDistance(item, myLoc.Latitude, myLoc.Longitude);
            TopPOIs.Add(item);
        }

        // Tính nearest từ all POIs
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

        // Top 2 tour nổi bật
        FeaturedTours.Clear();
        foreach (var tour in tours.Take(2))
        {
            if (myLoc != null && tour.POIs != null)
                foreach (var poi in tour.POIs)
                    DistanceUtils.UpdatePoiDistance(poi, myLoc.Latitude, myLoc.Longitude);
            FeaturedTours.Add(tour);
        }

        if (myLoc != null)
        {
            try
            {
                var placemarks = await Geocoding.GetPlacemarksAsync(myLoc);
                var place = placemarks?.FirstOrDefault();
                if (place != null)
                {
                    string street = place.Thoroughfare ?? "";
                    string ward = place.SubLocality ?? "";
                    string district = place.SubAdminArea ?? "";
                    string city = place.Locality ?? "";

                    var parts = new List<string> { street, ward, district, city }
                        .Where(s => !string.IsNullOrWhiteSpace(s));

                    CurrentAddress = string.Join(", ", parts);
                    if (string.IsNullOrWhiteSpace(CurrentAddress))
                        CurrentAddress = LocalizationService.Get("current_location");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Geocoding error: {ex.Message}");
                CurrentAddress = LocalizationService.Get("current_location");
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}