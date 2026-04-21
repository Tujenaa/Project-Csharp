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
    [BindProperty] public string? Password { get; set; }
    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("UserId") != null)
            return RedirectToPage("/Index");
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var client = _http.CreateClient("API");
        try
        {
            var loginPayload = new { Username, Password };
            var resp = await client.PostAsJsonAsync("users/login", loginPayload);

            // 404 → tài khoản không tồn tại
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Error = "Tài khoản không tồn tại.";
                return Page();
            }

            // 403 → tài khoản bị vô hiệu hoá
            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Error = "Tài khoản của bạn đã bị vô hiệu hoá. Vui lòng liên hệ quản trị viên.";
                return Page();
            }

            // 401 → sai mật khẩu
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Error = "Mật khẩu không đúng.";
                return Page();
            }

            if (!resp.IsSuccessStatusCode)
            {
                Error = "Đăng nhập thất bại. Vui lòng thử lại.";
                return Page();
            }

            var user = await resp.Content.ReadFromJsonAsync<UserDto>();
            if (user == null)
            {
                Error = "Lỗi hệ thống khi đăng nhập.";
                return Page();
            }

            // Chặn CUSTOMER
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
        catch
        {
            Error = "Không kết nối được API. Vui lòng thử lại.";
            return Page();
        }
    }
    public async Task<IActionResult> OnPostLogout()
    {
        var client = _http.CreateClient("API");
        var userId = HttpContext.Session.GetString("UserId");
        var username = HttpContext.Session.GetString("Username");
        var role = HttpContext.Session.GetString("Role");

        if (!string.IsNullOrEmpty(username))
        {
            try
            {
                var activity = new
                {
                    UserId = userId != null ? int.Parse(userId) : (int?)null,
                    Username = username,
                    Role = role,
                    ActivityType = "LOGOUT",
                    Details = $"Người dùng {username} đã đăng xuất từ trình duyệt.",
                    DeviceId = "WEB_ADMIN",
                    Timestamp = DateTime.Now
                };
                await client.PostAsJsonAsync("users/logout", activity);
            }
            catch { }
        }

        HttpContext.Session.Clear();
        return RedirectToPage("/Auth");
    }
    private record UserDto(int Id, string Username, string? PasswordHash, string? Role, bool IsActive = true);
}