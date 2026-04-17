using System.Net.Http.Json;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;


namespace TourGuideApp.Services;

public class DeviceHeartbeatSender
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private CancellationTokenSource? _cts;

    public DeviceHeartbeatSender() { }

    public void Start()
    {
        // Tránh start 2 lần (ví dụ OnResume gọi khi đã đang chạy)
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        _ = RunLoop(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await SendHeartbeatAsync();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
            }
            catch (TaskCanceledException)
            {
                // Stop() được gọi trong lúc đang chờ — thoát vòng lặp
                break;
            }
        }
    }

    private async Task SendHeartbeatAsync()
    {
        try
        {
            // 1. Thử lấy vị trí nhanh từ cache (Last Known)
            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            
            // 2. Nếu không có (máy vừa khởi động hoặc GPS tắt lâu), thử lấy vị trí mới với timeout 5s
            if (location == null)
            {
                System.Diagnostics.Debug.WriteLine("[Heartbeat] LastKnown is null, requesting fresh location...");
                location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
            }

            if (location == null)
            {
                System.Diagnostics.Debug.WriteLine("[Heartbeat] GPS unavailable. Skipping heartbeat.");
                return;
            }

            var deviceId = $"{DeviceInfo.Current.Name}_{DeviceInfo.Current.Model}";

            var payload = new
            {
                deviceId,
                latitude = location.Latitude,
                longitude = location.Longitude
            };

            var url = $"{ApiService.ApiConfig.BaseUrl}device/heartbeat";
            var response = await _http.PostAsJsonAsync(url, payload);

            if (response.IsSuccessStatusCode)
                System.Diagnostics.Debug.WriteLine($"[Heartbeat] Sent successfully: {deviceId} at {location.Latitude},{location.Longitude}");
            else
                System.Diagnostics.Debug.WriteLine($"[Heartbeat] Server error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Heartbeat] Critical Error: {ex.Message}");
        }
    }

    public async Task NotifyOfflineAsync()
    {
        try
        {
            var deviceId = $"{DeviceInfo.Current.Name}_{DeviceInfo.Current.Model}";
            var payload = new { deviceId };
            
            // Timeout ngắn vì đây là thao tác cleanup nhanh
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _http.PostAsJsonAsync($"{ApiService.ApiConfig.BaseUrl}device/offline", payload, cts.Token);
        }
        catch
        {
            // Fail silently as this is called during app sleep/exit
        }
    }
}