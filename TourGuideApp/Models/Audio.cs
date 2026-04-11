namespace TourGuideApp.Models;

public class Audio
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public int LanguageId { get; set; }
    public string? LanguageCode { get; set; } // 'vi', 'en', etc.
    public string? Script { get; set; }
}
