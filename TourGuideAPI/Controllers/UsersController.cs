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
            // Băm mật khẩu trước khi lưu
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                user.PasswordHash = BC.HashPassword(user.PasswordHash);
            }

            // Admin được tạo cả 3 role: ADMIN, OWNER, CUSTOMER
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
            
            // Nếu có password mới thì băm nó, nếu không thì giữ nguyên password cũ
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                existing.PasswordHash = BC.HashPassword(user.PasswordHash);
            }
            
            existing.Role = user.Role;
            existing.Name = user.Name;
            existing.Email = user.Email;
            existing.Phone = user.Phone;
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


        // ── APP: GET /api/users/login ── Đăng nhập (chỉ CUSTOMER)
        [HttpPost("login")]
        public IActionResult Login([FromBody] UserLoginRequest req)
        {
            var user = _context.Users
                .FirstOrDefault(x => x.Username == req.Username);

            if (user == null)
                return Unauthorized("Sai username");

            if (!BC.Verify(req.Password, user.PasswordHash))
                return Unauthorized("Sai mật khẩu");

            return Ok(new
            {
                user.Id,
                user.Username, // thêm dòng này
                user.Name,
                user.Email,
                user.Phone,
                user.Role
            });
        }

        // ── APP: POST /api/users/register ── Đăng ký (tự động CUSTOMER)
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            // check username tồn tại
            if (_context.Users.Any(x => x.Username == user.Username))
                return BadRequest("Username đã tồn tại");
            // Chỉ gán CUSTOMER nếu yêu cầu từ thiết bị không gửi kèm Role (như trên App)
            if (string.IsNullOrWhiteSpace(user.Role))
                user.Role = "CUSTOMER";

            // Băm mật khẩu trước khi lưu
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                user.PasswordHash = BC.HashPassword(user.PasswordHash);
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                user.Id,
                user.Username, // thêm dòng này
                user.Name,
                user.Email,
                user.Phone,
                user.Role
            });
        }

        // ── APP: PUT /api/users/{id} ── Cập nhật thông tin (chỉ CUSTOMER)
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

        // ── APP: PUT /api/users/change-password/{id} ── Đổi mật khẩu
        [HttpPut("change-password/{id}")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest req)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound("Người dùng không tồn tại");

            // Kiểm tra mật khẩu cũ bằng BCrypt
            if (!BC.Verify(req.OldPassword, user.PasswordHash))
                return BadRequest("Mật khẩu cũ không chính xác");

            if (string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest("Mật khẩu mới không được để trống");

            // Băm mật khẩu mới
            user.PasswordHash = BC.HashPassword(req.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công" });
        }
    }
}