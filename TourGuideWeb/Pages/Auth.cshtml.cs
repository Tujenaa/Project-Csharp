using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class AuthModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public AuthModel(IHttpClientFactory http) => _http = http;

    public string Error { get; set; } = "";

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("UserId") != null)
            return RedirectToPage("/Index");
        return Page();
    }

    // POST /Auth — đăng nhập
    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        { Error = "Vui lòng nhập đầy đủ thông tin."; return Page(); }

        var client = _http.CreateClient("API");
        try
        {
            var users = await client.GetFromJsonAsync<List<UserDto>>("users");
            var user = users?.FirstOrDefault(u =>
                u.Username.Equals(Username, StringComparison.OrdinalIgnoreCase) &&
                u.PasswordHash == Password);

            if (user == null)
            { Error = "Tên đăng nhập hoặc mật khẩu không đúng."; return Page(); }

            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role ?? "OWNER");
            return RedirectToPage("/Index");
        }
        catch
        { Error = "Không kết nối được API. Vui lòng thử lại."; return Page(); }
    }

    // POST /Auth?handler=Logout — đăng xuất
    public IActionResult OnPostLogout()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Auth");
    }

    private record UserDto(int Id, string Username, string? PasswordHash, string? Role);
}