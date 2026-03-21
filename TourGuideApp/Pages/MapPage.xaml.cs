using TourGuideApp.Models;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class MapPage : ContentPage
{
    readonly MapViewModel viewModel;
    bool mapReady = false;

    public MapPage()
    {
        InitializeComponent();

        viewModel = IPlatformApplication.Current!.Services.GetRequiredService<MapViewModel>();
        BindingContext = viewModel;

        viewModel.EvalJs = js => mapWebView.EvaluateJavaScriptAsync(js);
        mapWebView.Source = new HtmlWebViewSource { Html = viewModel.MapHtml };
    }

    // ── WebView: bắt URL scheme tourguide://poi/{id} ──────────────────────────

    private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith("tourguide://poi/")) return;
        e.Cancel = true;

        if (int.TryParse(e.Url.Replace("tourguide://poi/", ""), out int poiId))
        {
            var poi = viewModel.NearbyPOI.FirstOrDefault(p => p.Id == poiId);
            if (poi != null) viewModel.PlayPOIManually(poi);
        }
    }

    // ── Page lifecycle ────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!mapReady)
        {
            // Đợi Leaflet init (tối đa 3 s)
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

            // Cập nhật label điểm đến mặc định (POI đầu tiên sau khi sort)
            UpdateDestinationLabel();
        }
    }

    // ── FAB – về vị trí hiện tại ──────────────────────────────────────────────

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
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }

    // ── Thanh lộ trình – chọn điểm xuất phát mới ─────────────────────────────

    private async void OnChangeDestinationTapped(object sender, EventArgs e)
    {
        if (viewModel.NearbyPOI.Count == 0) return;

        // Hiện danh sách POI để chọn điểm bắt đầu
        var names = viewModel.NearbyPOI.Select(p => p.Name).ToArray();
        var chosen = await DisplayActionSheet(
            "Chọn điểm bắt đầu lộ trình", "Huỷ", null, names);

        if (chosen == null || chosen == "Huỷ") return;

        var poi = viewModel.NearbyPOI.FirstOrDefault(p => p.Name == chosen);
        if (poi == null) return;

        // xóa đường cũ + vẽ đường mới
        viewModel.ChangeDestinationCommand.Execute(poi);
        await mapWebView.EvaluateJavaScriptAsync("clearRoutes()");
        await Task.Delay(100); 
        await viewModel.PushMapDataAsync();
        // Cập nhật 
        lblDestination.Text = poi.Name;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void UpdateDestinationLabel()
    {
        var first = viewModel.NearbyPOI.FirstOrDefault();
        lblDestination.Text = first?.Name ?? "Chưa có điểm đến";
    }
}