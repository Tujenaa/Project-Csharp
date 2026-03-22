using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;

namespace GPSGuide.Web.Pages;

public class HistoryPageModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public HistoryPageModel(IHttpClientFactory http) => _http = http;

    public List<HistoryItem> History { get; set; } = [];

    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    private bool IsAdmin => Role == "ADMIN";
    private int? MyId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;

    public async Task OnGetAsync()
    {
        var client = _http.CreateClient("API");
        try
        {
            var all = await client.GetFromJsonAsync<List<HistoryItem>>("history") ?? [];

            if (IsAdmin)
            {
                History = all;
            }
            else
            {
                // Owner chỉ thấy lịch sử của POI thuộc mình
                var myPois = await client.GetFromJsonAsync<List<PoiItem>>($"poi/owner/{MyId}") ?? [];
                var myPoiIds = myPois.Select(p => p.Id).ToHashSet();
                History = all.Where(h => myPoiIds.Contains(h.PoiId)).ToList();
            }
        }
        catch { History = []; }
    }

    public record HistoryItem(int Id, int PoiId, string? PoiName, DateTime PlayTime);
    private record PoiItem(int Id);
}