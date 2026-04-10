using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TourGuideApp.Models;

public class Tour : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Status { get; set; }

    public List<POI> POIs { get; set; } = new();

    public string FullThumbnail
    {
        get
        {
            if (string.IsNullOrEmpty(ThumbnailUrl)) return "place_placeholder.png";
            var baseUrl = Services.ApiService.ApiConfig.BaseUrl.Replace("/api/", "");
            return ThumbnailUrl.StartsWith("http") ? ThumbnailUrl : baseUrl + "/" + ThumbnailUrl;
        }
    }

    public string PoiCountText => $"{POIs?.Count ?? 0} địa điểm";

    // First POI image as thumbnail fallback
    public string ThumbnailOrFirstPoi
    {
        get
        {
            if (!string.IsNullOrEmpty(ThumbnailUrl)) return FullThumbnail;
            var firstPoi = POIs?.FirstOrDefault();
            return firstPoi?.MainImage ?? "place_placeholder.png";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
