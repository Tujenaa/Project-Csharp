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
            // Lấy trực tiếp vị trí mới thay vì cache để đảm bảo tracking liên tục
            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5));
            return await Geolocation.Default.GetLocationAsync(request);
        }
        catch
        {
            //Fallback: trả cached dù cũ còn hơn null
            return await Geolocation.Default.GetLastKnownLocationAsync();
        }
    }
}