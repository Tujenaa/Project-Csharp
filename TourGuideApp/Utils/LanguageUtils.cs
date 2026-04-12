using TourGuideApp.Models;

namespace TourGuideApp.Utils;

public static class LanguageUtils
{
    /// <summary>
    /// Kiểm tra xem POI đã có bản thuyết minh viết tay cho ngôn ngữ này chưa.
    /// </summary>
    public static bool HasHandwrittenScript(POI poi, string lang)
    {
        if (poi?.Audios == null) return false;
        return poi.Audios.Any(a =>
            a.LanguageCode != null &&
            a.LanguageCode.Equals(lang, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(a.Script));
    }

    /// <summary>
    /// Lấy kịch bản Tiếng Việt chính thức của POI.
    /// KHÔNG fallback về Description vì Description thường là HTML/text tóm tắt,
    /// không phải script thuyết minh — dịch ra sẽ cho kết quả kém và dễ timeout.
    /// </summary>
    public static string GetVietnameseScript(POI poi)
    {
        if (poi?.Audios == null) return "";
        return poi.Audios.FirstOrDefault(a =>
            a.LanguageCode != null &&
            a.LanguageCode.Equals("vi", StringComparison.OrdinalIgnoreCase))?
            .Script ?? "";
    }

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