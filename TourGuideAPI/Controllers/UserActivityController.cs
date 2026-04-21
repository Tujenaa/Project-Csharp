using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

namespace TourGuideAPI.Controllers
{
    [Route("api/user-activity")]
    [ApiController]
    public class UserActivityController : ControllerBase
    {
        private readonly AppDbContext _context;
        public UserActivityController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.UserActivities
                .OrderByDescending(a => a.Timestamp)
                .Take(500) // Giới hạn 500 bản ghi mới nhất
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Log([FromBody] UserActivity activity)
        {
            if (activity.Timestamp == default) activity.Timestamp = DateTime.Now;
            
            _context.UserActivities.Add(activity);
            await _context.SaveChangesAsync();
            return Ok(activity);
        }

        [HttpDelete]
        public async Task<IActionResult> ClearAll()
        {
            // Chỉ xóa các bản ghi cũ (lịch sử), giữ lại các bản ghi trong 10 phút gần đây
            // để bảo vệ danh sách người dùng đang trực tuyến.
            var cutoff = DateTime.Now.AddMinutes(-10);
            var oldActivities = _context.UserActivities.Where(a => a.Timestamp < cutoff);
            
            _context.UserActivities.RemoveRange(oldActivities);
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Đã xóa lịch sử hoạt động (giữ lại 10 phút gần nhất)" });
        }
    }
}
