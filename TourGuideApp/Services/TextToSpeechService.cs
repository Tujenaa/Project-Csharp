using Microsoft.Maui.Media;
using System.Diagnostics;

namespace TourGuideApp.Services;

/// <summary>
/// TTS service: phát văn bản theo ngôn ngữ hiện tại của SettingService.
/// Hỗ trợ Play / Pause / Stop và báo cáo tiến trình qua OnProgress / OnFinished.
/// </summary>
public partial class TextToSpeechService
{
    private string _currentText = "";
    private int _charIndex = 0;
    private bool _finished = false;
    private bool _isPaused = false;

    public bool IsFinished => _finished;
    public bool IsPaused => _isPaused;

    /// <summary>Phát ra khi TTS đọc xong toàn bộ văn bản.</summary>
    public event Action? OnFinished;

    /// <summary>Phát ra định kỳ: (ký tự hiện tại, tổng số ký tự).</summary>
    public event Action<int, int>? OnProgress;

    // Độ ưu tiên giọng đọc theo ngôn ngữ
    private static readonly Dictionary<string, string[]> LocalePriority = new()
    {
        ["vi"] = new[] { "Microsoft HoaiMy", "Google Vietnamese", "vi-VN" },
        ["en"] = new[] { "Microsoft Aria", "Google US English", "en-US" },
        ["ja"] = new[] { "Microsoft Nanami", "Google 日本語", "ja-JP" },
        ["zh"] = new[] { "Microsoft Xiaoxiao", "Google 普通话", "zh-CN" },
        ["ko"] = new[] { "Microsoft SunHi", "Google 한국의", "ko-KR" },
        ["fr"] = new[] { "Microsoft Denise", "Google français", "fr-FR" },
        ["de"] = new[] { "Microsoft Katja", "Google Deutsch", "de-DE" },
        ["th"] = new[] { "Microsoft Premwadee", "Google ไทย", "th-TH" },
    };

    // ── PUBLIC API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Nạp văn bản mới. Nếu văn bản trùng với lần trước, không reset (tiếp tục từ vị trí cũ).
    /// Dùng ForceLoadText để buộc reset và replay từ đầu.
    /// </summary>
    public void LoadText(string text)
    {
        string finalText = string.IsNullOrWhiteSpace(text) ? "Không có dữ liệu." : text;
        if (_currentText == finalText) return; // không thay đổi → giữ nguyên trạng thái
        _currentText = finalText;
        _charIndex = 0;
        _finished = false;
        _isPaused = false;
        Debug.WriteLine($"[TTS] LoadText: {_currentText.Length} chars");
    }

    /// <summary>
    /// Buộc nạp lại văn bản và reset về đầu, kể cả khi nội dung không đổi.
    /// Dùng khi muốn replay cùng một POI.
    /// </summary>
    public void ForceLoadText(string text)
    {
        string finalText = string.IsNullOrWhiteSpace(text) ? "Không có dữ liệu." : text;
        _currentText = finalText;
        _charIndex = 0;
        _finished = false;
        _isPaused = false;
        Debug.WriteLine($"[TTS] ForceLoadText: {_currentText.Length} chars");
    }

    /// <summary>Phát TTS từ vị trí hiện tại (_charIndex).</summary>
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

        // Đọc ngôn ngữ ngay tại lúc phát để luôn phản ánh setting mới nhất
        string lang = SettingService.Instance.Language;
        Debug.WriteLine($"[TTS] SpeakAsync lang={lang}, charIndex={_charIndex}");

