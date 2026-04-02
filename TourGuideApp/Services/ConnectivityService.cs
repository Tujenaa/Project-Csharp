namespace TourGuideApp.Services;

/// <summary>
/// Helper kiểm tra trạng thái kết nối mạng.
/// </summary>
public static class ConnectivityService
{
    /// <summary>Trả về true nếu thiết bị đang có kết nối mạng.</summary>
    public static bool IsConnected =>
        Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    /// <summary>Thử kết nối đến API server để xác nhận server có hoạt động không.</summary>
    public static async Task<bool> CanReachApiAsync()
    {
        if (!IsConnected) return false;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            // Thử gọi đến base URL (không có /health)
            var url = ApiService.ApiConfig.BaseUrl.TrimEnd('/').Replace("/api", "");
            var response = await client.GetAsync(url);
            
            // Nếu nhận được phản hồi (kể cả 404, 401...) từ server thì coi như là Reachable
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Connectivity] Server unreachable: {ex.Message}");
            // Trong trường hợp lỗi kết nối thực sự, ta vẫn trả về IsConnected 
            // để cho phép app tiếp tục thử gọi API (ApiService sẽ tự handle lỗi tiếp)
            return IsConnected;
        }
    }
}
