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
                .Select(a => new {
                    a.Id,
                    a.PoiId,
                    PoiName = a.POI != null ? a.POI.Name : null,
                    a.vi,
                    a.en,
                    a.ja,
                    a.zh
                })
                .ToListAsync();
            return Ok(list);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var a = await _context.Audio.Include(x => x.POI).FirstOrDefaultAsync(x => x.Id == id);
            return a == null ? NotFound() : Ok(a);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Audio audio)
        {
            // Kiểm tra xem POI đã có audio chưa
            var exists = await _context.Audio.AnyAsync(a => a.PoiId == audio.PoiId);
            if (exists) return BadRequest("POI này đã có Audio rồi.");

            // Đảm bảo không insert navigation property
            audio.POI = null;
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
            existing.vi = audio.vi;
            existing.en = audio.en;
            existing.ja = audio.ja;
            existing.zh = audio.zh;
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