        try
        {
#if ANDROID
            await SpeakNativeAndroidAsync(lang, cancelToken);
#else
            await SpeakMauiAsync(lang, cancelToken);
#endif
            // Chỉ invoke OnFinished nếu không bị cancel
            if (!cancelToken.IsCancellationRequested)
            {
                _finished = true;
                OnFinished?.Invoke();
                Debug.WriteLine("[TTS] Finished");
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[TTS] Cancelled");
            throw; // re-throw để caller xử lý
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TTS] Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>Overload tiện lợi: nạp text rồi phát ngay.</summary>
    public async Task SpeakAsync(string text, CancellationToken cancelToken = default)
    {
        ForceLoadText(text);
        await SpeakAsync(cancelToken);
    }

    /// <summary>Tạm dừng phát (Android: dừng engine; iOS/Windows: cancel).</summary>
    public void Pause()
    {
        _isPaused = true;
#if ANDROID
        StopNativeAndroid();
#endif
        Debug.WriteLine("[TTS] Paused");
    }

    /// <summary>Dừng hẳn và reset tiến trình về 0.</summary>
    public void Stop()
    {
        _isPaused = false;
        _finished = false;
        _charIndex = 0;
#if ANDROID
        StopNativeAndroid();
#endif
        Debug.WriteLine("[TTS] Stopped");
    }

    // ── INTERNAL – MAUI FALLBACK (iOS / Windows) ──────────────────────────────

    private async Task SpeakMauiAsync(string lang, CancellationToken cancelToken)
    {
        var locales = await TextToSpeech.GetLocalesAsync();
        var locale = await ResolveLocaleAsync(lang, locales);

        // Tính toán text cần đọc (hỗ trợ resume từ _charIndex)
        string textToSpeak = _charIndex > 0 && _charIndex < _currentText.Length
            ? _currentText[_charIndex..]
            : _currentText;

        int total = _currentText.Length;

        // Báo progress thủ công vì MAUI không có callback ký tự
        // Estimate: cập nhật mỗi 500ms dựa trên thời gian trôi qua
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        _ = Task.Run(async () =>
        {
            // Ước tính tốc độ đọc: ~15 ký tự/giây
            const double charsPerMs = 15.0 / 1000.0;
            int reported = _charIndex;
            while (!cts.Token.IsCancellationRequested && reported < total)
            {
                await Task.Delay(300, cts.Token).ConfigureAwait(false);
                reported = Math.Min(reported + (int)(300 * charsPerMs), total);
                _charIndex = reported;
                OnProgress?.Invoke(reported, total);
            }
        }, cts.Token);

        await TextToSpeech.SpeakAsync(textToSpeak, new SpeechOptions { Locale = locale }, cancelToken);
        await cts.CancelAsync();
    }

    private static async Task<Locale?> ResolveLocaleAsync(string lang, IEnumerable<Locale> locales)
    {
        var localeList = locales.ToList();
        string normalizedLang = lang.ToLowerInvariant();

        // 1. Thử khớp theo danh sách ưu tiên (nếu có)
        if (LocalePriority.TryGetValue(normalizedLang, out var preferred))
        {
            foreach (var pref in preferred)
            {
                var byName = localeList.FirstOrDefault(l =>
                    l.Name != null && l.Name.Contains(pref, StringComparison.OrdinalIgnoreCase));
                if (byName != null) return byName;

                var parts = pref.Split('-');
                var byCode = localeList.FirstOrDefault(l =>
                    l.Language.Equals(parts[0], StringComparison.OrdinalIgnoreCase) &&
                    (parts.Length <= 1 || l.Country?.Equals(parts[1], StringComparison.OrdinalIgnoreCase) == true));
                if (byCode != null) return byCode;
            }
        }

        // 2. Thử khớp theo mã ngôn ngữ chính (vd: "ko" -> bất kỳ locale nào có Language == "ko")
        var byLangCode = localeList.FirstOrDefault(l =>
            l.Language.Equals(normalizedLang, StringComparison.OrdinalIgnoreCase));
        if (byLangCode != null) return byLangCode;

        // 3. Fallback: tìm bất kỳ locale nào có tên chứa mã ngôn ngữ
        var fallback = localeList.FirstOrDefault(l =>
            l.Language.StartsWith(normalizedLang, StringComparison.OrdinalIgnoreCase) ||
            (l.Name != null && l.Name.Contains(normalizedLang, StringComparison.OrdinalIgnoreCase)));

        if (fallback == null)
            Debug.WriteLine($"[TTS] No locale found for lang='{lang}', using system default");

        return await Task.FromResult(fallback);
    }

    // ── ANDROID PARTIAL DECLARATIONS ─────────────────────────────────────────
#if ANDROID
    private partial Task SpeakNativeAndroidAsync(string lang, CancellationToken cancelToken);
    private partial void StopNativeAndroid();
#endif
}