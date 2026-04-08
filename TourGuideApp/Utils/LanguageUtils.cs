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

        string? script = lang switch
        {
            "en" => poi.ScriptEn,
            "ja" => poi.ScriptJa,
            "zh" => poi.ScriptZh,
            _ => poi.ScriptVi
        };

        if (string.IsNullOrWhiteSpace(script))
        {
            return poi.Description ?? "Không có dữ liệu thuyết minh";
        }

        return script;
    }
}
