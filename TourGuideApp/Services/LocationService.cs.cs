using Microsoft.Maui.Devices.Sensors;

namespace TourGuideApp.Services;

public class LocationService
{
    public async Task<Location?> GetLocationAsync()
    {
        return await Geolocation.Default.GetLocationAsync();
    }
}