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

        // ── APP: GET /api/poi — chỉ APPROVED ──
        [HttpGet]
        public async Task<IActionResult> GetPOI()
        {
            var data = await (
                from p in _context.POI
                where p.Status == "APPROVED"
                join a in _context.Audio on p.Id equals a.PoiId into pa
                from a in pa.DefaultIfEmpty()
                select new POIDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Address = p.Address,
                    Phone = p.Phone,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Radius = p.Radius,
                    ScriptVi = a != null ? a.vi : p.Description,
                    ScriptEn = a != null ? a.en : null,
                    ScriptJa = a != null ? a.ja : null,
                    ScriptZh = a != null ? a.zh : null,
                    Images = _context.POIImages.Where(img => img.PoiId == p.Id).Select(img => img.ImageUrl).ToList()
                }
            ).ToListAsync();
            return Ok(data);
        }

        // ── APP: POST /api/poi/history ──
        [HttpPost("history")]
        public async Task<IActionResult> SaveHistory([FromBody] HistoryRequest request)
        {
            if (request.PoiId <= 0) return BadRequest("poiId không hợp lệ");
            var exists = await _context.POI.AnyAsync(p => p.Id == request.PoiId);
            if (!exists) return BadRequest("POI không tồn tại");
            _context.History.Add(new History { PoiId = request.PoiId, PlayTime = DateTime.Now });
            await _context.SaveChangesAsync();
            return Ok("Lưu thành công");
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

            var data = await (
                from p in _context.POI
                where p.Status == "APPROVED"
                join a in _context.Audio on p.Id equals a.PoiId into pa
                from a in pa.DefaultIfEmpty()
                where topPoiIds.Contains(p.Id)
                select new POIDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Address = p.Address,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Radius = p.Radius,
                    ScriptVi = a != null ? a.vi : p.Description,
                    ScriptEn = a != null ? a.en : null,
                    ScriptJa = a != null ? a.ja : null,
                    ScriptZh = a != null ? a.zh : null,
                    Images = _context.POIImages.Where(img => img.PoiId == p.Id).Select(img => img.ImageUrl).ToList()
                }
            ).ToListAsync();
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
        public async Task<IActionResult> GetByOwner(int ownerId) =>
            Ok(await _context.POI.Where(p => p.OwnerId == ownerId).ToListAsync());

        // ── ADMIN: GET /api/poi/pending ──
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending() =>
            Ok(await _context.POI.Where(p => p.Status == "PENDING").ToListAsync());

        // ── WEB: GET /api/poi/{id} ──
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var poi = await _context.POI.FindAsync(id);
            return poi == null ? NotFound() : Ok(poi);
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
            poi.Id = 0;
            poi.OwnerName = null;
            // Status đã được set từ Web (PENDING hoặc APPROVED)
            if (string.IsNullOrEmpty(poi.Status)) poi.Status = "PENDING";
            _context.POI.Add(poi);
            await _context.SaveChangesAsync();
            return Ok(poi);
        }

        // ── WEB: PUT /api/poi/{id} ──
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] POI poi)
        {
            var existing = await _context.POI.FindAsync(id);
            if (existing == null) return NotFound();
            existing.Name = poi.Name;
            existing.Description = poi.Description;
            existing.Address = poi.Address;
            existing.Phone = poi.Phone;
            existing.Latitude = poi.Latitude;
            existing.Longitude = poi.Longitude;
            existing.Radius = poi.Radius;
            existing.OwnerId = poi.OwnerId;
            existing.ImageUrl = poi.ImageUrl ?? existing.ImageUrl;
            if (!string.IsNullOrEmpty(poi.Status))
                existing.Status = poi.Status;
            existing.RejectReason = poi.RejectReason;
            await _context.SaveChangesAsync();
            return Ok(existing);
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
            var poi = await _context.POI.FindAsync(id);
            if (poi == null) return NotFound();
            _context.POI.Remove(poi);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ── WEB: GET /api/poi/{id}/images ──
        [HttpGet("{id}/images")]
        public async Task<IActionResult> GetImages(int id)
        {
            var images = await _context.POIImages.Where(img => img.PoiId == id).ToListAsync();
            return Ok(images);
        }

        // ── WEB: POST /api/poi/{id}/image — upload ảnh vào thư mục Web ──
        [HttpPost("{id}/image")]
        public async Task<IActionResult> UploadImage(int id, IFormFile file)
        {
            var p = await _context.POI.FindAsync(id);
            if (p == null) return NotFound("POI không tồn tại");
            if (file == null || file.Length == 0) return BadRequest("Không có file");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext)) return BadRequest("Chỉ hỗ trợ jpg/png/webp");

            try
            {
                // Đường dẫn tương đối sang project Web
                var webImageDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "TourGuideWeb", "wwwroot", "image"));
                if (!Directory.Exists(webImageDir)) Directory.CreateDirectory(webImageDir);

                var fileName = $"poi_{id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{ext}";
                var filePath = Path.Combine(webImageDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);

                // Lưu vào bảng POIImages
                var poiImage = new POIImage
                {
                    PoiId = id,
                    ImageUrl = $"/image/{fileName}", // App/Web sẽ dùng đường dẫn tuyệt đối sau
                    IsThumbnail = !await _context.POIImages.AnyAsync(img => img.PoiId == id) // Nếu là ảnh đầu tiên thì auto Thumbnail
                };

                _context.POIImages.Add(poiImage);
                await _context.SaveChangesAsync();

                return Ok(poiImage);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        // ── WEB: DELETE /api/poi/image/{imageId} ──
        [HttpDelete("image/{imageId}")]
        public async Task<IActionResult> DeletePOIImage(int imageId)
        {
            var img = await _context.POIImages.FindAsync(imageId);
            if (img == null) return NotFound();

            // Xóa file (tùy chọn, thường nên xóa)
            try
            {
                var webImageDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "TourGuideWeb", "wwwroot"));
                var fullPath = Path.Combine(webImageDir, img.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            } catch { }

            _context.POIImages.Remove(img);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ── WEB: PUT /api/poi/image/{imageId}/thumbnail ──
        [HttpPut("image/{imageId}/thumbnail")]
        public async Task<IActionResult> SetThumbnail(int imageId)
        {
            var img = await _context.POIImages.FindAsync(imageId);
            if (img == null) return NotFound();

            // Tắt các thumbnail khác của cùng POI
            var others = await _context.POIImages.Where(i => i.PoiId == img.PoiId && i.IsThumbnail).ToListAsync();
            foreach (var o in others) o.IsThumbnail = false;

            img.IsThumbnail = true;
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ── WEB: DELETE /api/poi/rejected ──
        [HttpDelete("rejected")]
        public async Task<IActionResult> DeleteRejected()
        {
            var rejected = await _context.POI
                .Where(p => p.Status == "REJECTED" || p.Status == null)
                .ToListAsync();

            if (rejected.Count == 0) return Ok("Không có POI nào bị từ chối");

            foreach (var p in rejected)
            {
                // Xóa ảnh liên quan
                var images = await _context.POIImages.Where(i => i.PoiId == p.Id).ToListAsync();
                foreach (var img in images)
                {
                    try {
                        var webImageDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "TourGuideWeb", "wwwroot"));
                        var fullPath = Path.Combine(webImageDir, img.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                    } catch { }
                }
                _context.POIImages.RemoveRange(images);
                _context.POI.Remove(p);
            }

            await _context.SaveChangesAsync();
            return Ok($"Đã xóa {rejected.Count} POI bị từ chối");
        }
    }

    public record RejectRequest(string Reason);
    public record HistoryRequest(int UserId, int PoiId);
}