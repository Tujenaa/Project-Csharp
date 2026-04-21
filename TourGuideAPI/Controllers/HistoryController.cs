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
            var list = await (
                from h in _context.History
                join p in _context.POI on h.PoiId equals p.Id into hp
                from p in hp.DefaultIfEmpty()
                join u in _context.Users on h.UserId equals u.Id into hu
                from u in hu.DefaultIfEmpty()
                orderby h.PlayTime descending
                select new
                {
                    h.Id,
                    h.PoiId,
                    PoiName = p != null ? p.Name : null,
                    h.UserId,
                    UserLogin = u != null ? u.Username : "guest",
                    UserFullName = u != null ? u.Name : "Khách",
                    UserRole = u != null ? u.Role : null,
                    h.PlayTime,
                    h.ListenDuration
                }
            ).ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] History history)
        {
            Console.WriteLine($"API RECEIVED: PoiId={history.PoiId}, UserId={history.UserId}");
            
            // Xử lý khách (UserId = 0 hoặc null)
            if (history.UserId == 0) history.UserId = null;

            if (history.UserId != null)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == history.UserId);
                if (user == null) return BadRequest("User không tồn tại");
                if (user.Role != "CUSTOMER") return BadRequest("Chỉ CUSTOMER mới được ghi lịch sử");
            }

            history.POI = null;
            if (history.PlayTime == default) history.PlayTime = DateTime.Now;
            _context.History.Add(history);
            await _context.SaveChangesAsync();

            // Ghi log hoạt động
            var poi = await _context.POI.FindAsync(history.PoiId);
            var userObj = history.UserId.HasValue ? await _context.Users.FindAsync(history.UserId.Value) : null;
            _context.UserActivities.Add(new UserActivity
            {
                UserId = history.UserId,
                Username = userObj?.Name ?? "Khách",
                Role = userObj?.Role ?? "GUEST",
                ActivityType = "LISTEN",
                Details = $"Đã nghe thuyết minh tại điểm: {poi?.Name ?? "Không xác định"}",
                DeviceId = history.DeviceId,
                Timestamp = DateTime.Now
            });
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
                    h.PlayTime,
                    h.ListenDuration
                })
                .ToListAsync();
            return Ok(list);
        }
    }
}