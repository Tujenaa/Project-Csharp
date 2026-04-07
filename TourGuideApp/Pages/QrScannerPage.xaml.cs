using TourGuideApp.Models;
using TourGuideApp.Services;

namespace TourGuideApp.Pages;

public partial class QrScannerPage : ContentPage
{
    private bool _isScanning = true;
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
        StartScanningAnimation();
    }

    private void StartScanningAnimation()
    {
        var animation = new Animation(v => ScannerLine.TranslationY = v, 0, 220);
        animation.Commit(this, "ScannerAnimation", 16, 2000, Easing.Linear, (v, c) => ScannerLine.TranslationY = 0, () => _isScanning);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        _isScanning = false;
        await Navigation.PopAsync();
    }

    private async void OnSimulateScanResult(object sender, EventArgs e)
    {
        // Giả lập quét thành công một POI
        ScannedPoi = new POI
        {
            Id = 1,
            Name = "Chùa Một Cột",
            Description = "Chùa Một Cột có tên hán việt là Diên Hựu tự, là một ngôi chùa nằm giữa lòng thủ đô Hà Nội.",
            Images = new List<string> { "place_placeholder.png" }
        };

        // Hiển thị thẻ kết quả với hiệu ứng mượt mà
        await ResultCard.TranslateTo(0, 0, 400, Easing.SinOut);
    }

    private async void OnCloseResultClicked(object sender, EventArgs e)
    {
        await ResultCard.TranslateTo(0, 400, 300, Easing.SinIn);
        ScannedPoi = null;
    }

    private void OnPlayPauseAudioClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null) return;

        ScannedPoi.IsPlaying = !ScannedPoi.IsPlaying;
        
        // Cập nhật icon minh họa
        PlayPauseIcon.Source = ScannedPoi.IsPlaying ? "ic_white_pause.svg" : "ic_white_play.svg";
        
        if (ScannedPoi.IsPlaying)
        {
            // Giả lập tiến trình âm thanh
            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                if (ScannedPoi == null || !ScannedPoi.IsPlaying) return false;
                
                if (ScannedPoi.AudioProgress < 100)
                    ScannedPoi.AudioProgress += 1;
                else
                    ScannedPoi.IsPlaying = false;
                
                return ScannedPoi.IsPlaying;
            });
        }
    }

    private void OnStopAudioClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null) return;
        ScannedPoi.IsPlaying = false;
        ScannedPoi.AudioProgress = 0;
        PlayPauseIcon.Source = "ic_white_play.svg";
    }

    private async void OnDetailClicked(object sender, EventArgs e)
    {
        if (ScannedPoi == null) return;
        
        // Chuyển hướng đến trang chi tiết
        await Shell.Current.GoToAsync($"///detail?poi={ScannedPoi.Id}");
    }
}
