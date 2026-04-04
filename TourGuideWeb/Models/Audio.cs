namespace GPSGuide.Web.Models;

public class Audio
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string? AudioUrl { get; set; }
    public string? Script { get; set; }
    public string? Language { get; set; }
    public string? PoiName { get; set; }
    public string? vi { get; set; }
    public string? en { get; set; }
    public string? ja { get; set; }
    public string? zh { get; set; }
}