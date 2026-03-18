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
            var request = new GeolocationRequest(
                GeolocationAccuracy.Best,
                TimeSpan.FromSeconds(10));

            var location = await Geolocation.Default.GetLocationAsync(request);

            return location;
        }
        catch
        {
            return null;
        }

    }
}