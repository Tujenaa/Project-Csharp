using Microsoft.Maui.Devices.Sensors;

namespace TourGuideApp.Services;

public class LocationService
{
     public async Task<Location?> GetCurrentLocationAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            return null;

        try
        {
            // 1 Dùng vị trí cached nếu còn mới (≤ 30 s) – trả về gần như ngay lập tức
            var cached = await Geolocation.Default.GetLastKnownLocationAsync();
            if (cached != null && (DateTimeOffset.UtcNow - cached.Timestamp).TotalSeconds <= 30)
                return cached;

            //Lấy fresh – timeout ngắn 5 s, độ chính xác Medium vẫn đủ cho bản đồ
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
            return await Geolocation.Default.GetLocationAsync(request);
        }
        catch
        {
            //Fallback: trả cached dù cũ còn hơn null
            return await Geolocation.Default.GetLastKnownLocationAsync();
        }
    }
}