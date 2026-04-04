using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Net.Http.Json;

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
    [BindProperty] public string? vi { get; set; }
    [BindProperty] public string? en { get; set; }
    [BindProperty] public string? ja { get; set; }
    [BindProperty] public string? zh { get; set; }
    [BindProperty] public int DeleteId { get; set; }

    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    public bool IsAdmin => Role == "ADMIN";
    private int? MyId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;

    private HttpClient Api => _http.CreateClient("API");

    public async Task OnGetAsync()
    {
        try
        {
            var allAudios = await Api.GetFromJsonAsync<List<Audio>>("audio") ?? [];
            if (IsAdmin)
            {
                Audios = allAudios;
                Pois = await Api.GetFromJsonAsync<List<POI>>("poi/all") ?? [];
            }
            else
            {
                Pois = await Api.GetFromJsonAsync<List<POI>>($"poi/owner/{MyId}") ?? [];
                var myPoiIds = Pois.Select(p => p.Id).ToHashSet();
                Audios = allAudios.Where(a => myPoiIds.Contains(a.PoiId)).ToList();
            }
        }
        catch { Audios = []; Pois = []; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var payload = new { PoiId, vi, en, ja, zh };
        HttpResponseMessage res;

        if (Id == 0)
        {
            res = await Api.PostAsJsonAsync("audio", payload);
            Msg = res.IsSuccessStatusCode
                ? "Đã thêm audio thành công."
                : "Thêm thất bại: " + await res.Content.ReadAsStringAsync();
        }
        else
        {
            res = await Api.PutAsJsonAsync($"audio/{Id}", payload);
            Msg = res.IsSuccessStatusCode
                ? "Đã cập nhật audio."
                : "Cập nhật thất bại: " + await res.Content.ReadAsStringAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        await Api.DeleteAsync($"audio/{DeleteId}");
        Msg = "Đã xoá audio.";
        return RedirectToPage();
    }
}