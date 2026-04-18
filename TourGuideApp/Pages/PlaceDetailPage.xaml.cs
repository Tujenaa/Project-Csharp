using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.ViewModels;
using TourGuideApp.Utils;

namespace TourGuideApp.Pages;

[QueryProperty(nameof(Poi), "poi")]
public partial class PlaceDetailPage : ContentPage
{
    // ── Services ──────────────────────────────────────────────────────────────
    readonly TextToSpeechService ttsService = new();

    // ── TTS state ─────────────────────────────────────────────────────────────
    // Logic đã chuyển sang AudioPlaybackService
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
        
        // Cập nhật UI animation dựa trên trạng thái phát từ Service
        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    private void OnPlaybackStateChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_currentPoi != null)
            {
                bool playing = _currentPoi.IsPlaying;
                UpdateUI(playing);
                if (playing) StartWaveAnimation();
                else StopWaveAnimation();
            }
        });
    }



    // ── Lifecycle ─────────────────────────────────────────────────────────────
    // Logic OnTtsProgress đã chuyển sang AudioPlaybackService

    readonly LocationService locationService = new();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Cập nhật vị trí và khoảng cách khi load trang
        if (_currentPoi != null)
        {
            var loc = await locationService.GetCurrentLocationAsync();
            if (loc != null)
            {
                DistanceUtils.UpdatePoiDistance(_currentPoi, loc.Latitude, loc.Longitude);
            }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Không dừng hẳn, chỉ tháo event để tránh leak
        AudioPlaybackService.Instance.PlaybackStateChanged -= OnPlaybackStateChanged;
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
        AudioPlaybackService.Instance.Pause();
    }

    private void OnStopTapped(object sender, EventArgs e)
    {
        AudioPlaybackService.Instance.Stop();
    }

    private async void OnPlayTapped(object sender, EventArgs e)
    {
        if (BindingContext is not POI poi) return;
        await AudioPlaybackService.Instance.PlayAsync(poi);
    }

    // ── TTS callbacks ─────────────────────────────────────────────────────────
    // Logic đã chuyển sang AudioPlaybackService

    // ── Helpers ───────────────────────────────────────────────────────────────

    void StopPlayback()
    {
        AudioPlaybackService.Instance.Stop();
    }

    void UpdateUI(bool playing)
    {
        if (_currentPoi == null) return;
        _currentPoi.IsPlaying = playing;

        if (!playing && _currentPoi.AudioProgress >= 0.99)
        {
            lblAudioStatus.Text = LocalizationService.Get("listen_again");
        }
        else
        {
            lblAudioStatus.Text = playing ? 
                LocalizationService.Get("listening_now") : 
                LocalizationService.Get("listen_narration");
        }
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