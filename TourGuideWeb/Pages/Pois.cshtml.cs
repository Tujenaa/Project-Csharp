using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class PoisModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public PoisModel(IHttpClientFactory http) => _http = http;

<<<<<<< HEAD
    [TempData] public string Msg { get; set; } = "";
    [TempData] public string Error { get; set; } = "";
    public IList<POI> Pois { get; private set; } = new List<POI>();

=======
    // ── View data ─────────────────────────────────────────────
    [TempData] public string Msg { get; set; } = "";
    [TempData] public string Error { get; set; } = "";

    public IList<POI> Pois { get; private set; } = new List<POI>();

    // ── Bound properties cho form (khớp đúng cột DB) ─────────
>>>>>>> a774dae7a97873962cbcdc147ce51e50f1dd3c0e
    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public double Latitude { get; set; }
    [BindProperty] public double Longitude { get; set; }
    [BindProperty] public int Radius { get; set; } = 80;
    [BindProperty] public int? OwnerId { get; set; }
    [BindProperty] public int DeleteId { get; set; }

    private HttpClient Api => _http.CreateClient("API");

<<<<<<< HEAD
=======
    // ── GET ───────────────────────────────────────────────────
>>>>>>> a774dae7a97873962cbcdc147ce51e50f1dd3c0e
    public async Task OnGetAsync()
    {
        try
        {
<<<<<<< HEAD
            // Bỏ "api/" thừa — BaseAddress đã là https://localhost:7134/api/
            Pois = await Api.GetFromJsonAsync<List<POI>>("poi") ?? new List<POI>();
        }
        catch (Exception ex)
        {
            Error = "Không kết nối được API. Kiểm tra TourGuide.API đã chạy chưa. (" + ex.Message + ")";
        }
    }

=======
            Pois = await Api.GetFromJsonAsync<List<POI>>("api/poi") ?? new List<POI>();
        }
        catch
        {
            Error = "Không kết nối được API. Kiểm tra lại cấu hình ApiUrl.";
        }
    }

    // ── POST: Tạo mới hoặc cập nhật ──────────────────────────
>>>>>>> a774dae7a97873962cbcdc147ce51e50f1dd3c0e
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
<<<<<<< HEAD
                resp = await Api.PostAsJsonAsync("poi", body);
=======
                resp = await Api.PostAsJsonAsync("api/poi", body);
>>>>>>> a774dae7a97873962cbcdc147ce51e50f1dd3c0e
                Msg = $"Đã thêm điểm \"{Name}\" thành công.";
            }
            else
            {
<<<<<<< HEAD
                resp = await Api.PutAsJsonAsync($"poi/{Id}", body);
=======
                resp = await Api.PutAsJsonAsync($"api/poi/{Id}", body);
>>>>>>> a774dae7a97873962cbcdc147ce51e50f1dd3c0e
                Msg = $"Đã cập nhật \"{Name}\" thành công.";
            }

            if (!resp.IsSuccessStatusCode)
                Error = $"API trả về lỗi {(int)resp.StatusCode}: {resp.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            Error = "Lỗi kết nối API: " + ex.Message;
        }

        return RedirectToPage();
    }

<<<<<<< HEAD
=======
    // ── POST: Xoá ─────────────────────────────────────────────
>>>>>>> a774dae7a97873962cbcdc147ce51e50f1dd3c0e
    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
<<<<<<< HEAD
            var resp = await Api.DeleteAsync($"poi/{DeleteId}");
=======
            var resp = await Api.DeleteAsync($"api/poi/{DeleteId}");
>>>>>>> a774dae7a97873962cbcdc147ce51e50f1dd3c0e
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
<<<<<<< HEAD
}
=======
}
>>>>>>> a774dae7a97873962cbcdc147ce51e50f1dd3c0e
