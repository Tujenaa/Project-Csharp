using TourGuideApp.Models;

namespace TourGuideApp.Utils;

public static class LanguageUtils
{


    /// <summary>
    /// Lấy kịch bản thuyết minh dựa trên mã ngôn ngữ.
    /// KHÔNG fallback tự động về Tiếng Việt (để AudioPlaybackService xử lý dịch thuật).
    /// </summary>
    public static string GetScript(POI poi, string lang)
    {
        if (poi == null) return "Không có dữ liệu địa điểm";

        string? script = poi.Audios?
            .FirstOrDefault(a => a.LanguageCode != null && a.LanguageCode.Equals(lang, StringComparison.OrdinalIgnoreCase))?
            .Script;

        if (string.IsNullOrWhiteSpace(script))
        {
            return ""; // Trả về trống để báo hiệu cần dịch
        }

        return script;
    }

    /// <summary>
    /// Tính mã băm MD5 cho chuỗi văn bản để phát hiện thay đổi nội dung.
    /// </summary>
    public static string CalculateHash(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        using var md5 = System.Security.Cryptography.MD5.Create();
        byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);

        return Convert.ToHexString(hashBytes);
    }
}