using System.Net.Http;
using System.Text.Json;
using System.Web;

namespace TourGuideApp.Services;

public class TranslateService
{
    readonly HttpClient http = new();

    public TranslateService()
    {
        http.Timeout = TimeSpan.FromSeconds(15);
        // Thêm User-Agent để tránh bị chặn
        http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<string> TranslateAsync(string text, string toLang)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        // Nếu ngôn ngữ đích là tiếng Việt, không cần dịch
        if (toLang == "vi")
        {
            System.Diagnostics.Debug.WriteLine("Translate: Target language is Vietnamese, no translation needed");
            return text;
        }

        try
        {
            var encodedText = Uri.EscapeDataString(text);

            // Sử dụng API dịch thay thế (LibreTranslate hoặc MyMemory)
            // Option 1: MyMemory API (free, không cần key)
            var url = $"https://api.mymemory.translated.net/get?q={encodedText}&langpair=vi|{toLang}";

            System.Diagnostics.Debug.WriteLine($"Translate API URL: {url}");

            var response = await http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Translation response: {json}");

            using var doc = JsonDocument.Parse(json);
            var translatedText = doc.RootElement
                .GetProperty("responseData")
                .GetProperty("translatedText")
                .GetString();

            return !string.IsNullOrWhiteSpace(translatedText) ? translatedText : text;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Translation error: {ex.Message}");
            return text;
        }
    }

    // Hàm dịch với retry mechanism
    public async Task<string> TranslateWithRetryAsync(string text, string toLang, int maxRetries = 2)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var result = await TranslateAsync(text, toLang);
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Translation attempt {i + 1} failed: {ex.Message}");
                if (i == maxRetries - 1)
                    return text;
                await Task.Delay(1000);
            }
        }
        return text;
    }
}