using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

namespace TourGuideAPI.Controllers
{
    [Route("api/audio")]
    [ApiController]
    public class AudioController : ControllerBase
    {
        private readonly AppDbContext _context;
        public AudioController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.Audio
                .Include(a => a.POI)
                .Include(a => a.Language)
                .Select(a => new {
                    a.Id,
                    a.PoiId,
                    PoiName = a.POI != null ? a.POI.Name : null,
                    a.LanguageId,
                    LanguageCode = a.Language != null ? a.Language.Code : null,
                    LanguageName = a.Language != null ? a.Language.Name : null,
                    a.Script
                })
                .ToListAsync();
            return Ok(list);
        }

        [HttpGet("poi/{poiId}")]
        public async Task<IActionResult> GetByPoi(int poiId)
        {
            var list = await _context.Audio
                .Include(a => a.Language)
                .Where(a => a.PoiId == poiId)
                .Select(a => new {
                    a.Id,
                    a.PoiId,
                    a.LanguageId,
                    LanguageCode = a.Language != null ? a.Language.Code : null,
                    LanguageName = a.Language != null ? a.Language.Name : null,
                    a.Script
                })
                .OrderBy(a => a.LanguageId)
                .ToListAsync();
            return Ok(list);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var a = await _context.Audio
                .Include(x => x.POI).Include(x => x.Language)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();
            return Ok(new
            {
                a.Id,
                a.PoiId,
                PoiName = a.POI?.Name,
                a.LanguageId,
                LanguageCode = a.Language?.Code,
                LanguageName = a.Language?.Name,
                a.Script
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AudioCreateRequest req)
        {
            // Dùng AsNoTracking để tránh EF track navigation
            var poiExists = await _context.POI.AsNoTracking()
                .AnyAsync(p => p.Id == req.PoiId);
            if (!poiExists) return BadRequest("POI không tồn tại.");

            var lang = await _context.Languages.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == req.LanguageId);
            if (lang == null) return BadRequest("Ngôn ngữ không tồn tại.");
            if (!lang.IsActive) return BadRequest("Ngôn ngữ này đã bị vô hiệu hóa.");

            var exists = await _context.Audio.AsNoTracking()
                .AnyAsync(a => a.PoiId == req.PoiId && a.LanguageId == req.LanguageId);
            if (exists) return BadRequest("POI này đã có Audio cho ngôn ngữ này rồi.");

            // Insert thẳng SQL, không qua EF change tracker
            var rows = await _context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Audio (PoiId, LanguageId, Script) VALUES ({0}, {1}, {2})",
                req.PoiId, req.LanguageId, req.Script ?? "");

            return Ok(new { req.PoiId, req.LanguageId, req.Script });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] AudioUpdateRequest req)
        {
            var rows = await _context.Audio
                .Where(a => a.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Script, req.Script ?? ""));
            if (rows == 0) return NotFound();
            return Ok(new { id, script = req.Script });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var rows = await _context.Audio
                .Where(a => a.Id == id)
                .ExecuteDeleteAsync();
            if (rows == 0) return NotFound();
            return Ok();
        }

        public class AudioCreateRequest
        {
            public int PoiId { get; set; }
            public int LanguageId { get; set; }
            public string? Script { get; set; }
        }

        public class AudioUpdateRequest
        {
            public string? Script { get; set; }
        }
    }
}