using Microsoft.Maui.Media;

public class TextToSpeechService
{
    public async Task SpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var locales = await TextToSpeech.GetLocalesAsync();
        var vi = locales.FirstOrDefault(l => l.Language.StartsWith("vi"));

        await TextToSpeech.SpeakAsync(text, new SpeechOptions
        {
            Locale = vi,
            Pitch = 1.0f,
            Volume = 1.0f
        });
    }
}