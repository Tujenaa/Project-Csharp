namespace TourGuideApp.Models;

public class Language
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int OrderIndex { get; set; } = 0;
}
