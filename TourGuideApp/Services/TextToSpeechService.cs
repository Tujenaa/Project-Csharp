using Microsoft.Maui.Media;
using TourGuideApp.Services;

public class TextToSpeechService
{
    public async Task SpeakAsync(string text, CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var lang = SettingService.Instance.Language; // e.g. "vi", "en", "fr"
        var locales = await TextToSpeech.GetLocalesAsync();

        // 1. Exact language match (e.g. "en" matches "en-US", "en-GB")
        var locale = locales.FirstOrDefault(l =>
                         l.Language.Equals(lang, StringComparison.OrdinalIgnoreCase))
                  // 2. Prefix match (e.g. lang="en" matches locale "en-US")
                  ?? locales.FirstOrDefault(l =>
                         l.Language.StartsWith(lang + "-", StringComparison.OrdinalIgnoreCase))
                  // 3. Partial match fallback (original behaviour)
                  ?? locales.FirstOrDefault(l =>
                         l.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase))
                  // 4. Ultimate fallback: Vietnamese
                  ?? locales.FirstOrDefault(l =>
                         l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase));

        await TextToSpeech.SpeakAsync(text, new SpeechOptions
        {
            Locale = locale,
            Pitch = 1.0f,
            Volume = 1.0f
        }, cancelToken);
    }
}