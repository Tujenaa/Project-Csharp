using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class AudioManagerModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    public AudioManagerModel(IHttpClientFactory http, IConfiguration config)
    {
        _http = http; _config = config;
    }

    [TempData] public string Msg { get; set; } = "";
    [TempData] public string Error { get; set; } = "";

    public List<AudioItem> Audios { get; set; } = new();
    public List<PoiOption> Pois { get; set; } = new();
    public List<PoiOption> PoisForAdd { get; set; } = new(); // POI còn có thể thêm audio
    public List<LangOption> Languages { get; set; } = new();
    public bool AllCovered { get; set; } = false; // tất cả POI đã có đủ audio mọi ngôn ngữ
    public string ApiBaseUrl => _config["ApiBaseUrl"] ?? "http://localhost:5266";

    [BindProperty] public int Id { get; set; }
    [BindProperty] public int PoiId { get; set; }
    [BindProperty] public int LanguageId { get; set; }
    [BindProperty] public string Script { get; set; } = "";
    [BindProperty] public int DeleteId { get; set; }

    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    public bool IsAdmin => Role == "ADMIN";
    private int? MyId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;
    private HttpClient Api()
    {
        var c = _http.CreateClient("API");
        c.DefaultRequestHeaders.Remove("X-Role");
        c.DefaultRequestHeaders.Remove("X-UserId");
        c.DefaultRequestHeaders.Remove("X-Username");
        
        c.DefaultRequestHeaders.Add("X-Role", Role);
        c.DefaultRequestHeaders.Add("X-UserId", HttpContext.Session.GetString("UserId") ?? "0");
        c.DefaultRequestHeaders.Add("X-Username", HttpContext.Session.GetString("Username") ?? "Unknown");
        
        return c;
    }

    public async Task OnGetAsync()
    {
        try
        {
            var api = Api();
            Languages = await api.GetFromJsonAsync<List<LangOption>>("languages/active") ?? new();
            var allAudios = await api.GetFromJsonAsync<List<AudioItem>>("audio") ?? new();

            if (IsAdmin)
            {
                Audios = allAudios;
                Pois = (await api.GetFromJsonAsync<List<PoiOption>>("poi/all") ?? new())
                    .Where(p => p.Status == "APPROVED").ToList();
            }
            else
            {
                var myPois = (await api.GetFromJsonAsync<List<PoiOption>>($"poi/owner/{MyId}") ?? new())
                    .Where(p => p.Status == "APPROVED").ToList();
                var myPoiIds = myPois.Select(p => p.Id).ToHashSet();
                Audios = allAudios.Where(a => myPoiIds.Contains(a.PoiId)).ToList();
                Pois = myPois;
            }

            // Tính POI còn có thể thêm audio (còn ngôn ngữ chưa có)
            var langCount = Languages.Count;
            PoisForAdd = Pois.Where(p => {
                var audioCountForPoi = Audios.Count(a => a.PoiId == p.Id);
                return audioCountForPoi < langCount;
            }).ToList();

            AllCovered = !PoisForAdd.Any();
        }
        catch (Exception ex) { Error = "Lỗi kết nối: " + ex.Message; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var api = Api();
            HttpResponseMessage res;
            if (Id == 0)
            {
                var body = new { PoiId, LanguageId, Script };
                res = await api.PostAsJsonAsync("audio", body);
                Msg = res.IsSuccessStatusCode ? "Đã thêm audio thành công." : await res.Content.ReadAsStringAsync();
            }
            else
            {
                var body = new { Script };
                res = await api.PutAsJsonAsync($"audio/{Id}", body);
                Msg = res.IsSuccessStatusCode ? "Đã cập nhật audio." : await res.Content.ReadAsStringAsync();
            }
            if (!res.IsSuccessStatusCode) { Error = Msg; Msg = ""; }
        }
        catch (Exception ex) { Error = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            var res = await Api().DeleteAsync($"audio/{DeleteId}");
            Msg = res.IsSuccessStatusCode ? "Đã xóa audio." : "Xóa thất bại.";
        }
        catch (Exception ex) { Error = ex.Message; }
        return RedirectToPage();
    }

    public record AudioItem(int Id, int PoiId, string? PoiName, int LanguageId, string? LanguageCode, string? LanguageName, string Script);
    public record PoiOption(int Id, string Name, string? Address, string Status);
    public record LangOption(int Id, string Code, string Name, bool IsActive, int OrderIndex);
}