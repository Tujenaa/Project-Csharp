using TourGuideApp.Models;
using TourGuideApp.Services;

namespace TourGuideApp.Pages;

[QueryProperty(nameof(Poi), "poi")]
public partial class PlaceDetailPage : ContentPage
{
    readonly TextToSpeechService ttsService = new();
    CancellationTokenSource? ttsToken;
    bool isPlaying = false;

    BoxView[]? _bars;

    // Peak heights and animation delays for each of 7 bars
    static readonly double[] BarPeaks = { 10, 24, 16, 28, 12, 22, 18 };
    static readonly int[] BarDelays = { 0, 80, 160, 40, 200, 100, 140 };

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

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await this.FadeTo(0.5, 100);
        await Shell.Current.GoToAsync("..");
    }

    private async void OnPlayTapped(object sender, EventArgs e)
    {
        if (isPlaying)
        {
            StopPlayback();
            return;
        }

        if (BindingContext is not POI poi) return;

        isPlaying = true;
        UpdateUI(playing: true);
        StartWaveAnimation();

        ttsToken = new CancellationTokenSource();
        string text =
         !string.IsNullOrWhiteSpace(poi.Script) ? poi.Script :
         !string.IsNullOrWhiteSpace(poi.Description) ? poi.Description :
         "Không có dữ liệu";

        try
        {
            await ttsService.SpeakAsync(text, ttsToken.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine(ex.Message); }

        isPlaying = false;
        UpdateUI(playing: false);
        StopWaveAnimation();
    }

    void StopPlayback()
    {
        ttsToken?.Cancel();
        isPlaying = false;
        UpdateUI(playing: false);
        StopWaveAnimation();
    }

    void UpdateUI(bool playing)
    {
        imgPlayIcon.Source = playing ? "ic_pause.svg" : "ic_white_play.svg";
        lblAudioStatus.Text = playing ? "Đang phát âm thanh..." : "Nghe thuyết minh";
    }

    // ── Waveform animation ────────────────────────────────────────────────────
    CancellationTokenSource? _waveCts;

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

    static async Task AnimateBarAsync(BoxView bar, double peak, int delayMs, CancellationToken token)
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

    static Task AnimateHeight(BoxView box, double target, uint ms, CancellationToken token)
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