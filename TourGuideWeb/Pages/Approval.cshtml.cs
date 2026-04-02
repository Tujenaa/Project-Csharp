using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class ApprovalModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public ApprovalModel(IHttpClientFactory http) => _http = http;

    [TempData] public string Msg { get; set; } = "";
    public List<POI> AllPois { get; set; } = [];

    [BindProperty] public int PoiId { get; set; }
    [BindProperty] public string Reason { get; set; } = "";

    private string Role => HttpContext.Session.GetString("Role") ?? "";
    private bool IsAdmin => Role == "ADMIN";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        var client = _http.CreateClient("API");
        try { AllPois = await client.GetFromJsonAsync<List<POI>>("poi/all") ?? []; }
        catch { AllPois = []; }
        return Page();
    }

    // Duyệt POI
    public async Task<IActionResult> OnPostApproveAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        var client = _http.CreateClient("API");
        await client.PutAsJsonAsync($"poi/{PoiId}/approve", new { });
        Msg = "Đã duyệt POI thành công.";
        return RedirectToPage();
    }

    // Từ chối POI
    public async Task<IActionResult> OnPostRejectAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        var client = _http.CreateClient("API");
        await client.PutAsJsonAsync($"poi/{PoiId}/reject", new { Reason });
        Msg = "Đã từ chối POI.";
        return RedirectToPage();
    }

    // Rút duyệt → về PENDING
    public async Task<IActionResult> OnPostRevokeAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        var client = _http.CreateClient("API");
        await client.PutAsJsonAsync($"poi/{PoiId}/reject", new { Reason = "Admin rút duyệt" });
        Msg = "Đã rút duyệt POI.";
        return RedirectToPage();
    }

    // Xóa tất cả POI bị từ chối
    public async Task<IActionResult> OnPostDeleteRejectedAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        var client = _http.CreateClient("API");
        var resp = await client.DeleteAsync("poi/rejected");
        if (resp.IsSuccessStatusCode)
        {
            Msg = await resp.Content.ReadAsStringAsync();
        }
        else
        {
            Msg = "Lỗi khi xóa dữ liệu.";
        }
        return RedirectToPage();
    }
}