using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class LanguageManagerModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public LanguageManagerModel(IHttpClientFactory http) => _http = http;

    [TempData] public string Msg { get; set; } = "";
    [TempData] public string Error { get; set; } = "";

    public List<LangItem> Languages { get; set; } = new();

    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Code { get; set; } = "";
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public bool IsActive { get; set; } = true;
    [BindProperty] public int OrderIndex { get; set; }
    [BindProperty] public int DeleteId { get; set; }

    private HttpClient Api => _http.CreateClient("API");

    public async Task OnGetAsync()
    {
        try { Languages = await Api.GetFromJsonAsync<List<LangItem>>("languages") ?? new(); }
        catch (Exception ex) { Error = "Lỗi: " + ex.Message; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var body = new { Code = Code.Trim().ToLower(), Name, IsActive, OrderIndex };
        try
        {
            HttpResponseMessage res;
            if (Id == 0)
            {
                res = await Api.PostAsJsonAsync("languages", body);
                Msg = res.IsSuccessStatusCode ? $"Đã thêm ngôn ngữ \"{Name}\"." : await res.Content.ReadAsStringAsync();
            }
            else
            {
                res = await Api.PutAsJsonAsync($"languages/{Id}", body);
                Msg = res.IsSuccessStatusCode ? $"Đã cập nhật \"{Name}\"." : await res.Content.ReadAsStringAsync();
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
            var res = await Api.DeleteAsync($"languages/{DeleteId}");
            if (res.IsSuccessStatusCode) Msg = "Đã xóa ngôn ngữ.";
            else Error = await res.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        return RedirectToPage();
    }

    public record LangItem(int Id, string Code, string Name, bool IsActive, int OrderIndex);
}