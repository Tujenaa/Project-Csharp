using TourGuideApp.Models;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class MapPage : ContentPage
{
    readonly MapViewModel viewModel;
    bool mapReady = false;
    bool poiListVisible = false;
    POI? currentDetailPoi = null;

    public MapPage()
    {
        InitializeComponent();

        viewModel = IPlatformApplication.Current!.Services.GetRequiredService<MapViewModel>();
        BindingContext = viewModel;

        viewModel.EvalJs = js => mapWebView.EvaluateJavaScriptAsync(js);
        mapWebView.Source = new HtmlWebViewSource { Html = viewModel.MapHtml };

        // Lắng nghe sự kiện khi ViewModel cập nhật khoảng cách
        viewModel.POIUpdated += OnPOIUpdated;
    }

    // ── WebView: bắt URL scheme tourguide://poi/{id} ──────────────────────────

    private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith("tourguide://poi/")) return;
        e.Cancel = true;

        if (int.TryParse(e.Url.Replace("tourguide://poi/", ""), out int poiId))
        {
            var poi = viewModel.NearbyPOI.FirstOrDefault(p => p.Id == poiId);
            if (poi != null)
            {
                // Hiện detail card thay vì tự động phát audio
                ShowDetailCard(poi);
            }
        }
    }

    // ── Page lifecycle ────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!mapReady)
        {
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(100);
                try
                {
                    var r = await mapWebView.EvaluateJavaScriptAsync(
                        "typeof map !== 'undefined' ? '1' : '0'");
                    if (r == "1") { mapReady = true; break; }
                }
                catch { /* chưa sẵn sàng */ }
            }

            await viewModel.PushMapDataAsync();
            UpdatePOICountLabel();
        }
    }

    // ── Toggle danh sách POI ──────────────────────────────────────────────────

    private void OnTogglePOIListTapped(object sender, EventArgs e)
    {
        poiListVisible = !poiListVisible;
        poiBottomSheet.IsVisible = poiListVisible;

        // Ẩn detail card khi mở list
        if (poiListVisible)
        {
            poiDetailCard.IsVisible = false;
            currentDetailPoi = null;
        }
    }

    // ── Nút "vị trí của tôi" ─────────────────────────────────────────────────

    private async void OnCurrentLocationTapped(object sender, EventArgs e)
    {
        try
        {
            var pos = await viewModel.GetCurrentLocationFastAsync();
            if (pos == null) return;

            var (lat, lon) = pos.Value;
            var latStr = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lonStr = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);

            await mapWebView.EvaluateJavaScriptAsync(
                $"setUserLocation({latStr},{lonStr}); flyTo({latStr},{lonStr},15);");

            // Cập nhật khoảng cách sau khi có vị trí mới
            viewModel.RefreshDistances();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }

    // ── Detail Card: hiện khi bấm marker ─────────────────────────────────────

    async void ShowDetailCard(POI poi)
    {
        currentDetailPoi = poi;

        lblCardName.Text = poi.Name;
        lblCardDistance.Text = poi.DistanceText;
        lblCardDesc.Text = string.IsNullOrWhiteSpace(poi.Description)
            ? "Nhấn \"Xem chi tiết\" để biết thêm về địa điểm này."
            : poi.Description;

        UpdateCardPlayButton(poi.IsPlaying);

        poiDetailCard.IsVisible = true;

        if (poiListVisible)
        {
            poiListVisible = false;
            poiBottomSheet.IsVisible = false;
        }

        // Highlight
        if (viewModel.EvalJs != null)
            await viewModel.EvalJs($"highlightPOI({poi.Id})");

        // Lấy vị trí
        var loc = await viewModel.GetCurrentLocationFastAsync();

        if (loc != null && viewModel.EvalJs != null)
        {
            var (lat, lon) = loc.Value;

            var lat1 = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lon1 = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lat2 = poi.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lon2 = poi.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

            await viewModel.EvalJs($"drawRoute({lat1},{lon1},{lat2},{lon2})");
        }
    }

    private void OnCloseDetailCardTapped(object sender, EventArgs e)
    {
        poiDetailCard.IsVisible = false;
        currentDetailPoi = null;
    }

    private void OnCardPlayAudioTapped(object sender, EventArgs e)
    {
        if (currentDetailPoi == null) return;
        viewModel.PlayAudioCommand.Execute(currentDetailPoi);
        UpdateCardPlayButton(!currentDetailPoi.IsPlaying); // toggle trước khi ViewModel update
    }

    private async void OnCardDetailTapped(object sender, EventArgs e)
    {
        if (currentDetailPoi == null) return;
        poiDetailCard.IsVisible = false;
        viewModel.GoToDetailCommand.Execute(currentDetailPoi);
    }

    void UpdateCardPlayButton(bool isPlaying)
    {
        imgCardPlay.Source = isPlaying ? "ic_pause.svg" : "ic_play.svg";
        lblCardPlay.Text = isPlaying ? "Đang phát" : "Audio giới thiệu";
    }

    // ── Cập nhật label số lượng POI ──────────────────────────────────────────

    void UpdatePOICountLabel()
    {
        int count = viewModel.NearbyPOI.Count;
        lblPoiCount.Text = count > 0
            ? $"{count} địa điểm · sắp xếp từ gần nhất"
            : "Không có địa điểm nào";
    }

    void OnPOIUpdated()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdatePOICountLabel();
            // Nếu detail card đang hiện, cập nhật khoảng cách
            if (currentDetailPoi != null && poiDetailCard.IsVisible)
            {
                var updated = viewModel.NearbyPOI.FirstOrDefault(p => p.Id == currentDetailPoi.Id);
                if (updated != null)
                    lblCardDistance.Text = updated.DistanceText;
            }
        });
    }
}