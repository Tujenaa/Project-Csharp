using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

namespace TourGuideAPI.Controllers
{
    [Route("api/audio-extra")]
    [ApiController]
    public class AudioExtraController : ControllerBase
    {
        private readonly AppDbContext _context;
        public AudioExtraController(AppDbContext context) => _context = context;

        // GET /api/audio-extra/audio/{audioId} — lấy tất cả ngôn ngữ extra của 1 audio
        [HttpGet("audio/{audioId}")]
        public async Task<IActionResult> GetByAudio(int audioId)
        {
            var list = await _context.AudioExtra
                .Where(e => e.AudioId == audioId)
                .OrderBy(e => e.LangCode)
                .ToListAsync();
            return Ok(list);
        }

        // POST /api/audio-extra — thêm ngôn ngữ mới
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AudioExtra extra)
        {
            // Kiểm tra audio tồn tại
            var audio = await _context.Audio.FindAsync(extra.AudioId);
            if (audio == null) return BadRequest("Audio không tồn tại.");

            // Kiểm tra LangCode đã tồn tại chưa
            var exists = await _context.AudioExtra
                .AnyAsync(e => e.AudioId == extra.AudioId &&
                               e.LangCode.ToLower() == extra.LangCode.ToLower());
            if (exists) return BadRequest($"Ngôn ngữ '{extra.LangCode}' đã tồn tại cho audio này.");

            extra.Audio = null;
            _context.AudioExtra.Add(extra);
            await _context.SaveChangesAsync();
            return Ok(extra);
        }

        // PUT /api/audio-extra/{id} — sửa script
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] AudioExtra data)
        {
            var existing = await _context.AudioExtra.FindAsync(id);
            if (existing == null) return NotFound();
            existing.LangName = data.LangName;
            existing.Script = data.Script;
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        // DELETE /api/audio-extra/{id} — xóa ngôn ngữ
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var extra = await _context.AudioExtra.FindAsync(id);
            if (extra == null) return NotFound();
            _context.AudioExtra.Remove(extra);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}