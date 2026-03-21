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
        public POIController(AppDbContext context) => _context = context;

        // ── APP: GET /api/poi — trả về POIDto kèm audio (dùng cho mobile app) ──
        [HttpGet]
        public async Task<IActionResult> GetPOI()
        {
            var data = await (
                from p in _context.POI
                join a in _context.Audio on p.Id equals a.PoiId into pa
                from a in pa.DefaultIfEmpty()
                select new POIDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Radius = p.Radius,
                    AudioUrl = a != null ? a.AudioUrl : null,
                    Script = a != null ? a.Script : null
                }
            ).ToListAsync();
            return Ok(data);
        }

        // ── APP: POST /api/poi/history — app ghi lịch sử phát ──
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

        // ── APP: GET  /api/POI/top - Lấy top 5 POI được nghe nhiều nhất -
        [HttpGet("top")]
        public IActionResult GetTopPOI()
        {
            var topPoiIds = _context.History
                .GroupBy(h => h.PoiId)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            var pois = _context.POI
                .Where(p => topPoiIds.Contains(p.Id))
                .ToList();

            return Ok(pois);
        }

        // ── APP: GET /api/poi/count — đếm tổng số POI ──
        [HttpGet("count")]
        public IActionResult GetPoiCount()
        {
            var count = _context.POI.Count();
            return Ok(count);
        }
        // ── APP: GET /api/poi/audio-count — đếm tổng số audio ──
        [HttpGet("audio-count")]
        public IActionResult GetAudioCount()
        {
            var count = _context.History.Count();
            return Ok(count);
        }


        // ── WEB: GET /api/poi/{id} — lấy 1 POI theo id ──
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var poi = await _context.POI.FindAsync(id);
            return poi == null ? NotFound() : Ok(poi);
        }

        // ── WEB: POST /api/poi — thêm POI mới ──
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
                poi = JsonSerializer.Deserialize<POI>(raw, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex) { return BadRequest($"JSON lỗi: {ex.Message}"); }

            if (poi == null) return BadRequest("Deserialize null");
            if (string.IsNullOrWhiteSpace(poi.Name)) return BadRequest("Name rỗng");

            poi.Id = 0;
            poi.OwnerName = null;
            _context.POI.Add(poi);
            await _context.SaveChangesAsync();
            return Ok(poi);
        }

        // ── WEB: PUT /api/poi/{id} — cập nhật POI ──
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] POI poi)
        {
            var existing = await _context.POI.FindAsync(id);
            if (existing == null) return NotFound();
            existing.Name = poi.Name;
            existing.Description = poi.Description;
            existing.Latitude = poi.Latitude;
            existing.Longitude = poi.Longitude;
            existing.Radius = poi.Radius;
            existing.OwnerId = poi.OwnerId;
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        // ── WEB: DELETE /api/poi/{id} — xoá POI ──
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var poi = await _context.POI.FindAsync(id);
            if (poi == null) return NotFound();
            _context.POI.Remove(poi);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}