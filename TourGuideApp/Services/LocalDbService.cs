using SQLite;
using System.Text.Json;
using TourGuideApp.Models;

namespace TourGuideApp.Services;

/// <summary>
/// SQLite local database service — cache POI và lịch sử khi offline.
/// Tự động sync lên server khi kết nối mạng trở lại.
/// </summary>
public class LocalDbService
{
    static readonly string DbPath = Path.Combine(
        FileSystem.AppDataDirectory, "tourguide_local.db3");

    SQLiteAsyncConnection? _db;

    static LocalDbService? _instance;
    public static LocalDbService Instance => _instance ??= new LocalDbService();

    // ── Init ──────────────────────────────────────────────────────────────────

    public async Task InitAsync()
    {
        if (_db != null) return;

        _db = new SQLiteAsyncConnection(DbPath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        await _db.CreateTableAsync<CachedPOI>();
        await _db.CreateTableAsync<CachedTour>();
        await _db.CreateTableAsync<PendingHistory>();

        // Tự động seed dữ liệu JSON nếu bảng POI trống (Cung cấp sẵn cho Offline Mode)
        var count = await _db.Table<CachedPOI>().CountAsync();
        if (count == 0)
        {
            await SeedDataFromJsonAsync();
        }

        var tourCount = await _db.Table<CachedTour>().CountAsync();
        if (tourCount == 0)
        {
            await SeedToursFromJsonAsync();
        }

        System.Diagnostics.Debug.WriteLine($"[LocalDB] Initialized at: {DbPath}");
    }

    private async Task SeedDataFromJsonAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("seed_pois.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var pois = JsonSerializer.Deserialize<List<POI>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (pois != null && pois.Count > 0)
            {
                var cached = pois.Select(p => new CachedPOI
                {
                    Id = p.Id,
                    Name = p.Name ?? "",
                    Description = p.Description ?? "",
                    Address = p.Address ?? "",
                    Phone = p.Phone ?? "",
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Radius = p.Radius,
                    ScriptVi = p.ScriptVi ?? "",
                    ScriptEn = p.ScriptEn ?? "",
                    ScriptJa = p.ScriptJa ?? "",
                    ScriptZh = p.ScriptZh ?? "",
                    ImagesJson = JsonSerializer.Serialize(p.Images),
                    CachedAt = DateTime.UtcNow
                }).ToList();

                await _db!.InsertAllAsync(cached);
                System.Diagnostics.Debug.WriteLine($"[LocalDB] Mồi thành công {cached.Count} dữ liệu POI từ file seed_pois.json");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalDB] Lỗi không thể đọc file seed_pois.json: {ex.Message}");
        }
    }

