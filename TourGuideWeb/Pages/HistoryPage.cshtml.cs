using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;

namespace GPSGuide.Web.Pages;

public class HistoryPageModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public HistoryPageModel(IHttpClientFactory http) => _http = http;

    public List<HistoryItem> AllHistory { get; set; } = [];
    public List<HistoryItem> LatestHistory { get; set; } = [];

    public int TotalCount { get; set; }
    public int TotalDuration { get; set; }
    public double AverageDuration { get; set; }

    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    public bool IsAdmin => Role == "ADMIN";
    private int? MyId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;

    public List<PoiStat> PoiStats => AllHistory
        .GroupBy(h => new { h.PoiId, h.PoiName })
        .Select(g =>
        {
            var playCount = g.Count();
            var totalPoiDuration = g.Sum(h => h.ListenDuration);
            var avgDur = playCount > 0 ? (double)totalPoiDuration / playCount : 0;

            return new PoiStat(
                g.Key.PoiId,
                g.Key.PoiName ?? $"POI #{g.Key.PoiId}",
                playCount,
                g.Max(h => h.PlayTime),
                avgDur < 60 ? $"{(int)avgDur}s" : $"{(int)avgDur / 60}ph {(int)avgDur % 60}s"
            );
        })
        .OrderByDescending(p => p.PlayCount)
        .ToList();

    public async Task OnGetAsync()
    {
        var client = _http.CreateClient("API");
        try
        {
            var rawAll = await client.GetFromJsonAsync<List<HistoryItem>>("history") ?? [];

            if (IsAdmin)
            {
                AllHistory = rawAll;
            }
            else
            {
                var myPois = await client.GetFromJsonAsync<List<PoiItem>>($"poi/owner/{MyId}") ?? [];
                var myPoiIds = myPois.Select(p => p.Id).ToHashSet();
                AllHistory = rawAll.Where(h => myPoiIds.Contains(h.PoiId)).ToList();
            }

            TotalCount = AllHistory.Count;
            TotalDuration = AllHistory.Sum(h => h.ListenDuration);
            AverageDuration = TotalCount > 0 ? (double)TotalDuration / TotalCount : 0;

            LatestHistory = AllHistory.OrderByDescending(h => h.PlayTime).Take(5).ToList();
        }
        catch { AllHistory = []; LatestHistory = []; }
    }

    public record HistoryItem(int Id, int PoiId, string? PoiName, string UserLogin, string UserFullName, DateTime PlayTime, int ListenDuration);
    public record PoiStat(int PoiId, string Name, int PlayCount, DateTime LastPlay, string AvgInterval);
    private record PoiItem(int Id);
}