using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

[ApiController]
[Route("api/[controller]")]
public class POIController : ControllerBase
{
    private readonly AppDbContext _context;

    public POIController(AppDbContext context)
    {
        _context = context;
    }

    //  API CHÍNH CHO APP
    [HttpGet]
    public async Task<IActionResult> GetPOI()
    {
        var data = await (
            from p in _context.POI
            join a in _context.Audio
                on p.Id equals a.PoiId into pa
            from a in pa.DefaultIfEmpty()
            select new POIDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Radius = p.Radius,
                AudioUrl = a != null ? a.AudioUrl : null,
                Script = a != null ? a.Script : null
            }
        ).ToListAsync();

        return Ok(data);
    }
    [HttpPost("history")]
    public async Task<IActionResult> SaveHistory([FromBody] HistoryRequest request)
    {
        if (request.PoiId <= 0)
            return BadRequest("poiId không hợp lệ");

        var exists = await _context.POI.AnyAsync(p => p.Id == request.PoiId);

        if (!exists)
            return BadRequest("POI không tồn tại");

        var history = new History
        {
            PoiId = request.PoiId,
            PlayTime = DateTime.Now
        };

        _context.History.Add(history);
        await _context.SaveChangesAsync();

        return Ok("Lưu thành công");
    }
}