using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GPSGuide.Web.Models;
using System.Net.Http.Json;

namespace GPSGuide.Web.Pages
{
    public class QrCodeManagerModel : PageModel
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;

        public QrCodeManagerModel(IHttpClientFactory http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        public List<QRCodeDto> QrCodes { get; set; } = new();
        public List<POI> POIS { get; set; } = new();
        public bool IsAdmin { get; set; }
        public int MyUserId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            ViewData["Title"] = "Quản lý mã QR";
            ViewData["Active"] = "qrcodes";

            IsAdmin = HttpContext.Session.GetString("Role") == "ADMIN";
            var idStr = HttpContext.Session.GetString("UserId");
            MyUserId = int.TryParse(idStr, out var id) ? id : 0;

            var client = ApiWithRole();

            try
            {
                // 1. Lấy danh sách POI (đã được duyệt) - Đây chính là các QR mặc định của App
                string poiEndpoint = IsAdmin ? "poi/all" : $"poi/owner/{MyUserId}";
                POIS = await client.GetFromJsonAsync<List<POI>>(poiEndpoint) ?? new();
                POIS = POIS.Where(p => p.Status == "APPROVED").ToList();

                // 2. Lấy danh sách các bản tùy chỉnh QR từ database (nếu có)
                var savedQrs = await client.GetFromJsonAsync<List<QRCodeDto>>("qrcodes") ?? new();

                // 3. Kết hợp: Mỗi POI chắc chắn có 1 QR code
                QrCodes = new List<QRCodeDto>();
                foreach (var poi in POIS)
                {
                    // Ưu tiên bản ghi tùy chỉnh nếu đã được lưu trong DB
                    var custom = savedQrs.FirstOrDefault(q => q.PoiId == poi.Id);
                    if (custom != null)
                    {
                        QrCodes.Add(custom);
                    }
                    else
                    {
                        // Nếu chưa có trong DB, tạo bản ghi ảo dựa trên logic App
                        QrCodes.Add(new QRCodeDto(
                            Id: 0, // 0 nghĩa là chưa lưu tùy chỉnh
                            Name: poi.Name,
                            PoiId: poi.Id,
                            PoiName: poi.Name,
                            Content: $"tourguideapp://poi/{poi.Id}",
                            OwnerId: poi.OwnerId ?? 0,
                            CreatedAt: DateTime.Now
                        ));
                    }
                }

                // Thêm các mã QR tự do (không gắn với POI nào) vào cuối danh sách
                var standalone = savedQrs.Where(q => q.PoiId == null).ToList();
                QrCodes.AddRange(standalone);
            }
            catch 
            { 
                QrCodes = new(); 
                POIS = new();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id, string name, int? poiId, string content, string action)
        {
            var client = ApiWithRole();
            
            try
            {
                if (action == "delete" && id.HasValue && id.Value > 0)
                {
                    await client.DeleteAsync($"qrcodes/{id}");
                }
                else if (action == "save")
                {
                    if (id.HasValue && id.Value > 0) // Update
                    {
                        await client.PutAsJsonAsync($"qrcodes/{id}", new { Name = name, PoiId = poiId, Content = content });
                    }
                    else // Create hoặc Save virtual to DB
                    {
                        await client.PostAsJsonAsync("qrcodes", new { Name = name, PoiId = poiId, Content = content });
                    }
                }
            }
            catch { }

            return RedirectToPage();
        }

        private HttpClient ApiWithRole()
        {
            var client = _http.CreateClient("API");
            client.DefaultRequestHeaders.Add("X-Role", HttpContext.Session.GetString("Role"));
            client.DefaultRequestHeaders.Add("X-UserId", HttpContext.Session.GetString("UserId") ?? "0");
            client.DefaultRequestHeaders.Add("X-Username", HttpContext.Session.GetString("Username") ?? "Unknown");
            return client;
        }

        public record QRCodeDto(int Id, string Name, int? PoiId, string? PoiName, string Content, int OwnerId, DateTime CreatedAt);
    }
}
