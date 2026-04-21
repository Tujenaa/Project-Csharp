using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Linq;
using System.Collections.Generic;
using System;

namespace GPSGuide.Web.Pages;

public class UserActivityModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public UserActivityModel(IHttpClientFactory http) => _http = http;

    public List<ActivityItem> Activities { get; set; } = [];
    public List<ActivityItem> OnlineUsers { get; set; } = [];
    public string? Msg { get; set; }

    // Stat counters
    public int TotalDevices { get; set; }
    public int ActiveUsers { get; set; }
    public int ActiveGuests { get; set; }
    public int ActiveRegistered { get; set; }
    public int TodayListens { get; set; }

    private string Role => HttpContext.Session.GetString("Role") ?? "";
    private bool IsAdmin => Role == "ADMIN";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");

        var client = _http.CreateClient("API");

        try
        {
            // Tải nhật ký hoạt động
            Activities = await client.GetFromJsonAsync<List<ActivityItem>>("user-activity") ?? [];

            // Đếm thiết bị đang online (Heartbeat thực tế)
            var activeDevices = await client.GetFromJsonAsync<List<HeartbeatInfo>>("device/active") ?? [];
            TotalDevices = activeDevices.Count;

            // Tính toán danh sách người dùng đang trực tuyến dựa trên kết hợp Heartbeat và Identity
            OnlineUsers = new List<ActivityItem>();
            foreach (var dev in activeDevices)
            {
                // Tìm hoạt động gần nhất của thiết bị này trong lịch sử để lấy danh tính
                var lastAct = Activities
                    .Where(a => a.DeviceId == dev.deviceId)
                    .OrderByDescending(a => a.Timestamp)
                    .FirstOrDefault();

                if (lastAct != null && lastAct.ActivityType != "LOGOUT" && lastAct.ActivityType != "OFFLINE")
                {
                    // Họ đang đăng nhập
                    OnlineUsers.Add(lastAct);
                }
                else
                {
                    // Họ đã đăng xuất hoặc là khách chưa đăng nhập, nhưng Device vẫn đang gửi heartbeat
                    OnlineUsers.Add(new ActivityItem(
                        0, null, "Khách (Ẩn danh)", "GUEST", "ONLINE", "Thiết bị đang hoạt động", dev.deviceId, dev.lastSeen
                    ));
                }
            }

            ActiveUsers = OnlineUsers.Count;
            ActiveGuests = OnlineUsers.Count(u => u.Role == "GUEST");
            ActiveRegistered = ActiveUsers - ActiveGuests;

            // Lượt nghe hôm nay
            var today = DateTime.Today;
            TodayListens = Activities.Count(a => a.ActivityType == "LISTEN" && a.Timestamp.Date == today);
        }
        catch
        {
            Activities = [];
            OnlineUsers = [];
        }

        return Page();
    }


    public async Task<IActionResult> OnPostDeleteAllAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");

        var client = _http.CreateClient("API");
        try
        {
            await client.DeleteAsync("user-activity");
            Msg = "Đã xóa toàn bộ nhật ký hoạt động.";
        }
        catch
        {
            Msg = "Lỗi khi xóa dữ liệu.";
        }

        return await OnGetAsync();
    }

    // Local DTO — khớp với JSON trả về từ API, không cần tham chiếu TourGuideAPI
    public record ActivityItem(
        int Id,
        int? UserId,
        string Username,
        string Role,
        string ActivityType,
        string Details,
        string? DeviceId,
        DateTime Timestamp
    );
    public record HeartbeatInfo(string deviceId, double lat, double lng, DateTime lastSeen);
}
