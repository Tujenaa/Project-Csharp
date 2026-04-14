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
        public static string BaseUrl => DeviceInfo.DeviceType == DeviceType.Virtual 
            ? "http://10.0.2.2:5266/api/" 
            : "http://192.168.1.139:5266/api/";
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
                    return pois.Where(p => p.IsReady).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetPOI failed: {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine("[API] GetPOI → using offline cache");
        var cached = await LocalDbService.Instance.GetCachedPOIsAsync();
        return cached.Where(p => p.IsReady).ToList();
    }

    /// <summary>Lấy thông tin một POI từ API và cập nhật cache lẻ.</summary>
    public async Task<POI?> GetPOIById(int id)
    {
        if (ConnectivityService.IsConnected)
        {
            try
            {
                var poi = await client.GetFromJsonAsync<POI>($"{ApiConfig.BaseUrl}POI/{id}");
                if (poi != null)
                {
                    await LocalDbService.Instance.UpdateSinglePOIAsync(poi);
                    return poi;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetPOIById failed: {ex.Message}");
            }
        }
        return null;
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
        return cached.Where(p => p.IsReady).Take(5).ToList();
    }

    public async Task<int> GetPoiCount()
    {
        if (ConnectivityService.IsConnected)
        {
            try { return await client.GetFromJsonAsync<int>($"{ApiConfig.BaseUrl}POI/count"); }
            catch { }
        }
        var cached = await LocalDbService.Instance.GetCachedPOIsAsync();
        return cached.Count(p => p.IsReady);
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
    public async Task SaveHistory(int poiId, int userId, int listenDuration)
    {
        if (ConnectivityService.IsConnected)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { poiId, userId, listenDuration });
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
        await LocalDbService.Instance.AddPendingHistoryAsync(poiId, userId, listenDuration);
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
                var json = JsonSerializer.Serialize(new { 
                    poiId = item.PoiId, 
                    userId = item.UserId,
                    listenDuration = item.ListenDuration
                });
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

    public async Task<bool> ChangePassword(int userId, string oldPassword, string newPassword)
    {
        var response = await client.PutAsJsonAsync(
            $"{ApiConfig.BaseUrl}users/change-password/{userId}",
            new { OldPassword = oldPassword, NewPassword = newPassword });
        return response.IsSuccessStatusCode;
    }

    // ── Tours ─────────────────────────────────────────────────────────────────

    public async Task<List<TourGuideApp.Models.Tour>> GetTours()
    {
        if (ConnectivityService.IsConnected)
        {
            try
            {
                var tours = await client.GetFromJsonAsync<List<TourGuideApp.Models.Tour>>(
                    $"{ApiConfig.BaseUrl}tours")
                    ?? new List<TourGuideApp.Models.Tour>();
                
                if (tours.Count > 0)
                {
                    _ = LocalDbService.Instance.SaveToursAsync(tours);
                    foreach (var t in tours)
                    {
                        if (t.POIs != null) t.POIs = t.POIs.Where(p => p.IsApprovedInTour).ToList();
                    }
                    return tours.Where(t => t.POIs != null && t.POIs.Count > 0).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetTours failed: {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine("[API] GetTours → using offline cache");
        var cachedTours = await LocalDbService.Instance.GetCachedToursAsync();
        foreach (var t in cachedTours)
        {
            if (t.POIs != null) t.POIs = t.POIs.Where(p => p.IsApprovedInTour).ToList();
        }
        return cachedTours.Where(t => t.POIs != null && t.POIs.Count > 0).ToList();
    }

    public async Task<List<TourGuideApp.Models.Tour>> GetTopTours(int count = 2)
    {
        var all = await GetTours();
        return all.Take(count).ToList();
    }

    public async Task<TourGuideApp.Models.Tour?> GetTourById(int id)
    {
        if (ConnectivityService.IsConnected)
        {
            try
            {
                var tour = await client.GetFromJsonAsync<TourGuideApp.Models.Tour>(
                    $"{ApiConfig.BaseUrl}tours/{id}");
                if (tour != null && tour.POIs != null)
                {
                    tour.POIs = tour.POIs.Where(p => p.IsApprovedInTour).ToList();
                }
                return tour;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetTourById failed: {ex.Message}");
            }
        }
        return null;
    }

    public async Task<int> GetTourCount()
    {
        var all = await GetTours();
        return all.Count;
    }

    public async Task<List<Language>> GetLanguages()
    {
        if (ConnectivityService.IsConnected)
        {
            try
            {
                return await client.GetFromJsonAsync<List<Language>>(
                    $"{ApiConfig.BaseUrl}languages")
                    ?? new List<Language>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetLanguages failed: {ex.Message}");
            }
        }
        return new List<Language>();
    }
}
// Thêm ở cuối class - nhưng cần thêm vào trong class body
