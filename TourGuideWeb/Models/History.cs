namespace GPSGuide.Web.Models;

public class History
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string? PoiName { get; set; }
    public DateTime PlayTime { get; set; }
}