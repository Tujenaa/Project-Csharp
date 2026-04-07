using TourGuideApp.Models;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class MapPage : ContentPage
{
    readonly MapViewModel viewModel;
    bool mapReady = false;
    bool poiListVisible = false;
    bool searchPanelVisible = false;
    POI? currentDetailPoi = null;

    public MapPage()
    {
        InitializeComponent();

        viewModel = IPlatformApplication.Current!.Services.GetRequiredService<MapViewModel>();
        BindingContext = viewModel;

        viewModel.EvalJs = js => mapWebView.EvaluateJavaScriptAsync(js);
        mapWebView.Source = new HtmlWebViewSource { Html = viewModel.MapHtml };

        viewModel.POIUpdated += OnPOIUpdated;

        // Khi SelectRouteDestCommand được execute (từ search hoặc list) → vẽ đường
        viewModel.SelectRouteDestCommand = new Command<POI>(poi => _ = OnRouteDestSelectedAsync(poi));
    }

    // ── WebView: bắt URL scheme ───────────────────────────────────────────────

    private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith("tourguide://poi/")) return;
        e.Cancel = true;

        if (int.TryParse(e.Url.Replace("tourguide://poi/", ""), out int poiId))
        {
            var poi = viewModel.NearbyPOI.FirstOrDefault(p => p.Id == poiId);
            if (poi != null) ShowDetailCard(poi);
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
                catch { }
            }

            await viewModel.PushMapDataAsync();
            UpdatePOICountLabel();
        }
    }

    // ── Thanh lộ trình: bấm vào → mở/đóng panel tìm kiếm ────────────────────

    private void OnRouteBarTapped(object sender, EventArgs e)
    {
        // Nếu đang hiện detail card → đóng trước
        if (poiDetailCard.IsVisible)
        {
            poiDetailCard.IsVisible = false;
            currentDetailPoi = null;
            HighlightNearestIfNoSelection();
        }
        // Đóng bottom sheet nếu mở
        if (poiListVisible)
        {
            poiListVisible = false;
            poiBottomSheet.IsVisible = false;
        }

        searchPanelVisible = !searchPanelVisible;
        searchPanel.IsVisible = searchPanelVisible;

        if (searchPanelVisible)
        {
            // Focus ô tìm kiếm ngay
            searchEntry.Focus();
            // Hiện toàn bộ danh sách khi chưa gõ gì
            searchResultList.IsVisible = false;
        }
        else
        {
            CloseSearchPanel();
        }
    }

    // ── Tìm kiếm ─────────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim() ?? "";
        btnClearSearch.IsVisible = query.Length > 0;

        if (query.Length == 0)
        {
            searchResultList.IsVisible = false;
            lblOrPickList.IsVisible = true;
            fullPoiList.IsVisible = true;
            return;
        }

        var results = viewModel.SearchPOI(query);
        searchResultList.ItemsSource = results;
        searchResultList.IsVisible = results.Count > 0;
        lblOrPickList.IsVisible = false;
        fullPoiList.IsVisible = false;
    }

    private void OnClearSearchTapped(object sender, EventArgs e)
    {
        searchEntry.Text = "";
        searchResultList.IsVisible = false;
        btnClearSearch.IsVisible = false;
        lblOrPickList.IsVisible = true;
        fullPoiList.IsVisible = true;
    }

    // ── Chọn điểm đến lộ trình (từ search dropdown hoặc danh sách) ───────────

    async Task OnRouteDestSelectedAsync(POI poi)
    {
        if (poi == null) return;

        // 1. Cập nhật thanh lộ trình
        lblRouteDestination.Text = poi.Name;
        lblRouteDestination.TextColor = Color.FromArgb("#1A1035");
        btnClearRoute.IsVisible = true;

        // 2. Đóng panel search
        CloseSearchPanel();

        // 3. Highlight marker POI (đã được gọi trong ShowDetailCard, giữ nguyên cũng được)
        if (viewModel.EvalJs != null)
            await viewModel.EvalJs($"highlightPOI({poi.Id})");

        // 4. Lấy vị trí user và vẽ đường
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

        // 5. Hiện detail card
        ShowDetailCard(poi);
    }

    void CloseSearchPanel()
    {
        searchPanelVisible = false;
        searchPanel.IsVisible = false;
        searchEntry.Text = "";
        searchResultList.IsVisible = false;
        btnClearSearch.IsVisible = false;
        searchEntry.Unfocus();
    }

    // ── Xóa lộ trình ─────────────────────────────────────────────────────────

    private async void OnClearRouteTapped(object sender, EventArgs e)
    {
        lblRouteDestination.Text = "Chọn điểm đến...";
        lblRouteDestination.TextColor = Color.FromArgb("#94A3B8");
        btnClearRoute.IsVisible = false;

        if (viewModel.EvalJs != null)
        {
            await viewModel.EvalJs("if(typeof routeLine !== 'undefined' && routeLine) { map.removeLayer(routeLine); routeLine = null; }");
        }

        poiDetailCard.IsVisible = false;
        currentDetailPoi = null;
        HighlightNearestIfNoSelection();
    }

    // ── Toggle danh sách POI ──────────────────────────────────────────────────

    private void OnTogglePOIListTapped(object sender, EventArgs e)
    {
        // Đóng search panel nếu đang mở
        if (searchPanelVisible) CloseSearchPanel();

        poiListVisible = !poiListVisible;
        poiBottomSheet.IsVisible = poiListVisible;

        if (poiListVisible)
        {
            poiDetailCard.IsVisible = false;
            currentDetailPoi = null;
            HighlightNearestIfNoSelection();
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

            viewModel.RefreshDistances();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }

    // ── Detail Card ───────────────────────────────────────────────────────────

    void ShowDetailCard(POI poi)
    {
        currentDetailPoi = poi;

        lblCardName.Text = poi.Name;
        lblCardDistance.Text = poi.DistanceText;
        lblCardDesc.Text = string.IsNullOrWhiteSpace(poi.Description)
            ? "Nhấn \"Xem chi tiết\" để biết thêm về địa điểm này."
            : poi.Description;

        UpdateCardPlayButton(poi.IsPlaying);
        poiDetailCard.IsVisible = true;

        if (viewModel.EvalJs != null)
            _ = viewModel.EvalJs($"highlightPOI({poi.Id})");

        if (poiListVisible)
        {
            poiListVisible = false;
            poiBottomSheet.IsVisible = false;
        }
    }

    private void OnCloseDetailCardTapped(object sender, EventArgs e)
    {
        poiDetailCard.IsVisible = false;
        currentDetailPoi = null;
        HighlightNearestIfNoSelection();
    }

    private void OnCardPlayAudioTapped(object sender, EventArgs e)
    {
        if (currentDetailPoi == null) return;
        viewModel.PlayAudioCommand.Execute(currentDetailPoi);
    }

    private void OnCardPauseAudioTapped(object sender, EventArgs e)
    {
        if (currentDetailPoi == null) return;
        viewModel.PauseAudioCommand.Execute(currentDetailPoi);
    }

    private async void OnCardNavigateTapped(object sender, EventArgs e)
    {
        if (currentDetailPoi == null) return;
        await OnRouteDestSelectedAsync(currentDetailPoi);
    }

    private async void OnCardDetailTapped(object sender, EventArgs e)
    {
        if (currentDetailPoi == null) return;
        poiDetailCard.IsVisible = false;
        viewModel.GoToDetailCommand.Execute(currentDetailPoi);
    }

    void UpdateCardPlayButton(bool isPlaying)
    {
        btnCardPlayAudio.IsVisible = !isPlaying;
        btnCardPauseAudio.IsVisible = isPlaying;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
            if (currentDetailPoi != null && poiDetailCard.IsVisible)
            {
                var updated = viewModel.NearbyPOI.FirstOrDefault(p => p.Id == currentDetailPoi.Id);
                if (updated != null)
                {
                    lblCardDistance.Text = updated.DistanceText;
                    UpdateCardPlayButton(updated.IsPlaying);
                }
            }
            else
            {
                HighlightNearestIfNoSelection();
            }
        });
    }

    void HighlightNearestIfNoSelection()
    {
        if (currentDetailPoi == null && viewModel.NearbyPOI.Count > 0 && viewModel.EvalJs != null)
        {
            var closest = viewModel.NearbyPOI.First();
            _ = viewModel.EvalJs($"highlightNearest({closest.Id})");
        }
    }
}