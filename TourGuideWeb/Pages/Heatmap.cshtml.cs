using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Text.Json;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages;

public class HeatmapModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;

    public HeatmapModel(IHttpClientFactory http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public string HeatDataJson { get; set; } = "[]";
    public string CurrentFilter { get; set; } = "all";
    public string ApiBaseUrl
    {
        get
        {
            var url = _config["ApiUrl"] ?? "http://localhost:5266";
            return url.TrimEnd('/');
        }
    }

    private string Role => HttpContext.Session.GetString("Role") ?? "OWNER";
    public bool IsAdmin => Role == "ADMIN";
    private int? MyId => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : null;

    public async Task OnGetAsync()
    {
        var client = _http.CreateClient("API");

        try
        {
            // Lấy danh sách thiết bị đang online từ API
            // Sử dụng endpoint proxy đã có trong Program.cs hoặc gọi trực tiếp nếu nội bộ
            // Ở đây Model gọi trực tiếp tới API server
            var devices = await client.GetFromJsonAsync<List<ActiveDevice>>("device/active") ?? new();

            // Nếu muốn lọc theo Owner (giả sử chỉ hiện thiết bị nếu người dùng là Admin hoặc có liên quan)
            // Hiện tại API trả về tất cả, ta sẽ hiển thị tất cả mật độ thiết bị
            
            if (devices.Any())
            {
                // Chuyển sang JSON cho frontend
                // Định dạng: [lat, lng, intensity]
                // Với mật độ thiết bị, mỗi thiết bị tính là 1 đơn vị cường độ
                var heatData = devices.Select(d => new double[] { d.lat, d.lng, 1.0 }).ToList();
                HeatDataJson = JsonSerializer.Serialize(heatData);
            }
            else
            {
                HeatDataJson = "[]";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error loading heatmap data: " + ex.Message);
            HeatDataJson = "[]";
        }
    }

    public class ActiveDevice
    {
        public string deviceId { get; set; } = "";
        public double lat { get; set; }
        public double lng { get; set; }
        public DateTime lastSeen { get; set; }
    }
}
