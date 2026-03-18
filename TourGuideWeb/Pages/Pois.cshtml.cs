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
            // BaseAddress đã là http://localhost:5266/api/
            // nên chỉ cần "poi", không phải "api/poi"
            Pois = await Api.GetFromJsonAsync<List<POI>>("poi") ?? new List<POI>();
        }
        catch (Exception ex)
        {
            Error = "Không kết nối được API: " + ex.Message;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        var body = new POI
        {
            Id = Id,
            Name = Name,
            Description = Description ?? "",
            Latitude = Latitude,
            Longitude = Longitude,
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
                Error = $"API lỗi {(int)resp.StatusCode}: {resp.ReasonPhrase}";
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