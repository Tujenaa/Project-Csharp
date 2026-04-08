using TourGuideApp.Models;
using TourGuideApp.Services;
using BarcodeScanner.Mobile;

namespace TourGuideApp.Pages;

public partial class QrScannerPage : ContentPage
{
    private readonly ApiService _apiService = new();
    private readonly TextToSpeechService _ttsService = new();
    private CancellationTokenSource? _ttsToken;

    private bool _isProcessingResult = false;
    private bool _isPaused = false;

    private POI? _scannedPoi;
    public POI? ScannedPoi
    {
        get => _scannedPoi;
        set { _scannedPoi = value; OnPropertyChanged(); }
    }

    public QrScannerPage()
    {
        InitializeComponent();
        BindingContext = this;

        _ttsService.OnFinished += OnTtsFinished;
        _ttsService.OnProgress += OnTtsProgress;

        StartScanningAnimation();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Quyền truy cập", "Ứng dụng cần quyền Camera để quét mã QR.", "OK");
            await Navigation.PopAsync();
        }
        else
        {
            await Task.Delay(500);
            BarcodeReader.IsScanning = true;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReader.IsScanning = false;
        StopAudio();
    }

    // ── Scanner animation ─────────────────────────────────────────────────────

    private void StartScanningAnimation()
    {
        var animation = new Animation(v => ScannerLine.TranslationY = v, 0, 224);
        animation.Commit(this, "ScannerAnimation", 16, 2000, Easing.Linear,
            (v, c) => ScannerLine.TranslationY = 0,
            () => !_isProcessingResult);
    }

    // ── Barcode detected ──────────────────────────────────────────────────────

    private async void OnBarcodesDetected(object sender, OnDetectedEventArg e)
    {
        if (_isProcessingResult) return;

        var barcodes = e.BarcodeResults;
        if (barcodes == null || barcodes.Count == 0) return;

        var first = barcodes.FirstOrDefault();
        if (first == null) return;

        string code = first.DisplayValue;
        if (!code.StartsWith("https://poi:")) return;

        _isProcessingResult = true;

        await Dispatcher.DispatchAsync(async () =>
        {
            BarcodeReader.IsScanning = false;

            await Task.WhenAll(
                ScannerFrame.ScaleTo(0.8, 250, Easing.CubicOut),
                ScannerFrame.FadeTo(0, 250),
                ScannerLine.FadeTo(0, 250));

            string idStr = code.Replace("https://poi:", "");
            if (int.TryParse(idStr, out int id))
                await ProcessScannedId(id);
            else
                ResetScanner();
        });
    }

    private async Task ProcessScannedId(int id)
    {
        var poi = await _apiService.GetPOIById(id);
        if (poi != null)
        {
            ScannedPoi = poi;
            ShowResultCard();       // hiện card ngay lập tức (không await)
            await PlayAudioFromStart();
        }
        else
        {
            await DisplayAlert("Không tìm thấy", "Không có thông tin cho mã QR này.", "OK");
            ResetScanner();
        }
    }

    // ── Show / hide card ──────────────────────────────────────────────────────

    /// Hiện card ngay — Grid Row="Auto" tự tính chiều cao, không cần animation phức tạp
    private void ShowResultCard()
    {
        ResultCard.Opacity = 0;
        ResultCard.IsVisible = true;
        ResultCard.FadeTo(1, 300, Easing.SinOut);   // fade in nhẹ
    }

    /// Ẩn card với fade out
    private async Task HideResultCard()
    {
        await ResultCard.FadeTo(0, 220, Easing.SinIn);
        ResultCard.IsVisible = false;
    }

    private void ResetScanner()
    {
        _isProcessingResult = false;
        _isPaused = false;
        ScannerFrame.Scale = 1;
        ScannerFrame.Opacity = 1;
        ScannerLine.Opacity = 1;
        BarcodeReader.IsScanning = true;
        StartScanningAnimation();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private async void OnBackClicked(object sender, EventArgs e)
    {
        _isProcessingResult = true;
        BarcodeReader.IsScanning = false;
        StopAudio();
        await Navigation.PopAsync();
    }

    private async void OnCloseResultClicked(object sender, EventArgs e)
    {
        StopAudio();
        await HideResultCard();
        ScannedPoi = null;
        ResetScanner();
    }

    private async void OnDetailClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null) return;
        StopAudio();
        await Shell.Current.GoToAsync("placeDetail", new Dictionary<string, object>
        {
            { "poi", ScannedPoi }
        });
    }

    // ── Audio: 3 nút riêng ────────────────────────────────────────────────────

    private async void OnPlayAudioClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null) return;
        if (_isPaused)
            await ResumeAudio();
        else
            await PlayAudioFromStart();
    }

    private void OnPauseAudioClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null || !ScannedPoi.IsPlaying) return;
        _ttsToken?.Cancel();
        _ttsService.Pause();
        _isPaused = true;
        ScannedPoi.IsPlaying = false; // DataTrigger: ẩn Pause+Stop, hiện Play
    }

    private void OnStopAudioClicked(object sender, EventArgs e) => StopAudio();

    // ── Audio core ────────────────────────────────────────────────────────────

    private async Task PlayAudioFromStart()
    {
        if (ScannedPoi == null) return;
        _isPaused = false;
        _ttsService.ForceLoadText(GetScriptForLang(ScannedPoi, SettingService.Instance.Language));
        await RunTts();
    }

    private async Task ResumeAudio()
    {
        if (ScannedPoi == null) return;
        _isPaused = false;
        await RunTts(); // không ForceLoadText → _charIndex còn nguyên
    }

    private async Task RunTts()
    {
        if (ScannedPoi == null) return;

        ScannedPoi.IsPlaying = true; // DataTrigger: ẩn Play, hiện Pause+Stop

        _ttsToken?.Cancel();
        _ttsToken?.Dispose();
        _ttsToken = new CancellationTokenSource();

        try
        {
            await _ttsService.SpeakAsync(_ttsToken.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QRScan] TTS Error: {ex.Message}");
        }
        finally
        {
            if (ScannedPoi != null && !_isPaused)
                ScannedPoi.IsPlaying = false;
        }
    }

    private void StopAudio()
    {
        _ttsToken?.Cancel();
        _ttsToken?.Dispose();
        _ttsToken = null;
        _ttsService.Stop();
        _isPaused = false;

        if (ScannedPoi == null) return;
        ScannedPoi.IsPlaying = false;
        ScannedPoi.AudioProgress = 0;
    }

    // ── TTS callbacks ─────────────────────────────────────────────────────────

    private void OnTtsProgress(int current, int total)
    {
        if (ScannedPoi == null || total == 0) return;
        MainThread.BeginInvokeOnMainThread(() =>
            ScannedPoi.AudioProgress = (double)current / total);
    }

    private void OnTtsFinished()
    {
        if (ScannedPoi == null) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _isPaused = false;
            ScannedPoi.IsPlaying = false;
            ScannedPoi.AudioProgress = 1.0;
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetScriptForLang(POI poi, string lang) =>
        lang switch
        {
            "en" => string.IsNullOrWhiteSpace(poi.ScriptEn) ? null : poi.ScriptEn,
            "ja" => string.IsNullOrWhiteSpace(poi.ScriptJa) ? null : poi.ScriptJa,
            "zh" => string.IsNullOrWhiteSpace(poi.ScriptZh) ? null : poi.ScriptZh,
            _ => string.IsNullOrWhiteSpace(poi.ScriptVi) ? null : poi.ScriptVi,
        } ?? "Không có dữ liệu thuyết minh.";
}