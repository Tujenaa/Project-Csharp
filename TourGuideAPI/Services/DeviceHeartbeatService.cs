using System.Collections.Concurrent;

namespace TourGuideAPI.Services;

public class DeviceLocation
{
    public string DeviceId { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime LastSeen { get; set; }
}

public class DeviceHeartbeatService
{
    // Lưu in-memory: deviceId -> location
    private readonly ConcurrentDictionary<string, DeviceLocation> _devices = new();

    // Timeout: thiết bị không gửi heartbeat trong 2 phút => coi là offline
    private static readonly TimeSpan OfflineThreshold = TimeSpan.FromMinutes(2);

    public void Upsert(string deviceId, double lat, double lng)
    {
        _devices[deviceId] = new DeviceLocation
        {
            DeviceId = deviceId,
            Latitude = lat,
            Longitude = lng,
            LastSeen = DateTime.UtcNow
        };
    }

    public void Remove(string deviceId)
    {
        _devices.TryRemove(deviceId, out _);
    }

    public IReadOnlyList<DeviceLocation> GetActive()
    {
        var cutoff = DateTime.UtcNow - OfflineThreshold;
        return _devices.Values
            .Where(d => d.LastSeen >= cutoff)
            .ToList();
    }
}