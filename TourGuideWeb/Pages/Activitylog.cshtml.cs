using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class ActivityLogModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public ActivityLogModel(IHttpClientFactory http) => _http = http;

    public List<ActivityItem> History { get; set; } = [];
    [TempData] public string Msg { get; set; } = "";
    public string Error { get; set; } = "";

    private string Role => HttpContext.Session.GetString("Role") ?? "";
    private bool IsAdmin => Role == "ADMIN";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        try
        {
            var client = _http.CreateClient("API");
            History = await client.GetFromJsonAsync<List<ActivityItem>>("history") ?? [];
        }
        catch (Exception ex) { Error = "Không kết nối được API: " + ex.Message; }
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int deleteId)
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        try
        {
            var client = _http.CreateClient("API");
            await client.DeleteAsync($"history/{deleteId}");
            Msg = "Đã xóa lịch sử.";
        }
        catch { }
        return RedirectToPage();
    }

    public record ActivityItem(
        int Id,
        int PoiId,
        string? PoiName,
        int UserId,
        string? UserLogin,
        string? UserFullName,
        string? UserRole,
        DateTime PlayTime
    );
}