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
    public IList<OwnerItem> Owners { get; private set; } = new List<OwnerItem>(); // cho admin dropdown

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
    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    public bool IsAdmin => Role == "ADMIN";
    private int? MyUserId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;
    public int MyUserIdVal => MyUserId ?? 0;

    public async Task OnGetAsync()
    {
        try
        {
            if (IsAdmin)
            {
                Pois = await Api.GetFromJsonAsync<List<POI>>("poi/all") ?? [];
                Owners = await Api.GetFromJsonAsync<List<OwnerItem>>("users/owners") ?? [];
            }
            else
            {
                Pois = await Api.GetFromJsonAsync<List<POI>>($"poi/owner/{MyUserId}") ?? [];
            }
        }
        catch (Exception ex) { Error = "Không kết nối được API: " + ex.Message; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var latStr = Request.Form["Latitude"].ToString().Replace(',', '.');
        var lngStr = Request.Form["Longitude"].ToString().Replace(',', '.');
        double lat = double.TryParse(latStr, System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out var lv) ? lv : Latitude;
        double lng = double.TryParse(lngStr, System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out var lgv) ? lgv : Longitude;

        // Owner luôn dùng ID của mình, không cho nhập tay
        var ownerId = IsAdmin ? (OwnerId > 0 ? OwnerId : null) : MyUserId;

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
                Msg = "";
            }
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            if (!IsAdmin)
            {
                var existing = await Api.GetFromJsonAsync<POI>($"poi/{DeleteId}");
                if (existing?.OwnerId != MyUserId)
                { Error = "Bạn không có quyền xóa điểm này."; return RedirectToPage(); }
            }
            var resp = await Api.DeleteAsync($"poi/{DeleteId}");
            if (resp.IsSuccessStatusCode) Msg = "Đã xoá điểm thuyết minh thành công.";
            else Error = $"Xoá thất bại: {(int)resp.StatusCode}";
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }
        return RedirectToPage();
    }

    public record OwnerItem(int Id, string Username);
}