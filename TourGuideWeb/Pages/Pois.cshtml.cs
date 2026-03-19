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
    [BindProperty] public double Latitude { get; set; }
    [BindProperty] public double Longitude { get; set; }
    [BindProperty] public int Radius { get; set; } = 80;
    [BindProperty] public int? OwnerId { get; set; }
    [BindProperty] public int DeleteId { get; set; }

    private HttpClient Api => _http.CreateClient("API");

    public async Task OnGetAsync()
    {
        try
        {
            Pois = await Api.GetFromJsonAsync<List<POI>>("poi") ?? new List<POI>();
        }
        catch (Exception ex)
        {
            Error = "Không kết nối được API: " + ex.Message;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Fix dấu thập phân — đọc thẳng từ form, parse bằng InvariantCulture
        var latStr = Request.Form["Latitude"].ToString().Replace(',', '.');
        var lngStr = Request.Form["Longitude"].ToString().Replace(',', '.');
        double lat = double.TryParse(latStr, System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out var lv) ? lv : Latitude;
        double lng = double.TryParse(lngStr, System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out var lgv) ? lgv : Longitude;

        var body = new POI
        {
            Id = Id,
            Name = Name,
            Description = Description ?? "",
            Latitude = lat,
            Longitude = lng,
            Radius = Radius,
            OwnerId = OwnerId
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
                resp = await Api.PutAsJsonAsync($"poi/{Id}", body);
                Msg = $"Đã cập nhật \"{Name}\" thành công.";
            }

            if (!resp.IsSuccessStatusCode)
            {
                var detail = await resp.Content.ReadAsStringAsync();
                Error = $"API lỗi {(int)resp.StatusCode}: {detail}";
            }
        }
        catch (Exception ex)
        {
            Error = "Lỗi kết nối API: " + ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            var resp = await Api.DeleteAsync($"poi/{DeleteId}");
            if (resp.IsSuccessStatusCode)
                Msg = "Đã xoá điểm thuyết minh thành công.";
            else
                Error = $"Xoá thất bại: {(int)resp.StatusCode} {resp.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            Error = "Lỗi kết nối API: " + ex.Message;
        }

        return RedirectToPage();
    }
}