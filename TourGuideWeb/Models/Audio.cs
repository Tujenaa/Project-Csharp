namespace GPSGuide.Web.Models;

public class Audio
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string? PoiName { get; set; }
    public string Language { get; set; } = "vi";
    public string? AudioUrl { get; set; }
    public string? Script { get; set; }
}