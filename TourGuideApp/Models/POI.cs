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
    public string? Script { get; set; }

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