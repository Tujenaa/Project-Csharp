using TourGuideApp.Models;
using TourGuideApp.Services;

namespace TourGuideApp.Pages;

[QueryProperty(nameof(Poi), "poi")]
public partial class PlaceDetailPage : ContentPage
{
    // ── Services ──────────────────────────────────────────────────────────────
    readonly TextToSpeechService ttsService = new();
    readonly TranslateService translateService = new();

    // ── TTS state (mirror MapViewModel / HomeViewModel) ───────────────────────
    CancellationTokenSource? ttsToken;
    bool isPlaying = false;   // đang chạy TTS
    bool _isPaused = false;  // đã pause (có vị trí câu đang giữ)

    // Ngôn ngữ đang được load trong ttsService
    // Dùng để detect khi user đổi ngôn ngữ giữa chừng
    string _currentLoadedLang = "";

    // Cache bản dịch: key = lang, tránh dịch lại khi resume
    readonly Dictionary<string, string> translationCache = new();

    // ── Waveform bars ─────────────────────────────────────────────────────────
    BoxView[]? _bars;
    static readonly double[] BarPeaks = { 10, 24, 16, 28, 12, 22, 18 };
    static readonly int[] BarDelays = { 0, 80, 160, 40, 200, 100, 140 };
    CancellationTokenSource? _waveCts;

    // ── POI binding ───────────────────────────────────────────────────────────
    public POI? Poi
    {
        set { BindingContext = value; }
    }

    public PlaceDetailPage()
    {
        InitializeComponent();
        _bars = new[] { bar0, bar1, bar2, bar3, bar4, bar5, bar6 };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPlayback();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await this.FadeTo(0.5, 100);
        await Shell.Current.GoToAsync("..");
    }

    // ── Play button handler ───────────────────────────────────────────────────

    private async void OnPlayTapped(object sender, EventArgs e)
    {
        if (BindingContext is not POI poi) return;

        string langNow = SettingService.Instance.Language;

        // ── PAUSE: đang phát → dừng, ghi nhớ vị trí câu ─────────────────────
        if (isPlaying)
        {
            ttsToken?.Cancel();
            isPlaying = false;
            _isPaused = true;
            UpdateUI(playing: false);
            StopWaveAnimation();
            // ttsService giữ nguyên _sentenceIndex → resume tiếp tục đúng chỗ
            return;
        }

        // ── RESUME: đã pause cùng ngôn ngữ → tiếp tục từ câu đang dừng ──────
        if (_isPaused && langNow == _currentLoadedLang)
        {
            _isPaused = false;
            isPlaying = true;
            UpdateUI(playing: true);
            StartWaveAnimation();

            ttsToken = new CancellationTokenSource();
            try
            {
                // Không LoadText → giữ nguyên _sentenceIndex (tiếp tục)
                // Nếu IsFinished == true → tự reset → replay
                await ttsService.SpeakAsync(ttsToken.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaceDetail] RESUME ERROR: {ex.Message}");
            }

            isPlaying = false;
            UpdateUI(playing: false);
            StopWaveAnimation();
            return;
        }

        // ── PLAY MỚI hoặc đổi ngôn ngữ sau pause ────────────────────────────
        // (lần đầu bấm / POI khác / ngôn ngữ thay đổi)
        _isPaused = false;

        if (langNow != _currentLoadedLang || string.IsNullOrEmpty(_currentLoadedLang))
        {
            string freshText = await GetTranslatedTextAsync(poi, langNow);
            ttsService.LoadText(freshText);
            _currentLoadedLang = langNow;
        }
        else
        {
            // Cùng ngôn ngữ, play lại từ đầu
            ttsService.LoadText(await GetTranslatedTextAsync(poi, langNow));
        }

        isPlaying = true;
        UpdateUI(playing: true);
        StartWaveAnimation();

        ttsToken = new CancellationTokenSource();
        try
        {
            await ttsService.SpeakAsync(ttsToken.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlaceDetail] TTS ERROR: {ex.Message}");
        }

        isPlaying = false;
        UpdateUI(playing: false);
        StopWaveAnimation();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void StopPlayback()
    {
        ttsToken?.Cancel();
        isPlaying = false;
        _isPaused = false;
        ttsService.LoadText(""); // reset sentence index khi stop hẳn
        UpdateUI(playing: false);
        StopWaveAnimation();
    }

    void UpdateUI(bool playing)
    {
        imgPlayIcon.Source = playing ? "ic_pause.svg" : "ic_white_play.svg";
        lblAudioStatus.Text = playing ? "Đang phát âm thanh..." : "Nghe thuyết minh";
    }

    /// <summary>
    /// Lấy text đã dịch; cache theo lang để không dịch lại khi resume.
    /// Nếu lang == "vi" trả về text gốc ngay (không gọi mạng).
    /// </summary>
    async Task<string> GetTranslatedTextAsync(POI poi, string lang)
    {
        string originalText =
            !string.IsNullOrWhiteSpace(poi.Script) ? poi.Script :
            !string.IsNullOrWhiteSpace(poi.Description) ? poi.Description :
            "Không có dữ liệu";

        if (lang == "vi") return originalText;

        if (translationCache.TryGetValue(lang, out var cached))
        {
            System.Diagnostics.Debug.WriteLine($"[PlaceDetail] Cache hit for lang={lang}");
            return cached;
        }

        System.Diagnostics.Debug.WriteLine($"[PlaceDetail] Translating to {lang}...");
        string translated = await translateService.TranslateWithRetryAsync(originalText, lang);
        translationCache[lang] = translated;
        return translated;
    }

    // ── Waveform animation ────────────────────────────────────────────────────

    void StartWaveAnimation()
    {
        _waveCts?.Cancel();
        _waveCts = new CancellationTokenSource();
        var token = _waveCts.Token;
        if (_bars == null) return;
        for (int i = 0; i < _bars.Length; i++)
            _ = AnimateBarAsync(_bars[i], BarPeaks[i], BarDelays[i], token);
    }

    void StopWaveAnimation()
    {
        _waveCts?.Cancel();
        if (_bars == null) return;
        foreach (var bar in _bars)
        {
            bar.CancelAnimations();
            bar.HeightRequest = 4;
        }
    }

    static async Task AnimateBarAsync(
        BoxView bar, double peak, int delayMs, CancellationToken token)
    {
        try
        {
            await Task.Delay(delayMs, token);
            while (!token.IsCancellationRequested)
            {
                await AnimateHeight(bar, peak, 200, token);
                await AnimateHeight(bar, 4, 200, token);
            }
        }
        catch (OperationCanceledException) { }
    }

    static Task AnimateHeight(
        BoxView box, double target, uint ms, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>();
        if (token.IsCancellationRequested) { tcs.SetCanceled(); return tcs.Task; }

        string name = $"wh_{box.GetHashCode()}";
        double from = box.HeightRequest < 1 ? 4 : box.HeightRequest;

        new Animation(v => box.HeightRequest = v, from, target, Easing.SinInOut)
            .Commit(box, name, 16, ms, finished: (_, __) => tcs.TrySetResult(true));

        token.Register(() =>
        {
            box.AbortAnimation(name);
            tcs.TrySetCanceled();
        });

        return tcs.Task;
    }
}