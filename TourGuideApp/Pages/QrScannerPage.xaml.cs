using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.Utils;
using BarcodeScanner.Mobile;

namespace TourGuideApp.Pages;

public partial class QrScannerPage : ContentPage
{
    private readonly ApiService _apiService = new();
    private bool _isProcessingResult = false;

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

        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;

        StartScanningAnimation();
    }

    private void OnPlaybackStateChanged()
    {
        // UI tự cập nhật qua DataTrigger và Binding với poi.IsPlaying trong XAML
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
            await DisplayAlert(
                LocalizationService.Get("permission_title"), 
                LocalizationService.Get("camera_permission_msg"), 
                LocalizationService.Get("ok"));
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
        AudioPlaybackService.Instance.PlaybackStateChanged -= OnPlaybackStateChanged;
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
        int? id = ParseScannedCode(code);
        if (id == null) return;

        _isProcessingResult = true;

        await Dispatcher.DispatchAsync(async () =>
        {
            BarcodeReader.IsScanning = false;

            await Task.WhenAll(
                ScannerFrame.ScaleTo(0.8, 250, Easing.CubicOut),
                ScannerFrame.FadeTo(0, 250),
                ScannerLine.FadeTo(0, 250));

            if (id.HasValue)
                await ProcessScannedId(id.Value);
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
            ShowResultCard();
            await AudioPlaybackService.Instance.PlayAsync(poi);
        }
        else
        {
            await DisplayAlert(
                LocalizationService.Get("not_found_title"), 
                LocalizationService.Get("qr_not_recognized_msg"), 
                LocalizationService.Get("ok"));
            ResetScanner();
        }
    }

    // ── Show / hide card ──────────────────────────────────────────────────────

    private void ShowResultCard()
    {
        ResultCard.Opacity = 0;
        ResultCard.IsVisible = true;
        ResultCard.FadeTo(1, 300, Easing.SinOut);
    }

    private async Task HideResultCard()
    {
        await ResultCard.FadeTo(0, 220, Easing.SinIn);
        ResultCard.IsVisible = false;
    }

    private void ResetScanner()
    {
        _isProcessingResult = false;
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
        _ = AudioPlaybackService.Instance.StopAsync();
        await Navigation.PopAsync();
    }

    private async void OnCloseResultClicked(object sender, EventArgs e)
    {
        _ = AudioPlaybackService.Instance.StopAsync();
        await HideResultCard();
        ScannedPoi = null;
        ResetScanner();
    }

    private async void OnDetailClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null) return;
        // Không stop âm thanh, để nó tiếp tục phát khi sang trang chi tiết
        await Shell.Current.GoToAsync("placeDetail", new Dictionary<string, object>
        {
            { "poi", ScannedPoi }
        });
    }

    private async void OnDirectionClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null) return;

        // Lưu ID vào MapTourState để MapPage xử lý khi mở lên
        MapTourState.DirectionPoiId = ScannedPoi.Id;

        // Chuyển sang tab Map
        await Shell.Current.GoToAsync("//map");
    }

    private int? ParseScannedCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        // Hỗ trợ định dạng cũ: https://poi:123
        if (code.StartsWith("https://poi:"))
        {
            if (int.TryParse(code.Replace("https://poi:", ""), out int id)) return id;
        }

        // Hỗ trợ định dạng Custom Scheme: tourguideapp://poi/123
        if (code.StartsWith("tourguideapp://poi/"))
        {
            if (int.TryParse(code.Replace("tourguideapp://poi/", ""), out int id)) return id;
        }

        // Hỗ trợ định dạng URL chuẩn: https://tourguide.vn/poi/123
        if (code.StartsWith("https://tourguide.vn/poi/"))
        {
            if (int.TryParse(code.Replace("https://tourguide.vn/poi/", ""), out int id)) return id;
        }

        return null;
    }

    // ── Audio controls ────────────────────────────────────────────────────────

    private async void OnPlayAudioClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null) return;
        await AudioPlaybackService.Instance.PlayAsync(ScannedPoi);
    }

    private void OnPauseAudioClicked(object sender, EventArgs e)
    {
        AudioPlaybackService.Instance.Pause();
    }

    private void OnStopAudioClicked(object sender, EventArgs e)
    {
        _ = AudioPlaybackService.Instance.StopAsync();
    }
}