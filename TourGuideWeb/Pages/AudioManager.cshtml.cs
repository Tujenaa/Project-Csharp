using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;

namespace GPSGuide.Web.Pages;

public class AudioManagerModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public AudioManagerModel(IHttpClientFactory http) => _http = http;

    public List<Audio> Audios { get; set; } = [];
    public List<POI> Pois { get; set; } = [];

    [TempData] public string Msg { get; set; } = "";

    [BindProperty] public int Id { get; set; }
    [BindProperty] public int PoiId { get; set; }
    [BindProperty] public string Language { get; set; } = "vi";
    [BindProperty] public string? Script { get; set; }
    [BindProperty] public int DeleteId { get; set; }

    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    private bool IsAdmin => Role == "ADMIN";
    private int? MyId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;

    public async Task OnGetAsync()
    {
        var client = _http.CreateClient("API");
        try
        {
            var allAudios = await client.GetFromJsonAsync<List<Audio>>("audio") ?? [];

            if (IsAdmin)
            {
                Audios = allAudios;
                Pois = await client.GetFromJsonAsync<List<POI>>("poi") ?? [];
            }
            else
            {
                // Owner chỉ thấy audio của POI mình
                Pois = await client.GetFromJsonAsync<List<POI>>($"poi/owner/{MyId}") ?? [];
                var myPoiIds = Pois.Select(p => p.Id).ToHashSet();
                Audios = allAudios.Where(a => myPoiIds.Contains(a.PoiId)).ToList();
            }
        }
        catch
        {
            Audios = []; Pois = [];
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = _http.CreateClient("API");
        var payload = new { PoiId, Language, Script };
        HttpResponseMessage res;
        if (Id == 0)
        {
            res = await client.PostAsJsonAsync("audio", payload);
            Msg = res.IsSuccessStatusCode ? "Đã thêm audio thành công." : "Thêm thất bại.";
        }
        else
        {
            res = await client.PutAsJsonAsync($"audio/{Id}", payload);
            Msg = res.IsSuccessStatusCode ? "Đã cập nhật audio." : "Cập nhật thất bại.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var client = _http.CreateClient("API");
        await client.DeleteAsync($"audio/{DeleteId}");
        Msg = "Đã xoá audio.";
        return RedirectToPage();
    }
}