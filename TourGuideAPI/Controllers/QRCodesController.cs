using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

namespace TourGuideAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QRCodesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public QRCodesController(AppDbContext context)
        {
            _context = context;
        }

        private bool IsAdmin => Request.Headers["X-Role"] == "ADMIN";
        private int MyUserId => int.TryParse(Request.Headers["X-UserId"], out var id) ? id : 0;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<QRCodeDto>>> GetQRCodes()
        {
            var query = _context.QRCodes
                .Include(q => q.POI)
                .AsQueryable();

            if (!IsAdmin)
            {
                query = query.Where(q => q.OwnerId == MyUserId);
            }

            var list = await query.OrderByDescending(q => q.CreatedAt).ToListAsync();
            
            var result = list.Select(q => new QRCodeDto
            {
                Id = q.Id,
                Name = q.Name,
                PoiId = q.PoiId,
                PoiName = q.POI?.Name,
                Content = q.Content,
                OwnerId = q.OwnerId,
                CreatedAt = q.CreatedAt
            }).ToList();

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<QRCode>> CreateQRCode(QRCode qr)
        {
            qr.OwnerId = MyUserId;
            qr.CreatedAt = DateTime.Now;
            
            _context.QRCodes.Add(qr);
            await _context.SaveChangesAsync();

            return Ok(qr);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQRCode(int id, QRCode qr)
        {
            var existing = await _context.QRCodes.FindAsync(id);
            if (existing == null) return NotFound();

            if (!IsAdmin && existing.OwnerId != MyUserId)
            {
                return Forbid();
            }

            existing.Name = qr.Name;
            existing.PoiId = qr.PoiId;
            existing.Content = qr.Content;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQRCode(int id)
        {
            var qr = await _context.QRCodes.FindAsync(id);
            if (qr == null) return NotFound();

            if (!IsAdmin && qr.OwnerId != MyUserId)
            {
                return Forbid();
            }

            _context.QRCodes.Remove(qr);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
