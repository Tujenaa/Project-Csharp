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

    public async Task<IActionResult> OnPostAsync()
    {
        // if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        // { Error = "Vui lòng nhập đầy đủ thông tin."; return Page(); }

        var client = _http.CreateClient("API");
        try
        {
            // Gọi endpoint login chính thức của API
            var loginPayload = new { Username, Password };
            var resp = await client.PostAsJsonAsync("users/login", loginPayload);
            
            if (!resp.IsSuccessStatusCode)
            {
                Error = "Tên đăng nhập hoặc mật khẩu không đúng.";
                return Page();
            }

            var user = await resp.Content.ReadFromJsonAsync<UserDto>();
            if (user == null)
            {
                Error = "Lỗi hệ thống khi đăng nhập.";
                return Page();
            }

            // Chặn CUSTOMER — web chỉ dành cho ADMIN và OWNER
            if (user.Role == "CUSTOMER")
            { 
                Error = "Tài khoản khách hàng không được phép đăng nhập web quản lý. Vui lòng dùng ứng dụng mobile."; 
                return Page(); 
            }

            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role ?? "OWNER");
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        { 
            Error = "Không kết nối được API. Vui lòng thử lại."; 
            return Page(); 
        }
    }

    // Đăng xuất
    public IActionResult OnPostLogout()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Auth");
    }

    private record UserDto(int Id, string Username, string? PasswordHash, string? Role);
}