using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

namespace TourGuideAPI.Controllers
{
    [Route("api/history")]
    [ApiController]
    public class HistoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        public HistoryController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.History
                .Include(h => h.POI)
                .Select(h => new {
                    h.Id,
                    h.PoiId,
                    PoiName = h.POI != null ? h.POI.Name : null,
                    h.PlayTime
                })
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] History history)
        {
            history.POI = null;
            if (history.PlayTime == default)
                history.PlayTime = DateTime.Now;
            _context.History.Add(history);
            await _context.SaveChangesAsync();
            return Ok(history);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var h = await _context.History.FindAsync(id);
            if (h == null) return NotFound();
            _context.History.Remove(h);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}