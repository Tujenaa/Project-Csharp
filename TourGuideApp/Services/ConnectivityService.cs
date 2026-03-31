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
            var response = await client.GetAsync(
                ApiService.ApiConfig.BaseUrl.TrimEnd('/').Replace("/api", "/health"));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            // Nếu không có /health endpoint thì chỉ cần có mạng là được
            return IsConnected;
        }
    }
}
