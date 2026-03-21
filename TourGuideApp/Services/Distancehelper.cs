namespace TourGuideApp.Utils;

public static class DistanceHelper
{
    // Haversine formula – trả về khoảng cách tính bằng mét.
    public static double GetDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000; // bán kính Trái Đất (m)

        double φ1 = lat1 * Math.PI / 180;
        double φ2 = lat2 * Math.PI / 180;
        double Δφ = (lat2 - lat1) * Math.PI / 180;
        double Δλ = (lon2 - lon1) * Math.PI / 180;

        double a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2)
                 + Math.Cos(φ1) * Math.Cos(φ2)
                 * Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);

        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}