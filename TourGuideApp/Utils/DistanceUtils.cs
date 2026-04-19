using TourGuideApp.Models;

namespace TourGuideApp.Utils;

public static class DistanceUtils
{
    /// <summary>
    /// Công thức Haversine – trả về khoảng cách tính bằng mét giữa hai tọa độ.
    /// </summary>
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

    /// <summary>
    /// Định dạng số mét sang chuỗi hiển thị (m hoặc km).
    /// </summary>
    public static string FormatDistance(double meters)
    {
        if (meters < 1000) return $"{(int)meters} m";
        return $"{(meters / 1000.0):F1} km";
    }

    /// <summary>
    /// Cập nhật DistanceText và WalkingTimeText cho POI dựa trên vị trí hiện tại.
    /// </summary>
    public static double UpdatePoiDistance(POI poi, double userLat, double userLon)
    {
        double d = GetDistance(userLat, userLon, poi.Latitude, poi.Longitude);
        poi.DistanceText = FormatDistance(d);

        // Tính thời gian đi bộ: 1.4 m/s (khoảng 5km/h)
        int minutes = (int)Math.Max(1, Math.Round(d / 1.33 / 60.0));
        poi.WalkingTimeText = $"{minutes} phút";

        return d;
    }

    /// <summary>
    /// Tính phương vị (Bearing) từ điểm 1 đến điểm 2 (độ, 0-360).
    /// </summary>
    public static double GetBearing(double lat1, double lon1, double lat2, double lon2)
    {
        double φ1 = lat1 * Math.PI / 180;
        double φ2 = lat2 * Math.PI / 180;
        double Δλ = (lon2 - lon1) * Math.PI / 180;

        double y = Math.Sin(Δλ) * Math.Cos(φ2);
        double x = Math.Cos(φ1) * Math.Sin(φ2) -
                   Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);

        double θ = Math.Atan2(y, x);
        return (θ * 180 / Math.PI + 360) % 360;
    }

    /// <summary>
    /// Tính độ lệch góc nhỏ nhất giữa hai góc (độ). 
    /// Trả về giá trị dương (0-180).
    /// </summary>
    public static double GetAngleDifference(double angle1, double angle2)
    {
        double diff = Math.Abs(angle1 - angle2) % 360;
        return diff > 180 ? 360 - diff : diff;
    }
}
