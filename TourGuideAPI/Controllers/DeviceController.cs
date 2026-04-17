using Microsoft.AspNetCore.Mvc;
using TourGuideAPI.Services;

namespace TourGuideAPI.Controllers;

[ApiController]
[Route("api/device")]
public class DeviceController : ControllerBase
{
    private readonly DeviceHeartbeatService _heartbeat;

    public DeviceController(DeviceHeartbeatService heartbeat)
    {
        _heartbeat = heartbeat;
    }

    // Mobile app gọi mỗi 30 giây để cập nhật vị trí
    // POST /api/device/heartbeat
    // Body: { "deviceId": "abc123", "latitude": 10.77, "longitude": 106.70 }
    [HttpPost("heartbeat")]
    public IActionResult Heartbeat([FromBody] HeartbeatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceId))
            return BadRequest("DeviceId is required");

        _heartbeat.Upsert(req.DeviceId, req.Latitude, req.Longitude);
        return Ok(new { status = "ok" });
    }

    // Mobile app gọi khi tắt app hoặc vào background để xóa trạng thái ngay lập tức
    [HttpPost("offline")]
    public IActionResult SetOffline([FromBody] OfflineRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.DeviceId))
        {
            _heartbeat.Remove(req.DeviceId);
        }
        return Ok(new { status = "removed" });
    }

    // Dashboard web gọi để lấy danh sách thiết bị đang online
    // GET /api/device/active
    [HttpGet("active")]
    public IActionResult GetActive()
    {
        var devices = _heartbeat.GetActive()
            .Select(d => new
            {
                deviceId = d.DeviceId,
                lat = d.Latitude,
                lng = d.Longitude,
                lastSeen = d.LastSeen
            });

        return Ok(devices);
    }
}

public class HeartbeatRequest
{
    public string DeviceId { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class OfflineRequest
{
    public string DeviceId { get; set; } = "";
}