using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class RegisterModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public RegisterModel(IHttpClientFactory http) => _http = http;

    public string Error { get; set; } = "";

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string ConfirmPassword { get; set; } = "";

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("UserId") != null)
            return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        { Error = "Vui lòng nhập đầy đủ thông tin."; return Page(); }

        if (Password.Length < 6)
        { Error = "Mật khẩu tối thiểu 6 ký tự."; return Page(); }

        if (Password != ConfirmPassword)
        { Error = "Mật khẩu xác nhận không khớp."; return Page(); }

        var client = _http.CreateClient("API");
        try
        {
            // Kiểm tra username đã tồn tại chưa
            var users = await client.GetFromJsonAsync<List<UserDto>>("users");
            if (users?.Any(u => u.Username.Equals(Username, StringComparison.OrdinalIgnoreCase)) == true)
            { Error = "Tên đăng nhập đã tồn tại."; return Page(); }

            // Tạo user mới với role OWNER
            var newUser = new { Username, PasswordHash = Password, Role = "OWNER" };
            var resp = await client.PostAsJsonAsync("users", newUser);

            if (!resp.IsSuccessStatusCode)
            { Error = "Đăng ký thất bại. Vui lòng thử lại."; return Page(); }

            var created = await resp.Content.ReadFromJsonAsync<UserDto>();
            if (created == null)
            { Error = "Đăng ký thất bại."; return Page(); }

            // Đăng nhập luôn sau khi đăng ký
            HttpContext.Session.SetString("UserId", created.Id.ToString());
            HttpContext.Session.SetString("Username", created.Username);
            HttpContext.Session.SetString("Role", "OWNER");

            return RedirectToPage("/Index");
        }
        catch
        {
            Error = "Không kết nối được API. Vui lòng thử lại.";
            return Page();
        }
    }

    private record UserDto(int Id, string Username, string? PasswordHash, string? Role);
}