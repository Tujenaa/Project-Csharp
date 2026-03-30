using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class UserDetailModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public UserDetailModel(IHttpClientFactory http) => _http = http;

    public UserInfo? User { get; set; }
    public List<POI> OwnerPois { get; set; } = [];

    private string Role => HttpContext.Session.GetString("Role") ?? "";
    private bool IsAdmin => Role == "ADMIN";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        // Chỉ Admin được xem
        if (!IsAdmin) return RedirectToPage("/Index");

        var client = _http.CreateClient("API");
        try
        {
            User = await client.GetFromJsonAsync<UserInfo>($"users/{id}");
            if (User?.Role == "OWNER")
                OwnerPois = await client.GetFromJsonAsync<List<POI>>($"poi/owner/{id}") ?? [];
        }
        catch { User = null; }

        return Page();
    }

    public record UserInfo(int Id, string Username, string? PasswordHash,
                           string? Role, string? Name, string? Email, string? Phone);
}