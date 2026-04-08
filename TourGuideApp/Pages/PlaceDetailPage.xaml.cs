using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

[QueryProperty(nameof(Poi), "poi")]
public partial class PlaceDetailPage : ContentPage
{
    // ── Services ──────────────────────────────────────────────────────────────
    readonly TextToSpeechService ttsService = new();

    // ── TTS state ─────────────────────────────────────────────────────────────
    CancellationTokenSource? ttsToken;
    bool isPlaying = false;
    bool _isPaused = false;

    // Ngôn ngữ đang được load trong ttsService
    string _currentLoadedLang = "";

    // POI hiện tại (lưu để ghi lịch sử)
    POI? _currentPoi;

    // ── Waveform bars ─────────────────────────────────────────────────────────
    BoxView[]? _bars;
    static readonly double[] BarPeaks = { 10, 24, 16, 28, 12, 22, 18 };
    static readonly int[] BarDelays = { 0, 80, 160, 40, 200, 100, 140 };
    CancellationTokenSource? _waveCts;

    // ── POI binding ───────────────────────────────────────────────────────────
    public POI? Poi
    {
        set
        {
            _currentPoi = value;
            BindingContext = value;

            // Reset TTS state khi chuyển sang POI mới
            StopPlayback();
            _currentLoadedLang = "";
        }
    }

    public PlaceDetailPage()
    {
        InitializeComponent();
        _bars = new[] { bar0, bar1, bar2, bar3, bar4, bar5, bar6 };

        ttsService.OnFinished += OnTtsFinished;
        ttsService.OnProgress += OnTtsProgress;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnTtsProgress(int current, int total)
    {
        if (_currentPoi == null || total == 0) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentPoi.AudioProgress = (double)current / total;
            _currentPoi.AudioDuration = $"{current}/{total} chữ";
        });
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

    // ── Audio controls ────────────────────────────────────────────────────────

    private void OnPauseTapped(object sender, EventArgs e)
    {
        if (!isPlaying) return;

        // FIX: Gọi cả Cancel token lẫn ttsService.Pause() để đồng bộ trạng thái
        ttsToken?.Cancel();
        ttsService.Pause();

        isPlaying = false;
        _isPaused = true;
        UpdateUI(playing: false);
        StopWaveAnimation();
    }

    private void OnStopTapped(object sender, EventArgs e)
    {
        StopPlayback();
    }

    private async void OnPlayTapped(object sender, EventArgs e)
    {
        if (BindingContext is not POI poi) return;

        // ── Sync script từ API nếu có mạng ───────────────────────────────────
        if (ConnectivityService.IsConnected)
        {
            var apiService = new ApiService();
            var freshPoi = await apiService.GetPOIById(poi.Id);
            if (freshPoi != null)
            {
                poi.ScriptVi = freshPoi.ScriptVi;
                poi.ScriptEn = freshPoi.ScriptEn;
                poi.ScriptJa = freshPoi.ScriptJa;
                poi.ScriptZh = freshPoi.ScriptZh;
                System.Diagnostics.Debug.WriteLine(
                    $"[PlaceDetail] Đã tải kịch bản mới nhất từ API cho POI {poi.Id}");
            }
        }

        if (isPlaying) return;

        string langNow = SettingService.Instance.Language;

        // ── RESUME: đã pause, cùng ngôn ngữ → tiếp tục từ vị trí đang dừng ──
        if (_isPaused && langNow == _currentLoadedLang)
        {
            _isPaused = false;
            isPlaying = true;
            UpdateUI(playing: true);
            StartWaveAnimation();

            // FIX: Dispose token cũ trước khi tạo mới
            ttsToken?.Dispose();
            ttsToken = new CancellationTokenSource();
            try
            {
                // Không ForceLoadText → giữ nguyên _charIndex để resume đúng chỗ
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

        // ── PLAY MỚI hoặc đổi ngôn ngữ sau khi pause ────────────────────────
        _isPaused = false;

        string freshText = GetScriptForLang(poi, langNow);

        if (langNow != _currentLoadedLang || string.IsNullOrEmpty(_currentLoadedLang))
        {
            // Ngôn ngữ khác trước → LoadText bình thường (reset về đầu)
            ttsService.LoadText(freshText);
        }
        else
        {
            // FIX: Cùng ngôn ngữ, bấm play lại sau khi đã phát xong
            // → ForceLoadText để buộc reset _charIndex về 0, tránh guard chặn
            ttsService.ForceLoadText(freshText);
        }
        _currentLoadedLang = langNow;

        isPlaying = true;
        UpdateUI(playing: true);
        StartWaveAnimation();

        // FIX: Dispose token cũ trước khi tạo mới
        ttsToken?.Dispose();
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

    // ── TTS callbacks ─────────────────────────────────────────────────────────

    /// Được gọi từ TextToSpeechService.OnFinished (có thể từ background thread)
    void OnTtsFinished()
    {
        if (_currentPoi == null) return;

        System.Diagnostics.Debug.WriteLine(
            $"[PlaceDetail] TTS finished → saving history for {_currentPoi.Name}");

        _ = HistoryStore.AddAsync(_currentPoi);

        // FIX: Cập nhật cả isPlaying/_isPaused trên main thread (trước đây chỉ gọi UpdateUI)
        MainThread.BeginInvokeOnMainThread(() =>
        {
            isPlaying = false;
            _isPaused = false;
            UpdateUI(playing: false);
            StopWaveAnimation();
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void StopPlayback()
    {
        ttsToken?.Cancel();
        ttsToken?.Dispose();
        ttsToken = null;

        // FIX: Gọi ttsService.Stop() thay vì LoadText("")
        // Stop() reset _charIndex về 0 và không set text rác "Không có dữ liệu."
        ttsService.Stop();

        isPlaying = false;
        _isPaused = false;
        UpdateUI(playing: false);
        StopWaveAnimation();
    }

    void UpdateUI(bool playing)
    {
        if (_currentPoi != null) _currentPoi.IsPlaying = playing;

        if (!playing && ttsService.IsFinished)
            lblAudioStatus.Text = "Nghe lại";
        else
            lblAudioStatus.Text = playing ? "Đang phát âm thanh..." : "Nghe thuyết minh";
    }

    private static string GetScriptForLang(POI poi, string lang)
    {
        string? script = lang switch
        {
            "en" => poi.ScriptEn,
            "ja" => poi.ScriptJa,
            "zh" => poi.ScriptZh,
            _ => poi.ScriptVi
        };
        return string.IsNullOrWhiteSpace(script) ? "Không có dữ liệu thuyết minh." : script;
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