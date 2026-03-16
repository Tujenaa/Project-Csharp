using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;

namespace GPSGuide.Web.Pages;

public class HistoryPageModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public HistoryPageModel(IHttpClientFactory http) => _http = http;

    public List<HistoryItem> History { get; set; } = [];
    public List<POI> Pois { get; set; } = [];
    [TempData] public string Msg { get; set; } = "";

    [BindProperty] public int PoiId { get; set; }
    [BindProperty] public DateTime? PlayTime { get; set; }
    [BindProperty] public int DeleteId { get; set; }

    public async Task OnGetAsync()
    {
        var client = _http.CreateClient("API");
        try { History = await client.GetFromJsonAsync<List<HistoryItem>>("history") ?? []; } catch { History = []; }
        try { Pois = await client.GetFromJsonAsync<List<POI>>("poi") ?? []; } catch { Pois = []; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = _http.CreateClient("API");
        var payload = new { PoiId, PlayTime = PlayTime ?? DateTime.Now };
        await client.PostAsJsonAsync("history", payload);
        Msg = "Đã thêm lịch sử phát.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var client = _http.CreateClient("API");
        await client.DeleteAsync($"history/{DeleteId}");
        Msg = "Đã xoá bản ghi.";
        return RedirectToPage();
    }

    public record HistoryItem(int Id, int PoiId, string? PoiName, DateTime PlayTime);
}