using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class UsersModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public UsersModel(IHttpClientFactory http) => _http = http;

    public List<UserItem> Users { get; set; } = [];
    [TempData] public string Msg { get; set; } = "";

    // Tạo mới
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string? PasswordHash { get; set; }
    [BindProperty] public string Role { get; set; } = "OWNER";

    // Đổi role
    [BindProperty] public int ChangeId { get; set; }
    [BindProperty] public string NewRole { get; set; } = "";

    // Xóa
    [BindProperty] public int DeleteId { get; set; }

    public async Task OnGetAsync()
    {
        var client = _http.CreateClient("API");
        try { Users = await client.GetFromJsonAsync<List<UserItem>>("users") ?? []; }
        catch { Users = []; }
    }

    // Tạo user mới
    public async Task<IActionResult> OnPostAsync()
    {
        var client = _http.CreateClient("API");
        var payload = new { Username, PasswordHash, Role };
        var res = await client.PostAsJsonAsync("users", payload);
        Msg = res.IsSuccessStatusCode ? $"Đã tạo tài khoản \"{Username}\"." : "Tạo thất bại.";
        return RedirectToPage();
    }

    // Đổi role — admin only
    public async Task<IActionResult> OnPostChangeRoleAsync()
    {
        var client = _http.CreateClient("API");
        var existing = await client.GetFromJsonAsync<UserDto>($"users/{ChangeId}");
        if (existing == null) return RedirectToPage();

        var payload = new
        {
            existing.Username,
            PasswordHash = (string?)null, // Không thay đổi password khi đổi role
            Role = NewRole,
            existing.Name,
            existing.Email,
            existing.Phone
        };
        await client.PutAsJsonAsync($"users/{ChangeId}", payload);
        Msg = $"Đã đổi vai trò thành {NewRole}.";
        return RedirectToPage();
    }

    // Xóa
    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var client = _http.CreateClient("API");
        await client.DeleteAsync($"users/{DeleteId}");
        Msg = "Đã xoá người dùng.";
        return RedirectToPage();
    }

    public record UserItem(int Id, string Username, string Role,
                           string? Name, string? Email, string? Phone);
    private record UserDto(int Id, string Username, string? PasswordHash,
                           string? Role, string? Name, string? Email, string? Phone);
}