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
                    a.Language,
                    a.AudioUrl,
                    a.Script
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
            existing.Language = audio.Language;
            existing.AudioUrl = audio.AudioUrl;
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