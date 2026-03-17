using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class UsersModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public UsersModel(IHttpClientFactory http) => _http = http;

    [TempData] public string Msg { get; set; } = "";
    [TempData] public string Error { get; set; } = "";

    public IList<User> Users { get; private set; } = new List<User>();

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string PasswordHash { get; set; } = "";
    [BindProperty] public string Role { get; set; } = "OWNER";
    [BindProperty] public int DeleteId { get; set; }

    private HttpClient Api => _http.CreateClient("API");

    public async Task OnGetAsync()
    {
        try
        {
            Users = await Api.GetFromJsonAsync<List<User>>("users") ?? new();
        }
        catch (Exception ex)
        {
            Error = "Không kết nối được API: " + ex.Message;
        }
    }

    // POST: tạo user mới
    public async Task<IActionResult> OnPostAsync()
    {
        var body = new { Username, PasswordHash, Role };
        try
        {
            var resp = await Api.PostAsJsonAsync("users", body);
            if (resp.IsSuccessStatusCode)
                Msg = $"Đã tạo người dùng \"{Username}\" thành công.";
            else
                Error = $"API lỗi {(int)resp.StatusCode}: {resp.ReasonPhrase}";
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }

        return RedirectToPage();
    }

    // POST: xoá user
    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            var resp = await Api.DeleteAsync($"users/{DeleteId}");
            if (resp.IsSuccessStatusCode)
                Msg = "Đã xoá người dùng thành công.";
            else
                Error = $"Xoá thất bại: {(int)resp.StatusCode} {resp.ReasonPhrase}";
        }
        catch (Exception ex) { Error = "Lỗi kết nối API: " + ex.Message; }

        return RedirectToPage();
    }
}