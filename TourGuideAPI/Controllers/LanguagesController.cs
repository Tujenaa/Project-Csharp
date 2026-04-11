using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

namespace TourGuideAPI.Controllers
{
    [Route("api/languages")]
    [ApiController]
    public class LanguagesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public LanguagesController(AppDbContext context) => _context = context;

        // GET /api/languages — tất cả (kể cả inactive)
        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _context.Languages.OrderBy(l => l.OrderIndex).ToListAsync());

        // GET /api/languages/active — chỉ active (dành cho App + AudioManager)
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
            => Ok(await _context.Languages.Where(l => l.IsActive).OrderBy(l => l.OrderIndex).ToListAsync());

        // GET /api/languages/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var l = await _context.Languages.FindAsync(id);
            return l == null ? NotFound() : Ok(l);
        }

        // POST /api/languages
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Language lang)
        {
            if (string.IsNullOrWhiteSpace(lang.Code) || string.IsNullOrWhiteSpace(lang.Name))
                return BadRequest("Code và Name không được để trống.");
            if (await _context.Languages.AnyAsync(l => l.Code == lang.Code.Trim().ToLower()))
                return BadRequest($"Mã ngôn ngữ '{lang.Code}' đã tồn tại.");
            lang.Code = lang.Code.Trim().ToLower();
            if (lang.OrderIndex == 0)
                lang.OrderIndex = (await _context.Languages.MaxAsync(l => (int?)l.OrderIndex) ?? 0) + 1;
            _context.Languages.Add(lang);
            await _context.SaveChangesAsync();
            return Ok(lang);
        }

        // PUT /api/languages/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Language lang)
        {
            var existing = await _context.Languages.FindAsync(id);
            if (existing == null) return NotFound();
            existing.Name = lang.Name ?? existing.Name;
            existing.Code = lang.Code?.Trim().ToLower() ?? existing.Code;
            existing.IsActive = lang.IsActive;
            existing.OrderIndex = lang.OrderIndex;
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        // DELETE /api/languages/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var lang = await _context.Languages.FindAsync(id);
            if (lang == null) return NotFound();
            var inUse = await _context.Audio.AnyAsync(a => a.LanguageId == id);
            if (inUse) return BadRequest("Ngôn ngữ này đang được dùng trong Audio, không thể xóa.");
            _context.Languages.Remove(lang);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}