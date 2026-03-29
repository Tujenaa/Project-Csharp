using System.Net.Http;
using System.Text.Json;

namespace TourGuideApp.Services;

/// <summary>
/// Dịch văn bản qua Google Translate free endpoint —
/// cùng API với web admin (translate.googleapis.com/translate_a/single)
/// Không cần key, hỗ trợ vi→en/ja/zh/fr/ko/...
/// </summary>
public class TranslateService
{
    readonly HttpClient http = new();

    public TranslateService()
    {
        http.Timeout = TimeSpan.FromSeconds(15);
        // Bắt buộc có User-Agent để Google không trả 403
        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    }

    /// <summary>
    /// Dịch <paramref name="text"/> từ tiếng Việt sang <paramref name="toLang"/>.
    /// Nếu toLang == "vi" trả về text gốc ngay, không gọi mạng.
    /// </summary>
    public async Task<string> TranslateAsync(string text, string toLang)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        if (toLang == "vi") return text;

        try
        {
            // Cùng endpoint với web admin
            // sl=vi  (source: Vietnamese — script gốc luôn là tiếng Việt)
            // tl=toLang
            // dt=t   (chỉ lấy translation, không cần định nghĩa/phiên âm)
            var url =
                $"https://translate.googleapis.com/translate_a/single" +
                $"?client=gtx&sl=vi&tl={Uri.EscapeDataString(toLang)}&dt=t" +
                $"&q={Uri.EscapeDataString(text)}";

            var json = await http.GetStringAsync(url);

            // Kết quả: [[ ["dịch","gốc",...], ... ], ...]
            // → nối tất cả phần tử [i][0] lại thành chuỗi
            using var doc = JsonDocument.Parse(json);
            var segments = doc.RootElement[0];
            var sb = new System.Text.StringBuilder();
            foreach (var seg in segments.EnumerateArray())
            {
                var part = seg[0].GetString();
                if (!string.IsNullOrEmpty(part))
                    sb.Append(part);
            }

            var result = sb.ToString().Trim();
            System.Diagnostics.Debug.WriteLine($"[Translate] {toLang}: {result[..Math.Min(80, result.Length)]}...");
            return string.IsNullOrWhiteSpace(result) ? text : result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Translate] ERROR: {ex.Message}");
            return text; // Fallback: đọc text gốc tiếng Việt
        }
    }

    /// <summary>Retry wrapper — tối đa <paramref name="maxRetries"/> lần.</summary>
    public async Task<string> TranslateWithRetryAsync(
        string text, string toLang, int maxRetries = 2)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await TranslateAsync(text, toLang);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Translate] Attempt {i + 1} failed: {ex.Message}");
                if (i == maxRetries - 1) return text;
                await Task.Delay(1000);
            }
        }
        return text;
    }
}