    private async Task SeedToursFromJsonAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("seed_tours.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var tours = JsonSerializer.Deserialize<List<Tour>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (tours != null && tours.Count > 0)
            {
                await SaveToursAsync(tours);
                System.Diagnostics.Debug.WriteLine($"[LocalDB] Mồi thành công {tours.Count} dữ liệu Tour từ file seed_tours.json");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalDB] Lỗi không thể đọc file seed_tours.json: {ex.Message}");
        }
    }


    // ── POI Cache ─────────────────────────────────────────────────────────────

    /// <summary>Lưu toàn bộ danh sách POI xuống local (replace).</summary>
    public async Task SavePOIsAsync(List<POI> pois)
    {
        await InitAsync();
        var cached = pois.Select(p => new CachedPOI
        {
            Id          = p.Id,
            Name        = p.Name ?? "",
            Description = p.Description ?? "",
            Address     = p.Address ?? "",
            Phone       = p.Phone ?? "",
            Latitude    = p.Latitude,
            Longitude   = p.Longitude,
            Radius      = p.Radius,
            ScriptVi    = p.ScriptVi ?? "",
            ScriptEn    = p.ScriptEn ?? "",
            ScriptJa    = p.ScriptJa ?? "",
            ScriptZh    = p.ScriptZh ?? "",
            ImagesJson  = JsonSerializer.Serialize(p.Images),
            CachedAt    = DateTime.UtcNow
        }).ToList();

        await _db!.DeleteAllAsync<CachedPOI>();
        await _db.InsertAllAsync(cached);

        System.Diagnostics.Debug.WriteLine($"[LocalDB] Saved {cached.Count} POIs");
    }

    /// <summary>Cập nhật hoặc chèn mới một POI đơn lẻ vào cache.</summary>
    public async Task UpdateSinglePOIAsync(POI p)
    {
        await InitAsync();
        var c = new CachedPOI
        {
            Id          = p.Id,
            Name        = p.Name ?? "",
            Description = p.Description ?? "",
            Address     = p.Address ?? "",
            Phone       = p.Phone ?? "",
            Latitude    = p.Latitude,
            Longitude   = p.Longitude,
            Radius      = p.Radius,
            ScriptVi    = p.ScriptVi ?? "",
            ScriptEn    = p.ScriptEn ?? "",
            ScriptJa    = p.ScriptJa ?? "",
            ScriptZh    = p.ScriptZh ?? "",
            ImagesJson  = JsonSerializer.Serialize(p.Images),
            CachedAt    = DateTime.UtcNow
        };
        await _db!.InsertOrReplaceAsync(c);
        System.Diagnostics.Debug.WriteLine($"[LocalDB] Updated single POI cache: {p.Id}");
    }

    /// <summary>Đọc POI từ local DB (dùng khi offline).</summary>
    public async Task<List<POI>> GetCachedPOIsAsync()
    {
        await InitAsync();
        var cached = await _db!.Table<CachedPOI>().ToListAsync();

        return cached.Select(c => new POI
        {
            Id          = c.Id,
            Name        = c.Name,
            Description = c.Description,
            Address     = c.Address,
            Phone       = c.Phone,
            Latitude    = c.Latitude,
            Longitude   = c.Longitude,
            Radius      = c.Radius,
            ScriptVi    = c.ScriptVi,
            ScriptEn    = c.ScriptEn,
            ScriptJa    = c.ScriptJa,
            ScriptZh    = c.ScriptZh,
            Images      = string.IsNullOrEmpty(c.ImagesJson) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(c.ImagesJson) ?? new List<string>()
        }).ToList();
    }

    // ── Tour Cache ────────────────────────────────────────────────────────────

    public async Task SaveToursAsync(List<Tour> tours)
    {
        await InitAsync();
        var cached = tours.Select(t => new CachedTour
        {
            Id           = t.Id,
            Name         = t.Name ?? "",
            Description  = t.Description ?? "",
            ThumbnailUrl = t.ThumbnailUrl ?? "",
            Status       = t.Status ?? "PUBLISHED",
            PoisJson     = JsonSerializer.Serialize(t.POIs),
            CachedAt     = DateTime.UtcNow
        }).ToList();

        await _db!.DeleteAllAsync<CachedTour>();
        await _db.InsertAllAsync(cached);
        System.Diagnostics.Debug.WriteLine($"[LocalDB] Saved {cached.Count} Tours");
    }

    public async Task<List<Tour>> GetCachedToursAsync()
    {
        await InitAsync();
        var cached = await _db!.Table<CachedTour>().ToListAsync();

        return cached.Select(c => new Tour
        {
            Id           = c.Id,
            Name         = c.Name,
            Description  = c.Description,
            ThumbnailUrl = c.ThumbnailUrl,
            Status       = c.Status,
            POIs         = string.IsNullOrEmpty(c.PoisJson) ? new List<POI>() : JsonSerializer.Deserialize<List<POI>>(c.PoisJson) ?? new List<POI>()
        }).ToList();
    }

    public async Task<bool> HasCachedPOIsAsync()
    {
        await InitAsync();
        return await _db!.Table<CachedPOI>().CountAsync() > 0;
    }

    /// <summary>Thời điểm cache POI gần nhất (để hiển thị thông báo offline).</summary>
    public async Task<DateTime?> GetLastCacheTimeAsync()
    {
        await InitAsync();
        var first = await _db!.Table<CachedPOI>().FirstOrDefaultAsync();
        return first?.CachedAt;
    }

    // ── Pending History (offline queue) ──────────────────────────────────────

    /// <summary>Thêm history vào hàng đợi local khi chưa sync được.</summary>
    public async Task AddPendingHistoryAsync(int poiId, int userId, int listenDuration)
    {
        await InitAsync();
        await _db!.InsertAsync(new PendingHistory
        {
            PoiId          = poiId,
            UserId         = userId,
            PlayTime       = DateTime.UtcNow,
            ListenDuration = listenDuration,
            Synced         = false
        });
        System.Diagnostics.Debug.WriteLine($"[LocalDB] Queued pending history: poi={poiId}, duration={listenDuration}s");
    }

    /// <summary>Lấy tất cả history chưa được sync.</summary>
    public async Task<List<PendingHistory>> GetPendingHistoriesAsync()
    {
        await InitAsync();
        return await _db!.Table<PendingHistory>()
            .Where(h => !h.Synced)
            .ToListAsync();
    }

    /// <summary>Đánh dấu history đã sync xong.</summary>
    public async Task MarkHistorySyncedAsync(int id)
    {
        await InitAsync();
        var item = await _db!.FindAsync<PendingHistory>(id);
        if (item != null)
        {
            item.Synced = true;
            await _db.UpdateAsync(item);
        }
    }

    /// <summary>Xóa các history đã sync để không chiếm bộ nhớ.</summary>
    public async Task CleanSyncedHistoriesAsync()
    {
        await InitAsync();
        await _db!.ExecuteAsync("DELETE FROM PendingHistory WHERE Synced = 1");
    }
}

// ── SQLite table models ───────────────────────────────────────────────────────

[Table("POICache")]
public class CachedPOI
{
    [PrimaryKey]
    public int Id { get; set; }

    public string Name        { get; set; } = "";
    public string Description { get; set; } = "";
    public string Address     { get; set; } = "";
    public string Phone       { get; set; } = "";
    public double Latitude    { get; set; }
    public double Longitude   { get; set; }
    public int    Radius      { get; set; }
    public string ScriptVi    { get; set; } = "";
    public string ScriptEn    { get; set; } = "";
    public string ScriptJa    { get; set; } = "";
    public string ScriptZh    { get; set; } = "";
    public string ImagesJson  { get; set; } = "[]";
    public DateTime CachedAt  { get; set; }
}

[Table("PendingHistory")]
public class PendingHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int      PoiId          { get; set; }
    public int      UserId         { get; set; }
    public DateTime PlayTime       { get; set; }
    public int      ListenDuration { get; set; }
    public bool     Synced         { get; set; }
}

[Table("TourCache")]
public class CachedTour
{
    [PrimaryKey]
    public int Id { get; set; }
    public string Name         { get; set; } = "";
    public string Description  { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string Status       { get; set; } = "PUBLISHED";
    public string PoisJson     { get; set; } = "[]";
    public DateTime CachedAt   { get; set; }
}
