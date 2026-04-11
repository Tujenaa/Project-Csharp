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

        // GET /api/audio/poi/{poiId} — tất cả audio của 1 POI
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
                .Include(x => x.POI)
                .Include(x => x.Language)
                .FirstOrDefaultAsync(x => x.Id == id);
            return a == null ? NotFound() : Ok(a);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AudioCreateRequest req)
        {
            var poi = await _context.POI.FindAsync(req.PoiId);
            if (poi == null) return BadRequest("POI không tồn tại.");

            var lang = await _context.Languages.FindAsync(req.LanguageId);
            if (lang == null) return BadRequest("Ngôn ngữ không tồn tại.");
            if (!lang.IsActive) return BadRequest("Ngôn ngữ này đã bị vô hiệu hóa.");

            var exists = await _context.Audio.AnyAsync(a => a.PoiId == req.PoiId && a.LanguageId == req.LanguageId);
            if (exists) return BadRequest("POI này đã có Audio cho ngôn ngữ này rồi.");

            var audio = new Audio
            {
                PoiId = req.PoiId,
                LanguageId = req.LanguageId,
                Script = req.Script ?? ""
            };
            _context.Audio.Add(audio);
            await _context.SaveChangesAsync();
            return Ok(audio);
        }

        public class AudioCreateRequest
        {
            public int PoiId { get; set; }
            public int LanguageId { get; set; }
            public string? Script { get; set; }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] AudioUpdateRequest req)
        {
            var existing = await _context.Audio.FindAsync(id);
            if (existing == null) return NotFound();
            existing.Script = req.Script ?? existing.Script;
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        public class AudioUpdateRequest
        {
            public string? Script { get; set; }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var audio = await _context.Audio.FindAsync(id);
            if (audio == null) return NotFound();
            _context.Audio.Remove(audio);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}