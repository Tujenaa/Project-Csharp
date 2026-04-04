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
                    PoiImage = h.POI != null ? h.POI.ImageUrl : null,
                    h.PlayTime
                })
                .ToListAsync();
            return Ok(list);
        }
        // // ── APP: POST api/history ──
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] History history)
        {
            Console.WriteLine($"API RECEIVED: PoiId={history.PoiId}, UserId={history.UserId}");

            if (history.UserId == 0)
            {
                return BadRequest("UserId bị null / không gửi lên");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == history.UserId);

            if (user == null)
            {
                return BadRequest("User không tồn tại");
            }

            if (user.Role != "CUSTOMER")
            {
                return BadRequest("Chỉ CUSTOMER mới được ghi lịch sử");
            }

            // Reset navigation tránh loop
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

        //APP: Lấy lịch sử nghe của một user, kèm tên POI 
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            var list = await _context.History
                .Where(h => h.UserId == userId)
                .Include(h => h.POI)
                .OrderByDescending(h => h.PlayTime)
                .Select(h => new {
                    h.Id,
                    h.PoiId,
                    PoiName = h.POI != null ? h.POI.Name : null,
                    PoiImage = h.POI != null ? h.POI.ImageUrl : null,
                    h.PlayTime
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}