using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TourGuideApp.Models;

public class POI : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Radius { get; set; }
    public string Status { get; set; } = "APPROVED";
    public string? TourRelationshipStatus { get; set; }

    public bool IsApproved => Status == "APPROVED";
    public bool IsReady => IsApproved && Audios != null && Audios.Count > 0;
    public bool HasAudio => Audios != null && Audios.Count > 0;
    public bool IsApprovedInTour => IsApproved && (string.IsNullOrEmpty(TourRelationshipStatus) || TourRelationshipStatus == "APPROVED");

    
    public List<Audio> Audios { get; set; } = new();
    public List<string> Images { get; set; } = new();

    public List<string> FullImages
    {
        get
        {
            var baseUrl = Services.ApiService.ApiConfig.BaseUrl.Replace("/api/", "").TrimEnd('/');
            if (Images == null || Images.Count == 0) 
                return new List<string> { "place_placeholder.png" };
            
            return Images.Select(img => 
            {
                var remoteUrl = img.StartsWith("http") ? img : baseUrl + "/" + img.TrimStart('/');
                return Services.ImageCacheService.Instance.GetImageSource(remoteUrl) ?? remoteUrl;
            }).ToList();
        }
    }

    public string MainImage => FullImages.FirstOrDefault() ?? "place_placeholder.png";


    // =============================
    // Playback state
    // =============================
    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }

    private double _audioProgress;
    public double AudioProgress
    {
        get => _audioProgress;
        set => SetProperty(ref _audioProgress, value);
    }

    private string _audioDuration = "0:00";
    public string AudioDuration
    {
        get => _audioDuration;
        set => SetProperty(ref _audioDuration, value);
    }

    // =============================
    // UI hiển thị
    // =============================

    // Số thứ tự marker (1,2,3...)
    private string _indexLabel = "";
    public string IndexLabel
    {
        get => _indexLabel;
        set => SetProperty(ref _indexLabel, value);
    }

    // Khoảng cách (VD: 350m, 1.2km)
    private string _distanceText = "Đang tính...";
    public string DistanceText
    {
        get => _distanceText;
        set => SetProperty(ref _distanceText, value);
    }

    private bool _isNearest;
    public bool IsNearest
    {
        get => _isNearest;
        set => SetProperty(ref _isNearest, value);
    }

    private bool _isInAnyTour;
    public bool IsInAnyTour
    {
        get => _isInAnyTour;
        set => SetProperty(ref _isInAnyTour, value);
    }

    // Thời gian đi bộ ước tính
    private string _walkingTimeText = "---";
    public string WalkingTimeText
    {
        get => _walkingTimeText;
        set => SetProperty(ref _walkingTimeText, value);
    }

    // =============================
    // INotifyPropertyChanged chuẩn
    // =============================
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(name);
        return true;
    }
}