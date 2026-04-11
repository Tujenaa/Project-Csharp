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
        public async Task<IActionResult> Create([FromBody] Audio audio)
        {
            // Kiểm tra POI đã được APPROVED chưa
            var poi = await _context.POI.FindAsync(audio.PoiId);
            if (poi == null) return BadRequest("POI không tồn tại.");
            
            // Kiểm tra ngôn ngữ tồn tại
            var lang = await _context.Languages.FindAsync(audio.LanguageId);
            if (lang == null) return BadRequest("Ngôn ngữ không tồn tại.");

            // Kiểm tra xem POI này đã có audio của ngôn ngữ này chưa
            var exists = await _context.Audio.AnyAsync(a => a.PoiId == audio.PoiId && a.LanguageId == audio.LanguageId);
            if (exists) return BadRequest("POI này đã có Audio cho ngôn ngữ này rồi.");

            audio.POI = null;
            audio.Language = null;
            _context.Audio.Add(audio);
            await _context.SaveChangesAsync();
            return Ok(audio);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Audio audio)
        {
            var existing = await _context.Audio.FindAsync(id);
            if (existing == null) return NotFound();
            
            existing.PoiId = audio.PoiId;
            existing.LanguageId = audio.LanguageId;
            existing.Script = audio.Script;
            
            await _context.SaveChangesAsync();
            return Ok(existing);
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