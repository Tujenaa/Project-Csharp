using Microsoft.Maui.Media;
using TourGuideApp.Services;

/// <summary>
/// TTS service cho MAUI — logic chọn giọng đồng bộ với web admin (pickVoice).
///
/// Web admin ưu tiên:
///   vi → Google Vietnamese > Microsoft NamMinh Online > HoaiMy Online
///   en → Google US English  > Microsoft Aria Online   > Guy Online
///   ja → Google 日本語       > Microsoft Nanami Online
///   zh → Google 普通话       > Microsoft Xiaoxiao Online
///
/// MAUI Locale map:
///   vi → vi-VN   en → en-US   ja → ja-JP   zh → zh-CN  (và các biến thể)
///
/// Pause/Resume:
///   MAUI TTS không có native pause/resume → ta chia text thành CÁC CÂU,
///   track sentenceIndex. Pause = cancel + giữ index. Resume = tiếp tục từ index đó.
///   Nếu đã phát hết (IsFinished) → lần phát tiếp sẽ reset từ câu 0 (replay).
/// </summary>
public class TextToSpeechService
{
    // ── Sentence-level state ──────────────────────────────────────────────────
    private List<string> _sentences = new();
    private int _sentenceIndex = 0;
    private bool _finished = false;

    public bool IsFinished => _finished;

    // ── Voice priority table (mirror của pickVoice trong web admin) ───────────
    // key = language code, value = preferred locale codes theo thứ tự ưu tiên
    private static readonly Dictionary<string, string[]> LocalePriority = new()
    {
        ["vi"] = new[] { "vi-VN" },
        ["en"] = new[] { "en-US", "en-GB", "en-AU" },
        ["ja"] = new[] { "ja-JP" },
        ["zh"] = new[] { "zh-CN", "zh-TW", "zh-HK" },
        ["fr"] = new[] { "fr-FR", "fr-CA" },
        ["ko"] = new[] { "ko-KR" },
        ["de"] = new[] { "de-DE" },
        ["es"] = new[] { "es-ES", "es-MX" },
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Load text mới → reset toàn bộ trạng thái sentence.
    /// Gọi khi bắt đầu play một POI mới (Play mới, không phải Resume).
    /// </summary>
    public void LoadText(string text)
    {
        _sentences = SplitToSentences(text);
        _sentenceIndex = 0;
        _finished = false;
        System.Diagnostics.Debug.WriteLine(
            $"[TTS] LoadText: {_sentences.Count} sentences");
    }

    /// <summary>
    /// Phát từ sentenceIndex hiện tại.
    /// - Nếu IsFinished → reset về câu 0 trước khi phát (replay).
    /// - Nếu cancel token bị trigger (pause) → dừng, giữ nguyên sentenceIndex.
    /// </summary>
    public async Task SpeakAsync(CancellationToken cancelToken = default)
    {
        if (_sentences.Count == 0) return;

        // Đã hết → replay từ đầu
        if (_finished)
        {
            _sentenceIndex = 0;
            _finished = false;
            System.Diagnostics.Debug.WriteLine("[TTS] Replay from beginning");
        }

        var lang = SettingService.Instance.Language;
        var locale = await ResolveLocaleAsync(lang);

        System.Diagnostics.Debug.WriteLine(
            $"[TTS] Starting at sentence {_sentenceIndex}/{_sentences.Count}, " +
            $"lang={lang}, locale={locale?.Language}-{locale?.Country}");

        while (_sentenceIndex < _sentences.Count)
        {
            if (cancelToken.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TTS] Paused at sentence {_sentenceIndex}");
                return; // Giữ nguyên index để resume tiếp tục
            }

            var sentence = _sentences[_sentenceIndex];
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                try
                {
                    await TextToSpeech.SpeakAsync(sentence, new SpeechOptions
                    {
                        Locale = locale,
                        Pitch = 1.0f,
                        Volume = 1.0f
                    }, cancelToken);
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[TTS] Paused mid-sentence {_sentenceIndex}");
                    return;
                }
            }

            if (cancelToken.IsCancellationRequested) return;

            _sentenceIndex++;
        }

        // Phát xong toàn bộ
        _finished = true;
        System.Diagnostics.Debug.WriteLine("[TTS] Finished all sentences");
    }

    /// <summary>
    /// Overload tiện lợi: load text mới rồi phát ngay.
    /// Dùng khi Play mới (không phải Resume).
    /// </summary>
    public async Task SpeakAsync(string text, CancellationToken cancelToken = default)
    {
        LoadText(text);
        await SpeakAsync(cancelToken);
    }

    // ── Voice resolution (tương đương pickVoice của web) ─────────────────────

    private async Task<Locale?> ResolveLocaleAsync(string lang)
    {
        var locales = await TextToSpeech.GetLocalesAsync();

        // 1. Tìm theo bảng ưu tiên (mirror pickVoice)
        if (LocalePriority.TryGetValue(lang.ToLowerInvariant(), out var preferred))
        {
            foreach (var code in preferred)
            {
                var parts = code.Split('-');
                var langPart = parts[0];
                var countryPart = parts.Length > 1 ? parts[1] : "";

                var match = locales.FirstOrDefault(l =>
                    l.Language.Equals(langPart, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(countryPart) ||
                     l.Country?.Equals(countryPart, StringComparison.OrdinalIgnoreCase) == true));

                if (match != null) return match;
            }
        }

        // 2. Fallback: bất kỳ locale nào có prefix ngôn ngữ khớp
        var fallback = locales.FirstOrDefault(l =>
            l.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase));
        if (fallback != null) return fallback;

        // 3. Ultimate fallback: tiếng Việt (giống web khi không tìm được voice)
        return locales.FirstOrDefault(l =>
            l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase));
    }

    // ── Text splitting ────────────────────────────────────────────────────────

    /// <summary>
    /// Chia văn bản thành danh sách câu.
    /// Tách theo dấu câu kết thúc: . ! ? và newline.
    /// </summary>
    private static List<string> SplitToSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var raw = System.Text.RegularExpressions.Regex.Split(
            text.Trim(),
            @"(?<=[.!?\n])\s+"
        );

        return raw
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}