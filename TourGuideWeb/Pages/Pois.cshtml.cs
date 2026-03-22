using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class PoisModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public PoisModel(IHttpClientFactory http) => _http = http;

    [TempData] public string Msg { get; set; } = "";
    [TempData] public string Error { get; set; } = "";

    public IList<POI> Pois { get; private set; } = new List<POI>();

    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? Address { get; set; }
    [BindProperty] public double Latitude { get; set; }
    [BindProperty] public double Longitude { get; set; }
    [BindProperty] public int Radius { get; set; } = 80;
    [BindProperty] public int? OwnerId { get; set; }
    [BindProperty] public int DeleteId { get; set; }

    private HttpClient Api => _http.CreateClient("API");

    // Lấy role và userId từ session
    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    private bool IsAdmin => Role == "ADMIN";
    private int? MyUserId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;

    public async Task OnGetAsync()
    {
        try
        {
            if (IsAdmin)
            {
                // Admin thấy tất cả POI
                Pois = await Api.GetFromJsonAsync<List<POI>>("poi") ?? new List<POI>();
            }
            else
            {
                // Owner chỉ thấy POI của mình
                Pois = await Api.GetFromJsonAsync<List<POI>>($"poi/owner/{MyUserId}") ?? new List<POI>();
            }
        }
        catch (Exception ex)
        {
            Error = "Không kết nối được API: " + ex.Message;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var latStr = Request.Form["Latitude"].ToString().Replace(',', '.');
        var lngStr = Request.Form["Longitude"].ToString().Replace(',', '.');
        double lat = double.TryParse(latStr, System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out var lv) ? lv : Latitude;
        double lng = double.TryParse(lngStr, System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out var lgv) ? lgv : Longitude;

        // Owner chỉ được thêm POI với OwnerId = chính mình
        var ownerId = IsAdmin ? OwnerId : MyUserId;

        var body = new POI
        {
            Id = Id,
            Name = Name,
            Description = Description ?? "",
            Address = Address ?? "",
            Latitude = lat,
            Longitude = lng,
            Radius = Radius,
            OwnerId = ownerId
        };

        try
        {
            HttpResponseMessage resp;
            if (Id == 0)
            {
                resp = await Api.PostAsJsonAsync("poi", body);
                Msg = $"Đã thêm điểm \"{Name}\" thành công.";
            }
            else
            {
                // Owner chỉ được sửa POI của mình
                if (!IsAdmin)
                {
                    var existing = await Api.GetFromJsonAsync<POI>($"poi/{Id}");
                    if (existing?.OwnerId != MyUserId)
                    { Error = "Bạn không có quyền sửa điểm này."; return RedirectToPage(); }
                }
                resp = await Api.PutAsJsonAsync($"poi/{Id}", body);
                Msg = $"Đã cập nhật \"{Name}\" thành công.";
            }

            if (!resp.IsSuccessStatusCode)
            {
                var detail = await resp.Content.ReadAsStringAsync();
                Error = $"API lỗi {(int)resp.StatusCode}: {detail}";
            }
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            // Owner chỉ được xóa POI của mình
            if (!IsAdmin)
            {
                var existing = await Api.GetFromJsonAsync<POI>($"poi/{DeleteId}");
                if (existing?.OwnerId != MyUserId)
                { Error = "Bạn không có quyền xóa điểm này."; return RedirectToPage(); }
            }

            var resp = await Api.DeleteAsync($"poi/{DeleteId}");
            if (resp.IsSuccessStatusCode)
                Msg = "Đã xoá điểm thuyết minh thành công.";
            else
                Error = $"Xoá thất bại: {(int)resp.StatusCode} {resp.ReasonPhrase}";
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }

        return RedirectToPage();
    }
}