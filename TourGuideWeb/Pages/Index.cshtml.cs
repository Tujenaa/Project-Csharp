using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;

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

    public async Task OnGetAsync()
    {
        ApiUrl = _config["ApiUrl"] ?? "(chưa cấu hình)";
        var client = _http.CreateClient("API");

        var pois = await Fetch<List<POI>>(client, "poi");
        var audios = await Fetch<List<AudioItem>>(client, "audio");
        var history = await Fetch<List<HistoryItem>>(client, "history");
        var users = await Fetch<List<UserItem>>(client, "users");

        ApiError = pois is null && audios is null && history is null && users is null;

        TotalPoi = pois?.Count ?? 0;
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