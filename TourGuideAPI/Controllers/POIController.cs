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
                    Script = a.Script
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
                    Script = a.Script
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
                    Script = a.Script
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
            poi.Id = 0;
            poi.OwnerName = null;
            if (string.IsNullOrEmpty(poi.Status)) poi.Status = "PENDING";
            _context.POI.Add(poi);
            await _context.SaveChangesAsync();
            return Ok(poi);
        }

        // ── WEB: PUT /api/poi/{id} ──
        // Admin sửa thẳng; Owner sửa → về PENDING chờ duyệt lại
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] POI poi)
        {
            var existing = await _context.POI.FindAsync(id);
            if (existing == null) return NotFound();

            var isAdmin = Request.Headers.TryGetValue("X-Role", out var role) && role == "ADMIN";

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

            if (isAdmin)
            {
                if (!string.IsNullOrEmpty(poi.Status))
                    existing.Status = poi.Status;
            }
            else
            {
                // Owner sửa → chờ duyệt lại
                existing.Status = "PENDING";
            }

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

            var audios = _context.Audio.Where(a => a.PoiId == id);
            _context.Audio.RemoveRange(audios);
            var histories = _context.History.Where(h => h.PoiId == id);
            _context.History.RemoveRange(histories);
            var images = _context.POIImages.Where(img => img.PoiId == id);
            _context.POIImages.RemoveRange(images);

            _context.POI.Remove(poi);
            await _context.SaveChangesAsync();
            return Ok();
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
            var p = await _context.POI.FindAsync(id);
            if (p == null) return NotFound();
            if (file == null || file.Length == 0) return BadRequest("No file");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext)) return BadRequest("Chỉ hỗ trợ jpg/png/webp");
            if (file.Length > 5_242_880) return BadRequest("Ảnh tối đa 5MB");

            var dir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "poi");
            Directory.CreateDirectory(dir);
            var fileName = $"poi_{id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
            var path = Path.Combine(dir, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
                await file.CopyToAsync(stream);

            var imageUrl = $"/uploads/poi/{fileName}";
            var isFirst = !await _context.POIImages.AnyAsync(img => img.PoiId == id);
            _context.POIImages.Add(new POIImage
            {
                PoiId = id,
                ImageUrl = imageUrl,
                IsThumbnail = isFirst
            });
            if (isFirst) p.ImageUrl = imageUrl;

            await _context.SaveChangesAsync();
            return Ok(new { imageUrl });
        }
    }

    public record RejectRequest(string Reason);
}