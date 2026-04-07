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
    private POI? _scannedPoi;

    public POI? ScannedPoi
    {
        get => _scannedPoi;
        set 
        { 
            _scannedPoi = value; 
            OnPropertyChanged(); 
        }
    }

    public QrScannerPage()
    {
        InitializeComponent();
        BindingContext = this;

        _ttsService.OnFinished += OnTtsFinished;
        _ttsService.OnProgress += OnTtsProgress;

        StartScanningAnimation();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Quyền truy cập", "Ứng dụng cần quyền Camera để quét mã QR.", "OK");
            await Navigation.PopAsync();
        }
        else
        {
            // Bật Camera với ML Kit
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

    private void StartScanningAnimation()
    {
        var animation = new Animation(v => ScannerLine.TranslationY = v, 0, 220);
        animation.Commit(this, "ScannerAnimation", 16, 2000, Easing.Linear, (v, c) => ScannerLine.TranslationY = 0, () => !_isProcessingResult);
    }

    private async void OnBarcodesDetected(object sender, OnDetectedEventArg e)
    {
        if (_isProcessingResult) return;

        var barcodes = e.BarcodeResults;
        if (barcodes == null || barcodes.Count == 0) return;

        var first = barcodes.FirstOrDefault();
        if (first == null) return;

        // Định dạng mong đợi: https://poi:1
        string code = first.DisplayValue;
        if (!code.StartsWith("https://poi:")) return;

        _isProcessingResult = true;
        
        await Dispatcher.DispatchAsync(async () =>
        {
            // Tắt bộ quét ngay lập tức
            BarcodeReader.IsScanning = false;

            // Hiệu ứng "Zoom" lại khung quét (thu nhỏ và mờ dần)
            await Task.WhenAll(
                ScannerFrame.ScaleTo(0.7, 300, Easing.CubicOut),
                ScannerFrame.FadeTo(0, 300),
                ScannerLine.FadeTo(0, 300)
            );

            string idStr = code.Replace("https://poi:", "");
            if (int.TryParse(idStr, out int id))
            {
                await ProcessScannedId(id);
            }
            else
            {
                // Nếu sai định dạng sâu bên trong thì reset
                ResetScanner();
            }
        });
    }

    private async Task ProcessScannedId(int id)
    {
        var poi = await _apiService.GetPOIById(id);
        if (poi != null)
        {
            ScannedPoi = poi;
            // Hiển thị thẻ kết quả
            await ResultCard.TranslateTo(0, 0, 400, Easing.SinOut);
            // Tự động phát âm thanh
            await StartAudio();
        }
        else
        {
            // Không tìm thấy POI -> bật lại quét
            ResetScanner();
        }
    }

    private void ResetScanner()
    {
        _isProcessingResult = false;
        ScannerFrame.Scale = 1;
        ScannerFrame.Opacity = 1;
        ScannerLine.Opacity = 1;
        BarcodeReader.IsScanning = true;
    }

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
        await ResultCard.TranslateTo(0, 400, 300, Easing.SinIn);
        ScannedPoi = null;
        ResetScanner();
    }

    // ── AUDIO LOGIC ──────────────────────────────────────────────────────────

    private async void OnPlayPauseAudioClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null) return;

        if (ScannedPoi.IsPlaying)
        {
            PauseAudio();
        }
        else
        {
            await StartAudio();
        }
    }

    private void OnStopAudioClicked(object sender, EventArgs e)
    {
        StopAudio();
    }

    private async Task StartAudio()
    {
        if (ScannedPoi == null) return;

        string lang = SettingService.Instance.Language;
        string text = GetScriptForLang(ScannedPoi, lang);

        ScannedPoi.IsPlaying = true;
        PlayPauseIcon.Source = "ic_white_pause.svg";

        _ttsToken?.Cancel();
        _ttsToken = new CancellationTokenSource();

        try
        {
            _ttsService.LoadText(text);
            await _ttsService.SpeakAsync(_ttsToken.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QRScan] TTS Error: {ex.Message}");
        }
    }

    private void PauseAudio()
    {
        if (ScannedPoi == null) return;
        _ttsToken?.Cancel();
        ScannedPoi.IsPlaying = false;
        PlayPauseIcon.Source = "ic_white_play.svg";
    }

    private void StopAudio()
    {
        if (ScannedPoi == null) return;
        _ttsToken?.Cancel();
        ScannedPoi.IsPlaying = false;
        ScannedPoi.AudioProgress = 0;
        PlayPauseIcon.Source = "ic_white_play.svg";
        _ttsService.LoadText("");
    }

    private void OnTtsProgress(int current, int total)
    {
        if (ScannedPoi == null || total == 0) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ScannedPoi.AudioProgress = (double)current / total;
        });
    }

    private void OnTtsFinished()
    {
        if (ScannedPoi == null) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ScannedPoi.IsPlaying = false;
            PlayPauseIcon.Source = "ic_white_play.svg";
            ScannedPoi.AudioProgress = 1.0;
        });
    }

    private string GetScriptForLang(POI poi, string lang)
    {
        string? script = lang switch
        {
            "en" => poi.ScriptEn,
            "ja" => poi.ScriptJa,
            "zh" => poi.ScriptZh,
            _ => poi.ScriptVi
        };
        return string.IsNullOrWhiteSpace(script) ? "Không có dữ liệu thuyết minh" : script;
    }

    private async void OnDetailClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null) return;
        StopAudio();
        
        // Navigation consistent with HomeViewModel
        await Shell.Current.GoToAsync("placeDetail", new Dictionary<string, object>
        {
            { "poi", ScannedPoi }
        });
    }
}
