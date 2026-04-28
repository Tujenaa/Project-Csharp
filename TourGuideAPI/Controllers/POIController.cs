using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

namespace TourGuideAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class POIController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public POIController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private async Task LogOwnerAction(string action, string details)
        {
            try
            {
                var role = Request.Headers["X-Role"].ToString();
                if (role != "OWNER") return;

                var userIdStr = Request.Headers["X-UserId"].ToString();
                var username = Request.Headers["X-Username"].ToString();
                int? userId = int.TryParse(userIdStr, out var id) ? id : (int?)null;

                _context.UserActivities.Add(new UserActivity
                {
                    UserId = userId,
                    Username = string.IsNullOrEmpty(username) ? "Owner" : username,
                    Role = "OWNER",
                    ActivityType = action,
                    Details = details,
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
            catch { /* Chặn lỗi ghi log làm hỏng nghiệp vụ chính */ }
        }

        // ── APP: GET /api/poi — chỉ APPROVED ──
        [HttpGet]
        public async Task<IActionResult> GetPOI()
        {
            var list = await _context.POI
                .Where(p => p.Status == "APPROVED")
                .Include(p => p.Audios)
                .ThenInclude(a => a.Language)
                .ToListAsync();

            var data = list.Select(p => new POIDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Address = p.Address,
                Phone = p.Phone,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Radius = p.Radius,
                Status = p.Status,
                Audios = p.Audios.Select(a => new AudioDto
                {
                    Id = a.Id,
                    PoiId = a.PoiId,
                    LanguageId = a.LanguageId,
                    LanguageCode = a.Language?.Code,
                    Script = a.Script,
                    Language = a.Language
                }).ToList(),
                Images = _context.POIImages
                             .Where(img => img.PoiId == p.Id)
                             .Select(img => img.ImageUrl)
                             .ToList()
            }).ToList();

            return Ok(data);
        }

        // ── APP: GET /api/poi/top ──
        [HttpGet("top")]
        public async Task<IActionResult> GetTopPOI()
        {
            var topPoiIds = _context.History
                .GroupBy(h => h.PoiId)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key);

            var list = await _context.POI
                .Where(p => p.Status == "APPROVED" && topPoiIds.Contains(p.Id))
                .Include(p => p.Audios)
                .ThenInclude(a => a.Language)
                .ToListAsync();

            var data = list.Select(p => new POIDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Address = p.Address,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Radius = p.Radius,
                Status = p.Status,
                Audios = p.Audios.Select(a => new AudioDto
                {
                    Id = a.Id,
                    PoiId = a.PoiId,
                    LanguageId = a.LanguageId,
                    LanguageCode = a.Language?.Code,
                    Script = a.Script,
                    Language = a.Language
                }).ToList(),
                Images = _context.POIImages
                             .Where(img => img.PoiId == p.Id)
                             .Select(img => img.ImageUrl)
                             .ToList()
            }).ToList();

            return Ok(data);
        }

        // ── APP: GET /api/poi/count ──
        [HttpGet("count")]
        public IActionResult GetPoiCount() =>
            Ok(_context.POI.Count(p => p.Status == "APPROVED"));

        // ── APP: GET /api/poi/audio-count ──
        [HttpGet("audio-count")]
        public IActionResult GetAudioCount() => Ok(_context.History.Count());

        // ── WEB ADMIN: GET /api/poi/all ──
        [HttpGet("all")]
        public async Task<IActionResult> GetAll() =>
            Ok(await _context.POI.ToListAsync());

        // ── WEB: GET /api/poi/owner/{ownerId} ──
        [HttpGet("owner/{ownerId}")]
        public async Task<IActionResult> GetByOwner(int ownerId)
        {
            var pois = await _context.POI.Where(p => p.OwnerId == ownerId).ToListAsync();

            // Lấy thêm các yêu cầu tạo mới đang chờ duyệt của owner này
            var pendingCreates = await _context.ApprovalRequests
                .Where(r => r.RequesterId == ownerId && r.EntityType == "POI" && r.RequestType == "CREATE" && r.Status == "PENDING")
                .ToListAsync();

            // ✅ FIX: map đầy đủ các trường bao gồm Latitude, Longitude, Radius
            var result = pois.Select(p => new POIDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Address = p.Address,
                Status = p.Status,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Radius = p.Radius
            }).ToList();

            foreach (var req in pendingCreates)
            {
                try
                {
                    var data = JsonSerializer.Deserialize<POI>(req.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (data != null)
                    {
                        // ✅ FIX: map đầy đủ tọa độ từ ApprovalRequest
                        result.Add(new POIDto
                        {
                            Id = -req.Id, // Dùng ID âm để phân biệt hàng ảo
                            Name = data.Name,
                            Description = data.Description,
                            Address = data.Address,
                            Status = "PENDING",
                            Latitude = data.Latitude,
                            Longitude = data.Longitude,
                            Radius = data.Radius
                        });
                    }
                }
                catch { }
            }

            return Ok(result);
        }

        // ── ADMIN: GET /api/poi/pending ──
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            // Kết hợp POI có Status PENDING (cũ) và các ApprovalRequest (mới)
            var oldPending = await _context.POI.Where(p => p.Status == "PENDING").ToListAsync();
            var newRequests = await _context.ApprovalRequests
                .Where(r => r.EntityType == "POI" && r.Status == "PENDING")
                .ToListAsync();

            var result = oldPending.Select(p => new {
                p.Id,
                p.Name,
                p.Description,
                p.Status,
                OwnerId = (int?)p.OwnerId,
                RequestType = "UPDATE (LEGACY)",
                RequestId = (int?)null
            }).ToList();

            foreach (var req in newRequests)
            {
                try
                {
                    var data = JsonSerializer.Deserialize<POI>(req.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (data != null)
                    {
                        result.Add(new
                        {
                            Id = req.EntityId ?? 0,
                            Name = data.Name,
                            Description = data.Description,
                            Status = "PENDING",
                            OwnerId = (int?)req.RequesterId,
                            RequestType = req.RequestType,
                            RequestId = (int?)req.Id
                        });
                    }
                }
                catch { }
            }

            return Ok(result);
        }

        // ── WEB: GET /api/poi/{id} ──
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var p = await _context.POI
                .Include(poi => poi.Audios)
                .ThenInclude(a => a.Language)
                .FirstOrDefaultAsync(poi => poi.Id == id);

            if (p == null) return NotFound();

            var data = new POIDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Address = p.Address,
                Phone = p.Phone,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Radius = p.Radius,
                Status = p.Status,
                Audios = p.Audios.Select(a => new AudioDto
                {
                    Id = a.Id,
                    PoiId = a.PoiId,
                    LanguageId = a.LanguageId,
                    LanguageCode = a.Language?.Code,
                    Script = a.Script,
                    Language = a.Language
                }).ToList(),
                Images = _context.POIImages
                             .Where(img => img.PoiId == p.Id)
                             .Select(img => img.ImageUrl)
                             .ToList()
            };

            return Ok(data);
        }

        // ── WEB: GET /api/poi/{id}/images ──
        [HttpGet("{id}/images")]
        public async Task<IActionResult> GetImages(int id)
        {
            if (id < 0)
            {
                var req = await _context.ApprovalRequests.FindAsync(-id);
                if (req == null) return Ok(new List<POIImage>());
                try
                {
                    var data = JsonSerializer.Deserialize<POI>(req.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return Ok(data?.Images ?? new List<POIImage>());
                }
                catch { return Ok(new List<POIImage>()); }
            }

            var images = await _context.POIImages
                .Where(img => img.PoiId == id)
                .ToListAsync();
            return Ok(images);
        }

        // ── WEB: POST /api/poi ──
        [HttpPost]
        public async Task<IActionResult> Create()
        {
            Request.EnableBuffering();
            var raw = await new System.IO.StreamReader(Request.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(raw))
                return BadRequest($"Body rỗng. ContentType={Request.ContentType}");
            POI? poi;
            try
            {
                poi = JsonSerializer.Deserialize<POI>(raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex) { return BadRequest($"JSON lỗi: {ex.Message}"); }
            if (poi == null) return BadRequest("Deserialize null");
            if (string.IsNullOrWhiteSpace(poi.Name)) return BadRequest("Name rỗng");

            var role = Request.Headers["X-Role"].ToString();
            var userIdStr = Request.Headers["X-UserId"].ToString();
            int.TryParse(userIdStr, out var userId);
            var username = Request.Headers["X-Username"].ToString();

            if (role == "OWNER")
            {
                // Owner tạo → tạo yêu cầu duyệt
                var request = new ApprovalRequest
                {
                    EntityType = "POI",
                    RequestType = "CREATE",
                    Content = raw,
                    RequesterId = userId,
                    RequesterName = username,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                };
                _context.ApprovalRequests.Add(request);
                await _context.SaveChangesAsync();
                await LogOwnerAction("REQUEST_CREATE_POI", $"Owner đã gửi yêu cầu tạo điểm mới: {poi.Name}");
                return Ok(new { message = "Yêu cầu tạo điểm mới đã được gửi và đang chờ duyệt.", requestId = request.Id });
            }

            // Admin tạo → thêm thẳng
            poi.Id = 0;
            poi.OwnerName = null;
            if (string.IsNullOrEmpty(poi.Status)) poi.Status = "APPROVED";
            _context.POI.Add(poi);
            await _context.SaveChangesAsync();
            return Ok(poi);
        }

        // ── WEB: PUT /api/poi/{id} ──
        // Admin sửa thẳng; Owner sửa → tạo yêu cầu duyệt, không đè lên bản cũ
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] POI poi)
        {
            var role = Request.Headers["X-Role"].ToString();
            var isAdmin = role == "ADMIN";
            var userIdStr = Request.Headers["X-UserId"].ToString();
            int.TryParse(userIdStr, out var userId);
            var username = Request.Headers["X-Username"].ToString();

            if (id < 0)
            {
                if (isAdmin) return BadRequest("Admin không thể sửa điểm đang chờ duyệt");
                var req = await _context.ApprovalRequests.FindAsync(-id);
                if (req == null || req.Status != "PENDING") return NotFound();
                
                // Keep original request type but update the content with new POI data
                req.Content = JsonSerializer.Serialize(poi);
                await _context.SaveChangesAsync();
                await LogOwnerAction("UPDATE_PENDING_POI", $"Owner đã cập nhật điểm chờ duyệt: {poi.Name}");
                return Ok(new { message = "Yêu cầu đã được cập nhật.", requestId = req.Id });
            }

            var existing = await _context.POI.FindAsync(id);
            if (existing == null) return NotFound();

            if (isAdmin)
            {
                existing.Name = poi.Name;
                existing.Description = poi.Description;
                existing.Address = poi.Address;
                existing.Phone = poi.Phone;
                existing.Latitude = poi.Latitude;
                existing.Longitude = poi.Longitude;
                existing.Radius = poi.Radius;
                existing.OwnerId = poi.OwnerId;
                existing.ImageUrl = poi.ImageUrl ?? existing.ImageUrl;
                existing.RejectReason = poi.RejectReason;

                if (!string.IsNullOrEmpty(poi.Status))
                    existing.Status = poi.Status;

                await _context.SaveChangesAsync();
                return Ok(existing);
            }
            else
            {
                // Owner sửa → tạo yêu cầu duyệt
                var request = new ApprovalRequest
                {
                    EntityId = id,
                    EntityType = "POI",
                    RequestType = "UPDATE",
                    Content = JsonSerializer.Serialize(poi),
                    RequesterId = userId,
                    RequesterName = username,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                };
                _context.ApprovalRequests.Add(request);
                await _context.SaveChangesAsync();
                await LogOwnerAction("REQUEST_UPDATE_POI", $"Owner đã gửi yêu cầu cập nhật điểm: {existing.Name}");
                return Ok(new { message = "Yêu cầu cập nhật đã được gửi và đang chờ duyệt.", requestId = request.Id });
            }
        }

        // ── ADMIN: PUT /api/poi/{id}/approve ──
        [HttpPut("{id}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            var p = await _context.POI.FindAsync(id);
            if (p == null) return NotFound();
            p.Status = "APPROVED";
            p.RejectReason = null;
            await _context.SaveChangesAsync();
            return Ok(p);
        }

        // ── ADMIN: PUT /api/poi/{id}/reject ──
        [HttpPut("{id}/reject")]
        public async Task<IActionResult> Reject(int id, [FromBody] RejectRequest req)
        {
            var p = await _context.POI.FindAsync(id);
            if (p == null) return NotFound();
            p.Status = "REJECTED";
            p.RejectReason = req.Reason;
            await _context.SaveChangesAsync();
            return Ok(p);
        }

        // ── WEB: DELETE /api/poi/{id} ──
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // 1. Xử lý xóa yêu cầu đang chờ duyệt (Id âm)
                if (id < 0)
                {
                    var req = await _context.ApprovalRequests.FindAsync(-id);
                    if (req != null)
                    {
                        _context.ApprovalRequests.Remove(req);
                        await _context.SaveChangesAsync();
                    }
                    return Ok();
                }

                // 2. Tìm POI thật
                var poi = await _context.POI.FindAsync(id);
                if (poi == null) return NotFound();
                var poiName = poi.Name;

                // 3. Xử lý xóa tất cả các bảng liên quan trong một đợt
                
                // Lấy danh sách AudioId để xóa ApprovalRequests liên quan đến Audio
                var audioIds = await _context.Audio.Where(a => a.PoiId == id).Select(a => a.Id).ToListAsync();

                // Xóa History
                var histories = await _context.History.Where(h => h.PoiId == id).ToListAsync();
                if (histories.Any()) _context.History.RemoveRange(histories);
                
                // Xóa Images và File vật lý
                var images = await _context.POIImages.Where(img => img.PoiId == id).ToListAsync();
                if (images.Any())
                {
                    foreach (var img in images)
                    {
                        if (!string.IsNullOrEmpty(img.ImageUrl))
                        {
                            var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", img.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(filePath))
                            {
                                try { System.IO.File.Delete(filePath); } catch { }
                            }
                        }
                    }
                    _context.POIImages.RemoveRange(images);
                }

                // Xóa Audio
                var audios = await _context.Audio.Where(a => a.PoiId == id).ToListAsync();
                if (audios.Any()) _context.Audio.RemoveRange(audios);

                // Xóa TourPOI
                var tourPois = await _context.TourPOI.Where(tp => tp.PoiId == id).ToListAsync();
                if (tourPois.Any()) _context.TourPOI.RemoveRange(tourPois);

                // Xóa QRCodes
                var qrs = await _context.QRCodes.Where(q => q.PoiId == id).ToListAsync();
                if (qrs.Any()) _context.QRCodes.RemoveRange(qrs);

                // Xóa ApprovalRequests của POI này (bao gồm cả CREATE/UPDATE của POI và AUDIO của POI)
                var poiReqs = await _context.ApprovalRequests
                    .Where(r => (r.EntityType == "POI" && r.EntityId == id) || 
                                (r.EntityType == "AUDIO" && r.EntityId.HasValue && audioIds.Contains(r.EntityId.Value)))
                    .ToListAsync();
                if (poiReqs.Any()) _context.ApprovalRequests.RemoveRange(poiReqs);

                // 4. Xóa POI chính
                _context.POI.Remove(poi);

                // 5. Lưu tất cả thay đổi một lần duy nhất
                await _context.SaveChangesAsync();

                try { await LogOwnerAction("DELETE_POI", $"Owner đã xóa điểm thuyết minh: {poiName}"); } catch { }
                return Ok();
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Lỗi xóa POI: {inner}");
            }
        }

        // ── ADMIN: DELETE /api/poi/rejected ──
        [HttpDelete("rejected")]
        public async Task<IActionResult> DeleteRejected()
        {
            var list = await _context.POI.Where(p => p.Status == "REJECTED").ToListAsync();
            _context.POI.RemoveRange(list);
            await _context.SaveChangesAsync();
            return Ok($"Đã xóa {list.Count} POI bị từ chối.");
        }

        // ── WEB: PUT /api/poi/image/{imageId}/thumbnail ──
        [HttpPut("image/{imageId}/thumbnail")]
        public async Task<IActionResult> SetThumbnail(int imageId)
        {
            var image = await _context.POIImages.FindAsync(imageId);
            if (image == null) return NotFound();
            var others = await _context.POIImages
                .Where(img => img.PoiId == image.PoiId)
                .ToListAsync();
            foreach (var img in others) img.IsThumbnail = false;
            image.IsThumbnail = true;
            await _context.SaveChangesAsync();
            return Ok(image);
        }

        // ── WEB: DELETE /api/poi/image/{imageId} ──
        [HttpDelete("image/{imageId}")]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            var image = await _context.POIImages.FindAsync(imageId);
            if (image == null) return NotFound();
            if (!string.IsNullOrEmpty(image.ImageUrl))
            {
                var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", image.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            _context.POIImages.Remove(image);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ── WEB: POST /api/poi/{id}/image ──
        [HttpPost("{id}/image")]
        public async Task<IActionResult> UploadImage(int id, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext)) return BadRequest("Chỉ hỗ trợ jpg/png/webp");
            if (file.Length > 5_242_880) return BadRequest("Ảnh tối đa 5MB");

            // Xử lý POI CHỜ DUYỆT (Id âm)
            if (id < 0)
            {
                var req = await _context.ApprovalRequests.FindAsync(-id);
                if (req == null) return NotFound("Không tìm thấy yêu cầu chờ duyệt.");

                var dirP = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "poi", "pending");
                Directory.CreateDirectory(dirP);
                var fileNameP = $"pending_{(-id)}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
                var pathP = Path.Combine(dirP, fileNameP);

                using (var stream = new FileStream(pathP, FileMode.Create))
                    await file.CopyToAsync(stream);

                var imageUrl = $"/uploads/poi/pending/{fileNameP}";

                try
                {
                    var data = JsonSerializer.Deserialize<POI>(req.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (data != null)
                    {
                        if (data.Images == null) data.Images = new List<POIImage>();
                        data.Images.Add(new POIImage { ImageUrl = imageUrl });
                        req.Content = JsonSerializer.Serialize(data);
                        await _context.SaveChangesAsync();
                        return Ok(new { imageUrl });
                    }
                }
                catch { return BadRequest("Lỗi xử lý dữ liệu yêu cầu."); }
            }

            var p = await _context.POI.FindAsync(id);
            if (p == null) return NotFound();

            var dir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "poi");
            Directory.CreateDirectory(dir);
            var fileName = $"poi_{id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
            var path = Path.Combine(dir, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
                await file.CopyToAsync(stream);

            var finalUrl = $"/uploads/poi/{fileName}";
            var isFirst = !await _context.POIImages.AnyAsync(img => img.PoiId == id);
            _context.POIImages.Add(new POIImage
            {
                PoiId = id,
                ImageUrl = finalUrl,
                IsThumbnail = isFirst
            });
            if (isFirst) p.ImageUrl = finalUrl;

            await _context.SaveChangesAsync();
            return Ok(new { imageUrl = finalUrl });
        }
    }

    public record RejectRequest(string Reason);
}