using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class ProfileModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public ProfileModel(IHttpClientFactory http) => _http = http;

    [TempData] public string Msg { get; set; } = "";
    [TempData] public string Error { get; set; } = "";

    public string CurrentUsername { get; set; } = "";
    public UserInfo? Info { get; set; }

    private int MyId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : 0;

    [BindProperty] public string NewUsername { get; set; } = "";
    [BindProperty] public string? NewPassword { get; set; }
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? Phone { get; set; }

    public async Task OnGetAsync()
    {
        CurrentUsername = HttpContext.Session.GetString("Username") ?? "";
        var client = _http.CreateClient("API");
        try { Info = await client.GetFromJsonAsync<UserInfo>($"users/{MyId}"); }
        catch { Info = null; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = _http.CreateClient("API");
        var existing = await client.GetFromJsonAsync<UserInfo>($"users/{MyId}");
        if (existing == null) { Error = "Không tải được thông tin."; return RedirectToPage(); }

        // ✅ Chỉ gửi password plaintext nếu người dùng nhập mới, nếu không gửi null để API giữ nguyên pass cũ
        string? passwordHash = string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword;

        var payload = new
        {
            Username = NewUsername,
            PasswordHash = passwordHash,
            Role = existing.Role,
            Name,
            Email,
            Phone
        };

        var res = await client.PutAsJsonAsync($"users/{MyId}", payload);
        if (res.IsSuccessStatusCode)
        {
            HttpContext.Session.SetString("Username", NewUsername);
            Msg = "Đã cập nhật thông tin thành công.";
        }
        else Error = "Cập nhật thất bại.";

        return RedirectToPage();
    }

    public record UserInfo(int Id, string Username, string? PasswordHash,
                           string? Role, string? Name, string? Email, string? Phone);
}