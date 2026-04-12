using System.Text.Json.Serialization;

namespace TourGuideApp.Models;

public class Audio
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public int LanguageId { get; set; }
    public string? Script { get; set; }

    /// <summary>
    /// Nested Language object do API trả về dạng:
    /// "language": { "code": "vi", "name": "Tiếng Việt" }
    /// (EF Core Include() tự sinh ra cấu trúc này)
    /// [JsonPropertyName] explicit để đảm bảo map đúng dù API trả camelCase.
    /// </summary>
    [JsonPropertyName("language")]
    public Language? Language { get; set; }

    /// <summary>
    /// Backing field cho dữ liệu cũ: seed JSON và SQLite cache
    /// vốn lưu trực tiếp "languageCode": "vi" (flat, không nested).
    /// </summary>
    [JsonPropertyName("languageCode")]
    public string? LanguageCodeFlat { get; set; }

    /// <summary>
    /// Mã ngôn ngữ dùng trong toàn app ('vi', 'en', 'ko'...).
    /// - Khi đọc từ API online  → Language.Code  (nested object)
    /// - Khi đọc từ seed/cache  → LanguageCodeFlat (flat field)
    /// Ưu tiên LanguageCodeFlat nếu Language null để tránh mất dữ liệu.
    /// </summary>
    [JsonIgnore]
    public string? LanguageCode => LanguageCodeFlat ?? Language?.Code;
}