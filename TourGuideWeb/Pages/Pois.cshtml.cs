using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Net.Http.Json;
using System.Globalization;

namespace GPSGuide.Web.Pages;

public class PoisModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    public PoisModel(IHttpClientFactory http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    [TempData] public string Msg { get; set; } = "";
    [TempData] public string Error { get; set; } = "";

    public IList<POI> Pois { get; private set; } = new List<POI>();
    public IList<OwnerItem> Owners { get; private set; } = new List<OwnerItem>();
    public IList<TourItem> AllTours { get; private set; } = new List<TourItem>();
    public string ApiBaseUrl => _config["ApiBaseUrl"] ?? "http://localhost:5266";

    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? Address { get; set; }
    [BindProperty] public double Latitude { get; set; }
    [BindProperty] public double Longitude { get; set; }
    [BindProperty] public int Radius { get; set; } = 5;
    [BindProperty] public int? OwnerId { get; set; }
    [BindProperty] public int DeleteId { get; set; }
    [BindProperty] public int ImageIdToDelete { get; set; }
    [BindProperty] public List<IFormFile> ImageFiles { get; set; } = [];
    [BindProperty] public List<int> TourIds { get; set; } = [];

    // Nhận danh sách imageId cần xóa từ hidden input (phân cách bằng dấu phẩy)
    [BindProperty] public string? DeletedImageIds { get; set; }

    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    public bool IsAdmin => Role == "ADMIN";
    private int? MyUserId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;
    public int MyUserIdVal => MyUserId ?? 0;

    private HttpClient ApiWithRole()
    {
        var client = _http.CreateClient("API");
        client.DefaultRequestHeaders.Remove("X-Role");
        client.DefaultRequestHeaders.Remove("X-UserId");
        client.DefaultRequestHeaders.Remove("X-Username");

        client.DefaultRequestHeaders.Add("X-Role", Role);
        client.DefaultRequestHeaders.Add("X-UserId", MyUserId?.ToString() ?? "0");
        client.DefaultRequestHeaders.Add("X-Username", HttpContext.Session.GetString("Username") ?? "Unknown");

        return client;
    }

    public async Task OnGetAsync()
    {
        try
        {
            var client = ApiWithRole();
            if (IsAdmin)
            {
                Pois = await client.GetFromJsonAsync<List<POI>>("poi/all") ?? [];
                Owners = await client.GetFromJsonAsync<List<OwnerItem>>("users/owners") ?? [];
                AllTours = await client.GetFromJsonAsync<List<TourItem>>("tours") ?? [];
            }
            else
            {
                Pois = await client.GetFromJsonAsync<List<POI>>($"poi/owner/{MyUserId}") ?? [];
                AllTours = await client.GetFromJsonAsync<List<TourItem>>("tours/published") ?? [];
            }
            foreach (var p in Pois)
                p.Images = await client.GetFromJsonAsync<List<POIImage>>($"poi/{p.Id}/images") ?? [];
        }
        catch (Exception ex) { Error = "Không kết nối được API: " + ex.Message; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var latStr = Request.Form["Latitude"].ToString().Trim().Replace(',', '.');
        var lngStr = Request.Form["Longitude"].ToString().Trim().Replace(',', '.');
        double lat = double.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lv) ? lv : Latitude;
        double lng = double.TryParse(lngStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lgv) ? lgv : Longitude;

        var ownerId = IsAdmin ? (OwnerId > 0 ? OwnerId : null) : MyUserId;
        var status = IsAdmin ? "APPROVED" : "PENDING";

        var body = new POI
        {
            Id = Id,
            Name = Name,
            Description = Description ?? "",
            Address = Address ?? "",
            Latitude = lat,
            Longitude = lng,
            Radius = Radius,
            OwnerId = ownerId,
            Status = status
        };

        try
        {
            var client = ApiWithRole();
            HttpResponseMessage resp;
            int savedId;

            if (Id == 0)
            {
                // ── TẠO MỚI ──
                resp = await client.PostAsJsonAsync("poi", body);
                if (!resp.IsSuccessStatusCode)
                { Error = $"API lỗi {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}"; return RedirectToPage(); }

                if (IsAdmin)
                {
                    // Admin → API trả về POI thật có Id
                    var created = await resp.Content.ReadFromJsonAsync<POI>();
                    savedId = created?.Id ?? 0;
                    Msg = $"Đã thêm \"{Name}\" thành công.";
                }
                else
                {
                    // Owner → API tạo ApprovalRequest, không có POI Id thật
                    // Chuyển sang Id âm để upload ảnh vào Request Content
                    var result = await resp.Content.ReadFromJsonAsync<ReqResult>();
                    if (result != null && result.requestId > 0)
                    {
                        savedId = -result.requestId;
                        Msg = $"Đã gửi \"{Name}\" kèm {ImageFiles?.Count ?? 0} ảnh — đang chờ Admin phê duyệt.";
                    }
                    else
                    {
                        Msg = $"Đã gửi \"{Name}\" — đang chờ Admin phê duyệt.";
                        return RedirectToPage();
                    }
                }
            }
            else
            {
                // ── CẬP NHẬT ──
                if (!IsAdmin)
                {
                    if (Id > 0)
                    {
                        try
                        {
                            var existing = await client.GetFromJsonAsync<POI>($"poi/{Id}");
                            if (existing != null && existing.OwnerId.HasValue && existing.OwnerId > 0 && existing.OwnerId != MyUserId)
                            { Error = "Bạn không có quyền sửa điểm này."; return RedirectToPage(); }
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        { /* Ignore 404 */ }
                    }
                }
                resp = await client.PutAsJsonAsync($"poi/{Id}", body);
                savedId = Id;
                if (!resp.IsSuccessStatusCode)
                { Error = $"API lỗi {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}"; return RedirectToPage(); }
                Msg = IsAdmin ? $"Đã cập nhật \"{Name}\" thành công." : $"Đã gửi cập nhật \"{Name}\" — đang chờ Admin phê duyệt.";
            }

            // ── XÓA ẢNH ĐÃ ĐÁNH DẤU ──
            if (!string.IsNullOrWhiteSpace(DeletedImageIds))
            {
                var ids = DeletedImageIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var imgIdStr in ids)
                {
                    if (int.TryParse(imgIdStr.Trim(), out var imgId))
                    {
                        var delResp = await client.DeleteAsync($"poi/image/{imgId}");
                        if (!delResp.IsSuccessStatusCode)
                            Msg += $" (Lỗi xóa ảnh #{imgId})";
                    }
                }
            }

            // ── GÁN TOUR ──
            if (savedId != 0 && TourIds.Any())
            {
                int tourOk = 0, tourPending = 0;
                foreach (var tourId in TourIds)
                {
                    var res = await client.PostAsJsonAsync($"tours/{tourId}/pois", new { PoiId = savedId });
                    if (res.IsSuccessStatusCode)
                    {
                        var content = await res.Content.ReadAsStringAsync();
                        if (content.Contains("PENDING")) tourPending++;
                        else tourOk++;
                    }
                }
                if (tourPending > 0) Msg += $" Đã xin vào {tourPending} tour.";
                if (tourOk > 0) Msg += $" Đã thêm vào {tourOk} tour.";
            }

            // ── UPLOAD ẢNH MỚI ──
            if (savedId != 0 && ImageFiles != null)
            {
                foreach (var file in ImageFiles.Where(f => f.Length > 0).Take(5))
                {
                    using var form = new MultipartFormDataContent();
                    var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    ms.Position = 0;
                    form.Add(new StreamContent(ms), "file", file.FileName);
                    var imgResp = await client.PostAsync($"poi/{savedId}/image", form);
                    if (!imgResp.IsSuccessStatusCode) Msg += $" (Lỗi upload '{file.FileName}')";
                    await Task.Delay(50);
                }
            }
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            var client = ApiWithRole();

            // POI ảo từ ApprovalRequest có Id âm → hủy ApprovalRequest
            if (DeleteId < 0)
            {
                var requestId = -DeleteId; // đảo lại thành Id dương
                var resp = await client.DeleteAsync($"approvalrequests/{requestId}");
                if (!resp.IsSuccessStatusCode)
                    Error = $"Không thể hủy yêu cầu chờ duyệt (#{requestId}).";
                else
                    Msg = "Đã hủy yêu cầu tạo điểm thuyết minh thành công.";
                return RedirectToPage();
            }

            // POI thật
            if (!IsAdmin)
            {
                var existing = await client.GetFromJsonAsync<POI>($"poi/{DeleteId}");
                if (existing != null && existing.OwnerId.HasValue && existing.OwnerId > 0 && existing.OwnerId != MyUserId)
                { Error = "Bạn không có quyền xóa điểm này."; return RedirectToPage(); }
            }
            var delResp = await client.DeleteAsync($"poi/{DeleteId}");
            if (!delResp.IsSuccessStatusCode)
                Error = $"Không thể xóa điểm (API {(int)delResp.StatusCode}): {await delResp.Content.ReadAsStringAsync()}";
            else
                Msg = "Đã xoá điểm thuyết minh thành công.";
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteImageAsync()
    {
        try
        {
            var resp = await ApiWithRole().DeleteAsync($"poi/image/{ImageIdToDelete}");
            if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode);
            return new JsonResult(new { success = true });
        }
        catch (Exception) { return StatusCode(500); }
    }

    public record OwnerItem(int Id, string Username);
    public record TourItem(int Id, string? Name, string? Status);
    private record ReqResult(string message, int requestId);
}