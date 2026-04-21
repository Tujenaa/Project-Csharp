using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class TourManagerModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;

    public TourManagerModel(IHttpClientFactory http, IConfiguration config)
    {
        _http = http; _config = config;
    }

    [TempData] public string Msg { get; set; } = "";
    [TempData] public string Error { get; set; } = "";

    public IList<TourItem> Tours { get; private set; } = new List<TourItem>();
    public IList<PoiOption> AllPois { get; private set; } = new List<PoiOption>();
    public string ApiBaseUrl => _config["ApiBaseUrl"] ?? "http://localhost:5266";

    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string Status { get; set; } = "PUBLISHED";
    [BindProperty] public int DeleteId { get; set; }
    [BindProperty] public List<int> PoiIds { get; set; } = new();

    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    public bool IsAdmin => Role == "ADMIN";
    private int? MyUserId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;

    private HttpClient Api()
    {
        var client = _http.CreateClient("API");
        client.DefaultRequestHeaders.Remove("X-Role");
        client.DefaultRequestHeaders.Remove("X-UserId");
        client.DefaultRequestHeaders.Remove("X-Username");
        
        client.DefaultRequestHeaders.Add("X-Role", Role);
        client.DefaultRequestHeaders.Add("X-UserId", HttpContext.Session.GetString("UserId") ?? "0");
        client.DefaultRequestHeaders.Add("X-Username", HttpContext.Session.GetString("Username") ?? "Unknown");
        
        return client;
    }

    public async Task OnGetAsync()
    {
        try
        {
            var client = Api();
            Tours = await client.GetFromJsonAsync<List<TourItem>>("tours") ?? new();
            var pois = await client.GetFromJsonAsync<List<PoiOption>>("poi/all") ?? new();
            AllPois = pois.Where(p => p.Status == "APPROVED").ToList();
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var body = new
        {
            name = Name,
            description = Description ?? "",
            status = Status,
            createdBy = MyUserId ?? 1,
            poiIds = PoiIds
        };

        try
        {
            var client = Api();
            HttpResponseMessage resp;
            if (Id == 0)
            {
                resp = await client.PostAsJsonAsync("tours", body);
                Msg = resp.IsSuccessStatusCode ? $"Đã thêm tour \"{Name}\"." : $"Lỗi: {await resp.Content.ReadAsStringAsync()}";
            }
            else
            {
                resp = await client.PutAsJsonAsync($"tours/{Id}", body);
                Msg = resp.IsSuccessStatusCode ? $"Đã cập nhật tour \"{Name}\"." : $"Lỗi: {await resp.Content.ReadAsStringAsync()}";
            }
            if (!resp.IsSuccessStatusCode) Error = Msg;
        }
        catch (Exception ex) { Error = "Lỗi: " + ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            var resp = await Api().DeleteAsync($"tours/{DeleteId}");
            Msg = resp.IsSuccessStatusCode ? "Đã xóa tour." : "Xóa thất bại.";
        }
        catch (Exception ex) { Error = ex.Message; }
        return RedirectToPage();
    }

    public record TourItem(int Id, string? Name, string? Description, string? Status, List<PoiInTour> POIs);
    public record PoiInTour(int Id, string? Name, string? Address);
    public record PoiOption(int Id, string Name, string? Address, string Status);
}