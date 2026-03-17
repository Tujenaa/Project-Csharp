using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace GPSGuide.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _http;

    public int TotalPois { get; set; }
    public int TotalPlays { get; set; }
    public int TotalUsers { get; set; }
    public int TotalHistory { get; set; }
    public string ErrorMessage { get; set; } = "";

    public List<PoiStat> TopPois { get; set; } = new();
    public List<PlayHistory> RecentHistory { get; set; } = new();

    public IndexModel(IHttpClientFactory http) => _http = http;

    public async Task OnGetAsync()
    {
        var api = _http.CreateClient("API");
        try
        {
            // Lấy song song để nhanh hơn
            var taskPois = api.GetFromJsonAsync<List<POI>>("poi");
            var taskUsers = api.GetFromJsonAsync<List<User>>("users");
            var taskHistory = api.GetFromJsonAsync<List<PlayHistory>>("history");

            await Task.WhenAll(
                taskPois ?? Task.FromResult<List<POI>?>(null),
                taskUsers ?? Task.FromResult<List<User>?>(null),
                taskHistory ?? Task.FromResult<List<PlayHistory>?>(null)
            );

            var pois = taskPois?.Result ?? new();
            var users = taskUsers?.Result ?? new();
            var history = taskHistory?.Result ?? new();

            TotalPois = pois.Count;
            TotalUsers = users.Count;
            TotalHistory = history.Count;
            TotalPlays = history.Count;

            // Top 5 POI được phát nhiều nhất
            TopPois = history
                .GroupBy(h => new { h.PoiId, h.PoiName })
                .Select(g => new PoiStat
                {
                    Name = g.Key.PoiName ?? $"POI #{g.Key.PoiId}",
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList();

            // 5 lượt phát gần nhất
            RecentHistory = history
                .OrderByDescending(h => h.PlayTime)
                .Take(5)
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Không kết nối được API ({ex.Message}). " +
                           "Kiểm tra TourGuide.API đã chạy chưa và ApiBaseUrl trong appsettings.json.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Lỗi: " + ex.Message;
        }
    }

    public class PoiStat
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }
}