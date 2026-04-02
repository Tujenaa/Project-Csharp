using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TourGuideApp.Models;

namespace TourGuideApp.Services;

public class ApiService
{
    readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static class ApiConfig
    {
        public const string BaseUrl = "http://192.168.1.53:5266/api/";
        //public const string BaseUrl = "http://10.0.2.2:5266/api/";
    }

    // ── POI ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách POI.
    /// - Online: lấy từ API rồi cache xuống SQLite.
    /// - Offline: đọc từ SQLite cache.
    /// </summary>
    public async Task<List<POI>> GetPOI()
    {
        if (ConnectivityService.IsConnected)
        {
            try
            {
                var pois = await client.GetFromJsonAsync<List<POI>>(
                    $"{ApiConfig.BaseUrl}POI");

                if (pois != null && pois.Count > 0)
                {
                    _ = LocalDbService.Instance.SavePOIsAsync(pois);
                    return pois;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetPOI failed: {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine("[API] GetPOI → using offline cache");
        return await LocalDbService.Instance.GetCachedPOIsAsync();
    }

    /// <summary>Top POI — offline fallback lấy 5 POI đầu trong cache.</summary>
    public async Task<List<POI>> GetTopPOI()
    {
        if (ConnectivityService.IsConnected)
        {
            try
            {
                return await client.GetFromJsonAsync<List<POI>>(
                    $"{ApiConfig.BaseUrl}POI/top")
                    ?? new List<POI>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetTopPOI failed: {ex.Message}");
            }
        }

        var cached = await LocalDbService.Instance.GetCachedPOIsAsync();
        return cached.Take(5).ToList();
    }

    public async Task<int> GetPoiCount()
    {
        if (ConnectivityService.IsConnected)
        {
            try { return await client.GetFromJsonAsync<int>($"{ApiConfig.BaseUrl}POI/count"); }
            catch { }
        }
        var cached = await LocalDbService.Instance.GetCachedPOIsAsync();
        return cached.Count;
    }

    public async Task<int> GetAudioCount()
    {
        if (ConnectivityService.IsConnected)
        {
            try { return await client.GetFromJsonAsync<int>($"{ApiConfig.BaseUrl}POI/audio-count"); }
            catch { }
        }
        return 0;
    }

    // ── History ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Lưu lịch sử.
    /// - Online: gửi API ngay + sync queue offline.
    /// - Offline: lưu vào SQLite pending queue.
    /// </summary>
    public async Task SaveHistory(int poiId, int userId)
    {
        if (ConnectivityService.IsConnected)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { poiId, userId });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await client.PostAsync($"{ApiConfig.BaseUrl}history", content);
                _ = SyncPendingHistoriesAsync(userId);
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] SaveHistory failed: {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"[API] SaveHistory → queued offline (poi={poiId})");
        await LocalDbService.Instance.AddPendingHistoryAsync(poiId, userId);
    }

    public async Task<List<HistoryDto>> GetHistory(int userId)
    {
        if (ConnectivityService.IsConnected)
        {
            try
            {
                return await client.GetFromJsonAsync<List<HistoryDto>>(
                    $"{ApiConfig.BaseUrl}history/user/{userId}")
                    ?? new List<HistoryDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetHistory failed: {ex.Message}");
            }
        }
        return new List<HistoryDto>();
    }

    /// <summary>Sync tất cả history offline queue lên server khi có mạng lại.</summary>
    public async Task SyncPendingHistoriesAsync(int userId)
    {
        var pending = await LocalDbService.Instance.GetPendingHistoriesAsync();
        if (pending.Count == 0) return;

        System.Diagnostics.Debug.WriteLine($"[API] Syncing {pending.Count} pending histories...");

        foreach (var item in pending)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { poiId = item.PoiId, userId = item.UserId });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{ApiConfig.BaseUrl}history", content);

                if (response.IsSuccessStatusCode)
                    await LocalDbService.Instance.MarkHistorySyncedAsync(item.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Sync failed for item {item.Id}: {ex.Message}");
            }
        }

        await LocalDbService.Instance.CleanSyncedHistoriesAsync();
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    public async Task<UserDto?> Login(string username, string password)
    {
        var response = await client.PostAsJsonAsync(
            $"{ApiConfig.BaseUrl}users/login",
            new { Username = username, Password = password });

        if (!response.IsSuccessStatusCode) return null;

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        if (user != null) SessionService.CurrentUser = user;
        return user;
    }

    public async Task<UserDto?> Register(string username, string name, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            $"{ApiConfig.BaseUrl}users/register",
            new { Username = username, Name = name, Email = email, PasswordHash = password });

        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserDto>();
    }

    public async Task<bool> UpdateProfile(int userId, string name, string phone)
    {
        var response = await client.PutAsJsonAsync(
            $"{ApiConfig.BaseUrl}users/customer/{userId}",
            new { Name = name, Phone = phone });
        return response.IsSuccessStatusCode;
    }
}
