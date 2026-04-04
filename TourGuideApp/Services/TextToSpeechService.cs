using Microsoft.Maui.Media;
using System.Diagnostics;

namespace TourGuideApp.Services;

/// <summary>
/// TTS service hỗ trợ Native Pause/Resume thông qua Android/iOS API.
/// </summary>
public partial class TextToSpeechService
{
    private string _currentText = "";
    private int _charIndex = 0; // Vị trí ký tự đang đọc (để Resume chính xác)
    private bool _finished = false;
    private bool _isPaused = false;

    public bool IsFinished => _finished;
    public bool IsPaused => _isPaused;

    public event Action? OnFinished;
    public event Action<int, int>? OnProgress;

    private static readonly Dictionary<string, string[]> LocalePriority = new()
    {
        ["vi"] = new[] { "Google Vietnamese", "Microsoft HoaiMy", "vi-VN" },
        ["en"] = new[] { "Google US English", "Microsoft Aria", "en-US" },
        ["ja"] = new[] { "Google 日本語", "Microsoft Nanami", "ja-JP" },
        ["zh"] = new[] { "Google 普通话", "Microsoft Xiaoxiao", "zh-CN" },
    };

    public void LoadText(string text)
    {
        string finalText = string.IsNullOrWhiteSpace(text) ? "Không có dữ liệu" : text;
        if (_currentText == finalText) return;
        _currentText = finalText;
        _charIndex = 0;
        _finished = false;
        _isPaused = false;
        Debug.WriteLine($"[TTS] LoadText: {_currentText.Length} chars");
    }

    public async Task SpeakAsync(CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(_currentText)) return;

        if (_finished)
        {
            _charIndex = 0;
            _finished = false;
            Debug.WriteLine("[TTS] Replay from beginning");
        }

        _isPaused = false;
        var lang = SettingService.Instance.Language;

#if ANDROID
        await SpeakNativeAndroidAsync(lang, cancelToken);
#else
        await SpeakMauiAsync(lang, cancelToken);
#endif
    }

    public async Task SpeakAsync(string text, CancellationToken cancelToken = default)
    {
        LoadText(text);
        await SpeakAsync(cancelToken);
    }

    public void Pause()
    {
        _isPaused = true;
#if ANDROID
        StopNativeAndroid();
#else
        // MAUI Essentials không có Pause thực sự
#endif
    }

    private async Task SpeakMauiAsync(string lang, CancellationToken cancelToken)
    {
        var locales = await TextToSpeech.GetLocalesAsync();
        var locale = await ResolveLocaleAsync(lang, locales);
        await TextToSpeech.SpeakAsync(_currentText, new SpeechOptions { Locale = locale }, cancelToken);
    }

    private async Task<Locale?> ResolveLocaleAsync(string lang, IEnumerable<Locale> locales)
    {
        if (LocalePriority.TryGetValue(lang.ToLowerInvariant(), out var preferred))
        {
            foreach (var pref in preferred)
            {
                var matchByName = locales.FirstOrDefault(l =>
                    l.Name != null && l.Name.Contains(pref, StringComparison.OrdinalIgnoreCase));
                if (matchByName != null) return matchByName;

                var parts = pref.Split('-');
                var matchByCode = locales.FirstOrDefault(l =>
                    l.Language.Equals(parts[0], StringComparison.OrdinalIgnoreCase) &&
                    (parts.Length <= 1 || l.Country?.Equals(parts[1], StringComparison.OrdinalIgnoreCase) == true));
                if (matchByCode != null) return matchByCode;
            }
        }
        return locales.FirstOrDefault(l => l.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase));
    }

#if ANDROID
    private partial Task SpeakNativeAndroidAsync(string lang, CancellationToken cancelToken);
    private partial void StopNativeAndroid();
#endif
}