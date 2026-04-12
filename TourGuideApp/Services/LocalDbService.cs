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
        else
        {
            // BUG FIX: Phát hiện cache POI cũ bị hỏng (LanguageCode=null) và xóa để force re-fetch
            // Nguyên nhân: bug deserialization cũ không map được Language.Code vào LanguageCodeFlat
            await MigrateInvalidPOICacheAsync();
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
                    // BUG FIX: Normalize trước khi serialize để LanguageCodeFlat luôn có giá trị
                    AudiosJson = JsonSerializer.Serialize(NormalizeAudios(p.Audios)),
                    ImagesJson = JsonSerializer.Serialize(p.Images),
                    Status = p.Status,
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
            Id = p.Id,
            Name = p.Name ?? "",
            Description = p.Description ?? "",
            Address = p.Address ?? "",
            Phone = p.Phone ?? "",
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            Radius = p.Radius,
            // BUG FIX: Normalize trước khi serialize để LanguageCodeFlat luôn có giá trị
            AudiosJson = JsonSerializer.Serialize(NormalizeAudios(p.Audios)),
            ImagesJson = JsonSerializer.Serialize(p.Images),
            Status = p.Status,
            CachedAt = DateTime.UtcNow
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
            Id = p.Id,
            Name = p.Name ?? "",
            Description = p.Description ?? "",
            Address = p.Address ?? "",
            Phone = p.Phone ?? "",
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            Radius = p.Radius,
            // BUG FIX: Normalize trước khi serialize để LanguageCodeFlat luôn có giá trị
            AudiosJson = JsonSerializer.Serialize(NormalizeAudios(p.Audios)),
            ImagesJson = JsonSerializer.Serialize(p.Images),
            Status = p.Status,
            CachedAt = DateTime.UtcNow
        };
        await _db!.InsertOrReplaceAsync(c);
        System.Diagnostics.Debug.WriteLine($"[LocalDB] Updated single POI cache: {p.Id}");
    }

    private static readonly JsonSerializerOptions _caseInsensitive = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Đọc POI từ local DB (dùng khi offline).</summary>
    public async Task<List<POI>> GetCachedPOIsAsync()
    {
        await InitAsync();
        var cached = await _db!.Table<CachedPOI>().ToListAsync();

        return cached.Select(c => new POI
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            Address = c.Address,
            Phone = c.Phone,
            Latitude = c.Latitude,
            Longitude = c.Longitude,
            Radius = c.Radius,
            // BUG FIX: Dùng PropertyNameCaseInsensitive để deserialize đúng nested Language object
            // (JSON lưu trong cache có thể là camelCase "language" thay vì PascalCase "Language")
            Audios = string.IsNullOrEmpty(c.AudiosJson)
                ? new List<Audio>()
                : JsonSerializer.Deserialize<List<Audio>>(c.AudiosJson, _caseInsensitive) ?? new List<Audio>(),
            Images = string.IsNullOrEmpty(c.ImagesJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(c.ImagesJson, _caseInsensitive) ?? new List<string>(),
            Status = c.Status
        }).Where(p => p.IsReady).ToList();
    }

    // ── Tour Cache ────────────────────────────────────────────────────────────

    public async Task SaveToursAsync(List<Tour> tours)
    {
        await InitAsync();
        var cached = tours.Select(t => new CachedTour
        {
            Id = t.Id,
            Name = t.Name ?? "",
            Description = t.Description ?? "",
            ThumbnailUrl = t.ThumbnailUrl ?? "",
            Status = t.Status ?? "PUBLISHED",
            PoisJson = JsonSerializer.Serialize(t.POIs),
            CachedAt = DateTime.UtcNow
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
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            ThumbnailUrl = c.ThumbnailUrl,
            Status = c.Status,
            // BUG FIX: Dùng PropertyNameCaseInsensitive để deserialize đúng POI lồng trong Tour
            POIs = string.IsNullOrEmpty(c.PoisJson)
                ? new List<POI>()
                : JsonSerializer.Deserialize<List<POI>>(c.PoisJson, _caseInsensitive) ?? new List<POI>()
        }).Select(t => {
            if (t.POIs != null) t.POIs = t.POIs.Where(p => p.IsApprovedInTour).ToList();
            return t;
        }).Where(t => t.POIs != null && t.POIs.Count > 0).ToList();
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
            PoiId = poiId,
            UserId = userId,
            PlayTime = DateTime.UtcNow,
            ListenDuration = listenDuration,
            Synced = false
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



    /// <summary>
    /// Xóa các bản dịch rác bị lưu nhầm từ bug cũ (text quá ngắn, không phải script thật).
    /// Chạy một lần khi khởi động để tự heal database.
    /// </summary>
    /// <summary>
    /// Đảm bảo mỗi Audio trong list có LanguageCodeFlat được set từ Language.Code.
    /// Phải gọi trước khi serialize Audio vào SQLite để tránh mất thông tin ngôn ngữ.
    /// </summary>
    private static List<Audio> NormalizeAudios(List<Audio>? audios)
    {
        if (audios == null) return new List<Audio>();
        foreach (var a in audios)
        {
            // Nếu LanguageCodeFlat chưa có giá trị, copy từ Language.Code (API nested object)
            if (string.IsNullOrEmpty(a.LanguageCodeFlat) && !string.IsNullOrEmpty(a.Language?.Code))
                a.LanguageCodeFlat = a.Language.Code;
        }
        return audios;
    }

    /// <summary>
    /// Phát hiện cache POI cũ bị hỏng (Audio.LanguageCode = null do bug deserialization).
    /// Nếu phát hiện → xóa toàn bộ POICache để app re-fetch khi online.
    /// </summary>
    private async Task MigrateInvalidPOICacheAsync()
    {
        try
        {
            var sample = await _db!.Table<CachedPOI>().FirstOrDefaultAsync();
            if (sample == null || string.IsNullOrEmpty(sample.AudiosJson)) return;

            var audios = JsonSerializer.Deserialize<List<Audio>>(sample.AudiosJson, _caseInsensitive);

            // Cache bị hỏng nếu có Audio có Script nhưng LanguageCode lại null
            bool isCorrupt = audios?.Any(a =>
                !string.IsNullOrWhiteSpace(a.Script) && a.LanguageCode == null) ?? false;

            if (isCorrupt)
            {
                await _db.DeleteAllAsync<CachedPOI>();
                System.Diagnostics.Debug.WriteLine(
                    "[LocalDB] ⚠️ Corrupt POI cache detected (LanguageCode=null). Cleared all POI cache. Will re-fetch on next online session.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalDB] MigrateInvalidPOICacheAsync error: {ex.Message}");
        }
    }


}

// ── SQLite table models ───────────────────────────────────────────────────────

[Table("POICache")]
public class CachedPOI
{
    [PrimaryKey]
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Radius { get; set; }
    public string AudiosJson { get; set; } = "[]";
    public string ImagesJson { get; set; } = "[]";
    public string Status { get; set; } = "APPROVED";
    public DateTime CachedAt { get; set; }
}

[Table("PendingHistory")]
public class PendingHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int PoiId { get; set; }
    public int UserId { get; set; }
    public DateTime PlayTime { get; set; }
    public int ListenDuration { get; set; }
    public bool Synced { get; set; }
}

[Table("TourCache")]
public class CachedTour
{
    [PrimaryKey]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string Status { get; set; } = "PUBLISHED";
    public string PoisJson { get; set; } = "[]";
    public DateTime CachedAt { get; set; }
}

