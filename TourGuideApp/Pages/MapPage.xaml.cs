using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class MapPage : ContentPage
{
    readonly MapViewModel viewModel;
    bool mapReady = false;
    bool poiListVisible = false;
    bool searchPanelVisible = false;
    POI? currentDetailPoi = null;
    POI? currentRoutePoi = null;  // POI đang có đường chỉ đường trên bản đồ

    public MapPage()
    {
        InitializeComponent();

        viewModel = IPlatformApplication.Current!.Services.GetRequiredService<MapViewModel>();
        BindingContext = viewModel;

        viewModel.EvalJs = js => mapWebView.EvaluateJavaScriptAsync(js);
        mapWebView.Source = new HtmlWebViewSource { Html = viewModel.MapHtml };

        viewModel.POIUpdated += OnPOIUpdated;
        viewModel.ActiveTourChanged += OnActiveTourChanged;
        viewModel.TourSelected += OnTourSelected;

        viewModel.SelectRouteDestCommand = new Command<POI>(poi => _ = OnRouteDestSelectedWithTourCheckAsync(poi));
        viewModel.ShowPOIOnMapCommand = new Command<POI>(poi => ShowDetailCard(poi));

        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    // ── Playback state ────────────────────────────────────────────────────────

    void OnPlaybackStateChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Đồng bộ lại reference currentDetailPoi phòng khi NearbyPOI đã rebuild
            if (currentDetailPoi != null)
            {
                var fresh = viewModel.NearbyPOI.FirstOrDefault(p => p.Id == currentDetailPoi.Id);
                if (fresh != null) currentDetailPoi = fresh;
            }

            if (currentDetailPoi == null || !poiDetailCard.IsVisible) return;

            var svc = AudioPlaybackService.Instance;
            bool isCurrentPoi = svc.CurrentPlayingPoi?.Id == currentDetailPoi.Id;
            bool isPlaying = isCurrentPoi && svc.IsPlaying;
            UpdateCardPlayButton(isPlaying);
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AudioPlaybackService.Instance.PlaybackStateChanged -= OnPlaybackStateChanged;
    }

    // ── WebView ───────────────────────────────────────────────────────────────

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
                    var r = await mapWebView.EvaluateJavaScriptAsync("typeof map !== 'undefined' ? '1' : '0'");
                    if (r == "1") { mapReady = true; break; }
                }
                catch { }
            }
        }

        // ── Đợi ViewModel khởi tạo xong dữ liệu POI ──────────────────────────────
        if (!viewModel.IsInitialized)
        {
            for (int i = 0; i < 50; i++) // Đọi tối đa 10 giây
            {
                if (viewModel.IsInitialized) break;
                await Task.Delay(200);
            }
        }

        // Đảm bảo dữ liệu mới nhất được đẩy lên bản đồ ngay khi mở
        await viewModel.PushMapDataAsync();
        UpdatePOICountLabel();

        // Nếu có tour được chọn từ HomePage
        if (MapTourState.SelectedTour != null)
        {
            var tour = MapTourState.SelectedTour;
            MapTourState.SelectedTour = null;
            await Task.Delay(300);
            await viewModel.ApplyTourAsync(tour);
            poiListVisible = true;
            poiBottomSheet.IsVisible = true;
            UpdatePOICountLabel();
            UpdateTourChipHighlight(tour);
        }

        // Nếu có POI được yêu cầu focus từ HomePage
        if (MapTourState.FocusPoiId != null)
        {
            int poiId = MapTourState.FocusPoiId.Value;
            // Gọi Refresh để đưa POI này vào danh sách hiển thị nếu nó chưa có (vd: chưa có audio)
            viewModel.RefreshNearbyOrder(); 
            
            MapTourState.FocusPoiId = null;
            var poi = viewModel.NearbyPOI.FirstOrDefault(p => p.Id == poiId);
            if (poi != null)
            {
                await Task.Delay(300);
                ShowDetailCard(poi);
                var lat = poi.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var lon = poi.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (viewModel.EvalJs != null)
                {
                    await viewModel.EvalJs($"highlightPOI({poiId})");
                    await viewModel.EvalJs($"flyTo({lat},{lon},16)");
                }
            }
        }

        // Nếu có POI được yêu cầu CHỈ ĐƯỜNG từ QrScannerPage
        if (MapTourState.DirectionPoiId != null)
        {
            int poiId = MapTourState.DirectionPoiId.Value;
            // Gọi Refresh để đưa POI này vào danh sách hiển thị nếu nó chưa có (vd: chưa có audio)
            viewModel.RefreshNearbyOrder();

            MapTourState.DirectionPoiId = null;
            var poi = viewModel.NearbyPOI.FirstOrDefault(p => p.Id == poiId);

            if (poi == null)
            {
                // Nếu vẫn chưa có trong Nearby sau khi refresh, thử lấy trực tiếp từ AllPOIs
                poi = viewModel.AllPOIs.FirstOrDefault(p => p.Id == poiId);
            }

            if (poi != null)
            {
                await OnRouteDestSelectedWithTourCheckAsync(poi);
            }
        }
    }

    // ── Tour chip ─────────────────────────────────────────────────────────────

    private void OnTourChipAllTapped(object sender, EventArgs e)
    {
        // Sử dụng command chính để reset về trang thái "Tất cả"
        viewModel.SelectTourCommand.Execute(null);

        UpdateTourChipHighlight(null);
        UpdatePOICountLabel();
    }

    string GetAllPOIsJson()
    {
        var pois = viewModel.NearbyPOI;
        var json = System.Text.Json.JsonSerializer.Serialize(pois.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            latitude = p.Latitude,
            longitude = p.Longitude
        }));
        return json;
    }

    void OnActiveTourChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var tour = viewModel.ActiveTour;
            lblBottomSheetTitle.Text = tour != null ? tour.Name ?? LocalizationService.Get("tour_label") : LocalizationService.Get("attractions_label");
            UpdatePOICountLabel();
        });
    }

    void OnTourSelected(Tour? tour)
    {
        MainThread.BeginInvokeOnMainThread(() => UpdateTourChipHighlight(tour));
    }

    private void UpdateTourChipHighlight(Tour? selectedTour)
    {
        // Highlight nút "Tất cả" nếu tour == null
        UpdateChipHighlight(chipAll, selectedTour == null);
        
        // Highlight tour chip con tương ứng trong BindableLayout
        foreach (var child in tourChipsContainer.Children)
        {
            if (child is Border b)
            {
                bool isSelected = (b.BindingContext is Tour t && selectedTour != null && t.Id == selectedTour.Id);
                UpdateChipHighlight(b, isSelected);
            }
        }
    }

    private void UpdateChipHighlight(Border border, bool isSelected)
    {
        border.BackgroundColor = isSelected ? Color.FromArgb("#512BD4") : Color.FromArgb("#F4F2FB");
        border.Stroke = isSelected ? Colors.Transparent : Color.FromArgb("#DDD6F3");
        
        if (border.Content is Label lbl)
        {
            lbl.TextColor = isSelected ? Colors.White : Color.FromArgb("#512BD4");
            lbl.FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    // ── Route bar ─────────────────────────────────────────────────────────────

    private void OnRouteBarTapped(object sender, EventArgs e)
    {
        if (poiDetailCard.IsVisible)
        {
            poiDetailCard.IsVisible = false;
            currentDetailPoi = null;
            HighlightNearestIfNoSelection();
        }
        if (poiListVisible)
        {
            poiListVisible = false;
            poiBottomSheet.IsVisible = false;
        }
        searchPanelVisible = !searchPanelVisible;
        searchPanel.IsVisible = searchPanelVisible;
        if (searchPanelVisible)
        {
            searchEntry.Focus();
            searchResultList.IsVisible = false;
        }
        else
        {
            CloseSearchPanel();
        }
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim() ?? "";
        btnClearSearch.IsVisible = query.Length > 0;
        if (query.Length == 0)
        {
            searchResultList.IsVisible = false;
            return;
        }
        var results = viewModel.SearchPOI(query);
        searchResultList.ItemsSource = results;
        searchResultList.IsVisible = results.Count > 0;
    }

    private void OnClearSearchTapped(object sender, EventArgs e)
    {
        searchEntry.Text = "";
        searchResultList.IsVisible = false;
        btnClearSearch.IsVisible = false;
    }

    // ── Route dest ────────────────────────────────────────────────────────────

    /// <summary>
    /// Wrapper kiểm tra tour trước khi chỉ đường.
    /// Nếu đang trong tour mà POI không thuộc tour → hỏi người dùng có muốn hủy tour không.
    /// </summary>
    async Task OnRouteDestSelectedWithTourCheckAsync(POI poi)
    {
        if (poi == null) return;

        if (viewModel.IsTourActive && viewModel.CurrentTour != null)
        {
            // Kiểm tra POI có trong danh sách tour ĐANG THỰC HIỆN hay không
            bool poiInTour = viewModel.CurrentTour.POIs?.Any(p => p.Id == poi.Id) ?? false;
            if (poiInTour)
            {
                // Reroute thông minh: Ưu tiên điểm vừa chọn nhưng vẫn giữ tour
                await viewModel.RerouteTourToPOI(poi);
                viewModel.CurrentDestinationName = poi.Name;
                btnClearRoute.IsVisible = true;
                CloseSearchPanel();
                ShowDetailCard(poi);
                return;
            }
            else
            {
                // Điểm nằm ngoài tour -> Hỏi ý kiến người dùng
                bool confirm = await DisplayAlert(
                    LocalizationService.Get("out_of_route_title"),
                    LocalizationService.Get("out_of_route_msg"),
                    LocalizationService.Get("cancel_tour_and_route"),
                    LocalizationService.Get("keep_tour"));

                if (!confirm) return;

                // Reset về trạng thái "Tất cả" để hiện đầy đủ Marker trước khi dẫn đường
                viewModel.SelectTourCommand.Execute(null);
                viewModel.CancelTourCommand.Execute(null);
            }
        }

        await OnRouteDestSelectedAsync(poi);
    }

    async Task OnRouteDestSelectedAsync(POI poi)
    {
        if (poi == null) return;
        viewModel.CurrentDestinationName = poi.Name;
        btnClearRoute.IsVisible = true;
        CloseSearchPanel();

        // Ghi nhớ POI đang được chỉ đường
        currentRoutePoi = poi;

        if (viewModel.EvalJs != null)
        {
            // Highlight marker điểm đến khi chỉ đường
            await viewModel.EvalJs($"highlightPOI({poi.Id})");
        }
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

    // ── Clear route ───────────────────────────────────────────────────────────

    private async void OnClearRouteTapped(object sender, EventArgs e)
    {
        viewModel.CurrentDestinationName = string.Empty;
        btnClearRoute.IsVisible = false;
        currentRoutePoi = null;  // Xóa route → reset về nearest
        if (viewModel.EvalJs != null)
            await viewModel.EvalJs("if(typeof routeLine !== 'undefined' && routeLine) { map.removeLayer(routeLine); routeLine = null; }");
        poiDetailCard.IsVisible = false;
        currentDetailPoi = null;
        HighlightNearestIfNoSelection();
    }

    // ── Toggle POI list ───────────────────────────────────────────────────────

    private void OnTogglePOIListTapped(object sender, EventArgs e)
    {
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

    // ── Current location ──────────────────────────────────────────────────────

    private async void OnCurrentLocationTapped(object sender, EventArgs e)
    {
        try
        {
            var pos = await viewModel.GetCurrentLocationFastAsync();
            if (pos == null) return;
            var (lat, lon) = pos.Value;
            var latStr = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lonStr = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await mapWebView.EvaluateJavaScriptAsync($"setUserLocation({latStr},{lonStr}); flyTo({latStr},{lonStr},15);");
            viewModel.RefreshNearbyOrder();
        }
        catch (Exception ex)
        {
            await DisplayAlert(LocalizationService.Get("error"), ex.Message, LocalizationService.Get("ok"));
        }
    }

    // ── Detail Card ───────────────────────────────────────────────────────────

    void ShowDetailCard(POI poi)
    {
        currentDetailPoi = poi;
        lblCardName.Text = poi.Name;
        lblCardDistance.Text = poi.DistanceText;
        lblCardDesc.Text = string.IsNullOrWhiteSpace(poi.Description)
            ? LocalizationService.Get("click_detail_to_learn_more")
            : poi.Description;

        var svc = AudioPlaybackService.Instance;
        bool isPlaying = svc.CurrentPlayingPoi?.Id == poi.Id && svc.IsPlaying;
        UpdateCardPlayButton(isPlaying);

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

        // Nếu vẫn đang có đường chỉ đường → giữ highlight cam cho POI đó
        if (currentRoutePoi != null && viewModel.EvalJs != null)
            _ = viewModel.EvalJs($"highlightPOI({currentRoutePoi.Id})");
        else
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
        await OnRouteDestSelectedWithTourCheckAsync(currentDetailPoi);
    }

    private async void OnCardDetailTapped(object sender, EventArgs e)
    {
        if (currentDetailPoi == null) return;
        poiDetailCard.IsVisible = false;
        viewModel.GoToDetailCommand.Execute(currentDetailPoi);
    }

    void UpdateCardPlayButton(bool isPlaying)
    {
        if (currentDetailPoi == null || !currentDetailPoi.HasAudio)
        {
            btnCardPlayAudio.IsVisible = false;
            btnCardPauseAudio.IsVisible = false;
            return;
        }

        btnCardPlayAudio.IsVisible = !isPlaying;
        btnCardPauseAudio.IsVisible = isPlaying;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void UpdatePOICountLabel()
    {
        int count = viewModel.NearbyPOI.Count;
        var tour = viewModel.ActiveTour;
        if (tour != null)
            lblPoiCount.Text = count > 0 ? LocalizationService.Get("locations_in_tour_format", count) : LocalizationService.Get("no_locations");
        else
            lblPoiCount.Text = count > 0
                ? LocalizationService.Get("locations_nearby_format", count)
                : LocalizationService.Get("no_locations_found");
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
                    // Cập nhật reference để tránh mất sync sau khi collection rebuild
                    currentDetailPoi = updated;
                    lblCardDistance.Text = updated.DistanceText;

                    // Dùng AudioPlaybackService làm nguồn sự thật thay vì poi.IsPlaying
                    var svc = AudioPlaybackService.Instance;
                    bool isPlaying = svc.CurrentPlayingPoi?.Id == updated.Id && svc.IsPlaying;
                    UpdateCardPlayButton(isPlaying);
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
        // Nếu đang có route, ưu tiên highlight điểm đến thay vì nearest
        if (currentRoutePoi != null && viewModel.EvalJs != null)
        {
            _ = viewModel.EvalJs($"highlightPOI({currentRoutePoi.Id})");
            return;
        }
        if (currentDetailPoi == null && viewModel.NearbyPOI.Count > 0 && viewModel.EvalJs != null)
        {
            var closest = viewModel.NearbyPOI.First();
            _ = viewModel.EvalJs($"highlightNearest({closest.Id})");
        }
    }
}