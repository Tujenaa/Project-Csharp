using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;

using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class HistoryPageModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public HistoryPageModel(IHttpClientFactory http) => _http = http;


    [TempData] public string Msg { get; set; } = "";
    [TempData] public string Error { get; set; } = "";

    public IList<PlayHistory> History { get; private set; } = new List<PlayHistory>();
    public IList<POI> Pois { get; private set; } = new List<POI>();

    // Bound properties cho form thêm thủ công
    [BindProperty] public int PoiId { get; set; }
    [BindProperty] public DateTime PlayTime { get; set; } = DateTime.Now;
    [BindProperty] public int DeleteId { get; set; }

    private HttpClient Api => _http.CreateClient("API");

    public async Task OnGetAsync()
    {
        try
        {
            History = await Api.GetFromJsonAsync<List<PlayHistory>>("history") ?? new();
            Pois = await Api.GetFromJsonAsync<List<POI>>("poi") ?? new();
        }
        catch (Exception ex)
        {
            Error = "Không kết nối được API: " + ex.Message;
        }
    }

    // POST: thêm lịch sử thủ công
    public async Task<IActionResult> OnPostAsync()
    {
        var body = new { PoiId, PlayTime };
        try
        {
            var resp = await Api.PostAsJsonAsync("history", body);
            if (resp.IsSuccessStatusCode)
                Msg = "Đã thêm lịch sử thành công.";
            else
                Error = $"API lỗi {(int)resp.StatusCode}: {resp.ReasonPhrase}";
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }

        return RedirectToPage();
    }

    // POST: xoá
    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            var resp = await Api.DeleteAsync($"history/{DeleteId}");
            if (resp.IsSuccessStatusCode)
                Msg = "Đã xoá bản ghi lịch sử.";
            else
                Error = $"Xoá thất bại: {(int)resp.StatusCode} {resp.ReasonPhrase}";
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }

        return RedirectToPage();
    }

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