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

    // Leaderboard toàn cục (tất cả POI, không filter theo owner)
    public List<PoiStat> GlobalPoiStats { get; set; } = [];

    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    public bool IsAdmin => Role == "ADMIN";
    private int? MyId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;

    // PoiStats: vẫn filter theo owner (dùng cho tab Xếp hạng của owner)
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

    // POI của owner có thứ hạng cao nhất trong leaderboard toàn cục
    public (PoiStat Poi, int Rank)? OwnerTopRank { get; set; }

    public async Task OnGetAsync()
    {
        var client = _http.CreateClient("API");
        try
        {
            var rawAll = await client.GetFromJsonAsync<List<HistoryItem>>("history") ?? [];

            // Tính leaderboard toàn cục từ rawAll
            GlobalPoiStats = rawAll
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

            if (IsAdmin)
            {
                AllHistory = rawAll;
            }
            else
            {
                var myPois = await client.GetFromJsonAsync<List<PoiItem>>($"poi/owner/{MyId}") ?? [];
                var myPoiIds = myPois.Select(p => p.Id).ToHashSet();
                AllHistory = rawAll.Where(h => myPoiIds.Contains(h.PoiId)).ToList();

                // Tìm POI của owner có rank cao nhất trong leaderboard toàn cục
                for (int i = 0; i < GlobalPoiStats.Count; i++)
                {
                    if (myPoiIds.Contains(GlobalPoiStats[i].PoiId))
                    {
                        OwnerTopRank = (GlobalPoiStats[i], i + 1);
                        break;
                    }
                }
            }

            TotalCount = AllHistory.Count;
            TotalDuration = AllHistory.Sum(h => h.ListenDuration);
            AverageDuration = TotalCount > 0 ? (double)TotalDuration / TotalCount : 0;

            LatestHistory = AllHistory.OrderByDescending(h => h.PlayTime).Take(5).ToList();
        }
        catch { AllHistory = []; LatestHistory = []; GlobalPoiStats = []; }
    }

    public record HistoryItem(int Id, int PoiId, string? PoiName, string? UserLogin, string? UserFullName, DateTime PlayTime, int ListenDuration);
    public record PoiStat(int PoiId, string Name, int PlayCount, DateTime LastPlay, string AvgInterval);
    private record PoiItem(int Id);
}