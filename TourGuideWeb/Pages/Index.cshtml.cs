using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;
public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    public IndexModel(IHttpClientFactory http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }
    public int TotalPoi { get; set; }
    public int TotalAudio { get; set; }
    public int TotalHistory { get; set; }
    public int TotalUsers { get; set; }
    public bool ApiError { get; set; }
    public string ApiUrl { get; set; } = "";
    public List<HistoryItem> RecentHistory { get; set; } = [];
    public List<KeyValuePair<string, int>> TopPoi { get; set; } = [];
    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    public bool IsAdmin => Role == "ADMIN";
    private int? MyId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;

    public async Task OnGetAsync()
    {
        ApiUrl = _config["ApiUrl"] ?? "";
        var client = _http.CreateClient("API");
        List<POI>? pois = null;
        List<AudioItem>? audios = null;
        List<HistoryItem>? history = null;
        List<UserItem>? users = null;

        if (IsAdmin)
        {
            pois = await Fetch<List<POI>>(client, "poi/all");
            audios = await Fetch<List<AudioItem>>(client, "audio");
            history = await Fetch<List<HistoryItem>>(client, "history");
            users = await Fetch<List<UserItem>>(client, "users");
        }
        else
        {
            pois = await Fetch<List<POI>>(client, $"poi/owner/{MyId}");
            audios = await Fetch<List<AudioItem>>(client, "audio");
            history = await Fetch<List<HistoryItem>>(client, "history");
            var myPoiIds = (pois ?? []).Select(p => p.Id).ToHashSet();
            audios = audios?.Where(a => myPoiIds.Contains(a.PoiId)).ToList();
            history = history?.Where(h => myPoiIds.Contains(h.PoiId)).ToList();
        }

        ApiError = pois is null && audios is null && history is null;

        if (IsAdmin)
        {
            // Admin: chỉ đếm POI đang active (không tính REJECTED)
            TotalPoi = pois?.Count(p => p.Status != "REJECTED") ?? 0;
        }
        else
        {
            // Owner: đếm tất cả POI của mình (trừ REJECTED)
            TotalPoi = pois?.Count(p => p.Status != "REJECTED") ?? 0;
        }

        TotalAudio = audios?.Count ?? 0;
        TotalHistory = history?.Count ?? 0;
        TotalUsers = users?.Count ?? 0;

        RecentHistory = (history ?? [])
            .OrderByDescending(h => h.PlayTime)
            .Take(10)
            .ToList();

        TopPoi = (history ?? [])
            .GroupBy(h => h.PoiName ?? $"POI #{h.PoiId}")
            .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .ToList();
    }

    private static async Task<T?> Fetch<T>(HttpClient client, string endpoint)
    {
        try { return await client.GetFromJsonAsync<T>(endpoint); }
        catch { return default; }
    }

    public record HistoryItem(int Id, int PoiId, string? PoiName, DateTime PlayTime);
    private record AudioItem(int Id, int PoiId);
    private record UserItem(int Id, string Username);
}