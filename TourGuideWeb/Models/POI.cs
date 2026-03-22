namespace GPSGuide.Web.Models;
public class POI
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Address { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Radius { get; set; }
    public int? OwnerId { get; set; }
    public string? OwnerName { get; set; }
}