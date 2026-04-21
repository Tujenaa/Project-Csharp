using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Data;
using TourGuideAPI.Models;
using BC = BCrypt.Net.BCrypt;

namespace TourGuideAPI.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        public UsersController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _context.Users.ToListAsync());

        [HttpGet("owners")]
        public async Task<IActionResult> GetOwners() =>
            Ok(await _context.Users.Where(u => u.Role == "OWNER").ToListAsync());

        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers() =>
            Ok(await _context.Users.Where(u => u.Role == "CUSTOMER").ToListAsync());

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _context.Users.FindAsync(id);
            return user == null ? NotFound() : Ok(user);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] User user)
        {
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
                user.PasswordHash = BC.HashPassword(user.PasswordHash);

            user.IsActive = true; // Tài khoản mới luôn active
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok(user);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] User user)
        {
            var existing = await _context.Users.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Username = user.Username;

            // Chỉ hash nếu PasswordHash được gửi lên khác với hash hiện tại và không phải là một chuỗi rỗng
            if (!string.IsNullOrWhiteSpace(user.PasswordHash) && user.PasswordHash != existing.PasswordHash)
            {
                // Kiểm tra xem có vẻ là hash BCrypt chưa (thường bắt đầu bằng $2)
                if (!user.PasswordHash.StartsWith("$2a$") && !user.PasswordHash.StartsWith("$2b$"))
                {
                    existing.PasswordHash = BC.HashPassword(user.PasswordHash);
                }
                else
                {
                    // Nếu nó đã là hash rồi thì cứ gán thẳng, không hash đè
                    existing.PasswordHash = user.PasswordHash;
                }
            }

            existing.Role = user.Role;
            existing.Name = user.Name;
            existing.Email = user.Email;
            existing.Phone = user.Phone;
            existing.IsActive = user.IsActive; // Cập nhật trạng thái active
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequest req)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Username == req.Username);

            // 1. Không tìm thấy user
            if (user == null)
                return NotFound("Tài khoản không tồn tại");

            // 2. Kiểm tra mật khẩu
            if (string.IsNullOrWhiteSpace(req.Password) || !BC.Verify(req.Password, user.PasswordHash))
                return Unauthorized("Mật khẩu không đúng");

            // 3. Kiểm tra trạng thái hoạt động
            if (!user.IsActive)
                return StatusCode(403, "Tài khoản của bạn đã bị vô hiệu hoá. Vui lòng liên hệ quản trị viên.");

            // Ghi log hoạt động
            _context.UserActivities.Add(new UserActivity
            {
                UserId = user.Id,
                Username = user.Name ?? user.Username,
                Role = user.Role,
                ActivityType = "LOGIN",
                Details = $"Người dùng {user.Username} đã đăng nhập hệ thống.",
                DeviceId = req.DeviceId,
                Timestamp = DateTime.Now
            });
            await _context.SaveChangesAsync();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Name,
                user.Email,
                user.Phone,
                user.Role,
                user.IsActive
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            if (_context.Users.Any(x => x.Username == user.Username))
                return BadRequest("Username đã tồn tại");

            if (string.IsNullOrWhiteSpace(user.Role))
                user.Role = "CUSTOMER";

            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
                user.PasswordHash = BC.HashPassword(user.PasswordHash);

            user.IsActive = true;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Name,
                user.Email,
                user.Phone,
                user.Role,
                user.IsActive
            });
        }

        [HttpPut("customer/{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] User updated)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.Name = updated.Name;
            user.Phone = updated.Phone;
            await _context.SaveChangesAsync();
            return Ok(user);
        }

        [HttpPut("change-password/{id}")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest req)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound("Người dùng không tồn tại");

            if (!BC.Verify(req.OldPassword, user.PasswordHash))
                return BadRequest("Mật khẩu cũ không chính xác");

            if (string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest("Mật khẩu mới không được để trống");

            user.PasswordHash = BC.HashPassword(req.NewPassword);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đổi mật khẩu thành công" });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] UserActivity log)
        {
            log.ActivityType = "LOGOUT";
            if (log.Timestamp == default) log.Timestamp = DateTime.Now;
            _context.UserActivities.Add(log);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}