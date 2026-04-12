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
    public List<TourPoiPending> PendingTourPois { get; set; } = [];
    public List<TourPoiPending> RemovePendingTourPois { get; set; } = [];
    public List<TourPoiPending> ApprovedTourPois { get; set; } = [];
    public List<TourPoiPending> RejectedTourPois { get; set; } = [];

    [BindProperty] public int PoiId { get; set; }
    [BindProperty] public string Reason { get; set; } = "";
    [BindProperty] public int TourPOIId { get; set; }
    [BindProperty] public int TourId { get; set; }

    private string Role => HttpContext.Session.GetString("Role") ?? "";
    private bool IsAdmin => Role == "ADMIN";
    private HttpClient Api => _http.CreateClient("API");

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        try
        {
            AllPois = await Api.GetFromJsonAsync<List<POI>>("poi/all") ?? [];
            PendingTourPois = await Api.GetFromJsonAsync<List<TourPoiPending>>("tours/pois/all-pending") ?? [];
            RemovePendingTourPois = await Api.GetFromJsonAsync<List<TourPoiPending>>("tours/pois/all-remove-pending") ?? [];
            ApprovedTourPois = await Api.GetFromJsonAsync<List<TourPoiPending>>("tours/pois/all-approved") ?? [];
            RejectedTourPois = await Api.GetFromJsonAsync<List<TourPoiPending>>("tours/pois/all-rejected") ?? [];
        }
        catch { AllPois = []; PendingTourPois = []; RemovePendingTourPois = []; ApprovedTourPois = []; RejectedTourPois = []; }
        return Page();
    }

    // ── POI handlers ─────────────────────────────────────────────────
    public async Task<IActionResult> OnPostApproveAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        // Load pending TourPOI của POI này trước khi duyệt
        var allPending = await Api.GetFromJsonAsync<List<TourPoiPending>>("tours/pois/all-pending") ?? [];
        var poiTours = allPending.Where(tp => tp.PoiId == PoiId).ToList();
        // Duyệt POI
        await Api.PutAsJsonAsync($"poi/{PoiId}/approve", new { });
        // Tự động duyệt luôn TourPOI pending của POI này
        foreach (var tp in poiTours)
            await Api.PutAsJsonAsync($"tours/{tp.TourId}/pois/{tp.TourPOIId}/approve", new { });
        Msg = poiTours.Any()
            ? $"Đã duyệt POI và tự động thêm vào {poiTours.Count} tour."
            : "Đã duyệt POI thành công.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        await Api.PutAsJsonAsync($"poi/{PoiId}/reject", new { Reason });
        Msg = "Đã từ chối POI.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        await Api.PutAsJsonAsync($"poi/{PoiId}/reject", new { Reason = "Admin rút duyệt" });
        Msg = "Đã rút duyệt POI.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteRejectedAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        var resp = await Api.DeleteAsync("poi/rejected");
        Msg = resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : "Lỗi khi xóa dữ liệu.";
        return RedirectToPage();
    }

    // ── TourPOI handlers ─────────────────────────────────────────────
    public async Task<IActionResult> OnPostApproveTourPoiAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        var resp = await Api.PutAsJsonAsync($"tours/{TourId}/pois/{TourPOIId}/approve", new { });
        Msg = resp.IsSuccessStatusCode ? "Đã duyệt yêu cầu tham gia tour." : "Duyệt thất bại.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectTourPoiAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        var resp = await Api.PutAsJsonAsync($"tours/{TourId}/pois/{TourPOIId}/reject", new { });
        Msg = resp.IsSuccessStatusCode ? "Đã từ chối yêu cầu tham gia tour." : "Từ chối thất bại.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteRejectedTourPoisAsync()
    {
        if (!IsAdmin) return RedirectToPage("/Index");
        var resp = await Api.DeleteAsync("tours/pois/rejected");
        Msg = resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : "Lỗi khi xóa dữ liệu.";
        return RedirectToPage();
    }

    public record TourPoiPending(int TourPOIId, int TourId, string? TourName,
        int PoiId, string? PoiName, string? Address, int OrderIndex, string Status);
}