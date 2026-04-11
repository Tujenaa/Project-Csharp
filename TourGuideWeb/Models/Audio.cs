namespace GPSGuide.Web.Models;

public class Audio
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string? PoiName { get; set; }
    public int LanguageId { get; set; }
    public string? LanguageCode { get; set; }
    public string? LanguageName { get; set; }
    public string Script { get; set; } = "";
}

public class Language
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int OrderIndex { get; set; }
}