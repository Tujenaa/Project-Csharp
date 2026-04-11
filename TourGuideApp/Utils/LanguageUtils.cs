using TourGuideApp.Models;

namespace TourGuideApp.Utils;

public static class LanguageUtils
{
    /// <summary>
    /// Lấy kịch bản thuyết minh dựa trên mã ngôn ngữ và POI cung cấp.
    /// Fallback về Description nếu kịch bản trống.
    /// </summary>
    public static string GetScript(POI poi, string lang)
    {
        if (poi == null) return "Không có dữ liệu địa điểm";

        string? script = poi.Audios?
            .FirstOrDefault(a => a.LanguageCode != null && a.LanguageCode.Equals(lang, StringComparison.OrdinalIgnoreCase))?
            .Script;
        
        // Nếu không tìm thấy ngôn ngữ yêu cầu, thử tìm Tiếng Việt làm fallback
        if (string.IsNullOrWhiteSpace(script) && !lang.Equals("vi", StringComparison.OrdinalIgnoreCase))
        {
            script = poi.Audios?
                .FirstOrDefault(a => a.LanguageCode != null && a.LanguageCode.Equals("vi", StringComparison.OrdinalIgnoreCase))?
                .Script;
        }

        if (string.IsNullOrWhiteSpace(script))
        {
            return poi.Description ?? "Không có dữ liệu thuyết minh";
        }

        return script;
    }
}
