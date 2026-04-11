using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

namespace TourGuideAPI.Controllers
{
    [ApiController]
    [Route("api/tours")]
    public class ToursController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ToursController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/tours — tất cả tour (kèm POI)
        [HttpGet]
        public async Task<IActionResult> GetTours()
        {
            var tours = await _context.Tours.OrderByDescending(t => t.CreatedAt).ToListAsync();
            var result = await BuildTourDtos(tours);
            return Ok(result);
        }

        // GET /api/tours/published — chỉ PUBLISHED (dành cho App)
        [HttpGet("published")]
        public async Task<IActionResult> GetPublished()
        {
            var tours = await _context.Tours
                .Where(t => t.Status == "PUBLISHED")
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            var result = await BuildTourDtos(tours);
            return Ok(result);
        }

        // GET /api/tours/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTourById(int id)
        {
            var tour = await _context.Tours.FindAsync(id);
            if (tour == null) return NotFound();
            var dto = await BuildTourDto(tour);
            return Ok(dto);
        }

        // GET /api/tours/by-poi/{poiId} — các tour chứa POI này
        [HttpGet("by-poi/{poiId}")]
        public async Task<IActionResult> GetToursByPoi(int poiId)
        {
            var tourIds = await _context.TourPOI
                .Where(tp => tp.PoiId == poiId)
                .Select(tp => tp.TourId)
                .ToListAsync();

            var tours = await _context.Tours
                .Where(t => tourIds.Contains(t.Id) && t.Status == "PUBLISHED")
                .ToListAsync();

            var result = await BuildTourDtos(tours);
            return Ok(result);
        }

        // POST /api/tours
        [HttpPost]
        public async Task<IActionResult> CreateTour([FromBody] TourCreateRequest req)
        {
            var tour = new Tour
            {
                Name = req.Name,
                Description = req.Description,
                ThumbnailUrl = req.ThumbnailUrl,
                Status = req.Status ?? "PUBLISHED",
                CreatedBy = req.CreatedBy,
                CreatedAt = DateTime.Now
            };
            _context.Tours.Add(tour);
            await _context.SaveChangesAsync();

            // Thêm POI vào tour
            if (req.PoiIds != null)
            {
                for (int i = 0; i < req.PoiIds.Count; i++)
                {
                    _context.TourPOI.Add(new TourPOI
                    {
                        TourId = tour.Id,
                        PoiId = req.PoiIds[i],
                        OrderIndex = i + 1
                    });
                }
                await _context.SaveChangesAsync();
            }

            return Ok(tour);
        }

        // PUT /api/tours/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTour(int id, [FromBody] TourCreateRequest req)
        {
            var tour = await _context.Tours.FindAsync(id);
            if (tour == null) return NotFound();

            tour.Name = req.Name ?? tour.Name;
            tour.Description = req.Description ?? tour.Description;
            tour.ThumbnailUrl = req.ThumbnailUrl ?? tour.ThumbnailUrl;
            tour.Status = req.Status ?? tour.Status;

            // Cập nhật danh sách POI
            if (req.PoiIds != null)
            {
                var existing = _context.TourPOI.Where(tp => tp.TourId == id);
                _context.TourPOI.RemoveRange(existing);
                for (int i = 0; i < req.PoiIds.Count; i++)
                {
                    _context.TourPOI.Add(new TourPOI
                    {
                        TourId = id,
                        PoiId = req.PoiIds[i],
                        OrderIndex = i + 1
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(tour);
        }

        // DELETE /api/tours/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTour(int id)
        {
            var tour = await _context.Tours.FindAsync(id);
            if (tour == null) return NotFound();
            var pois = _context.TourPOI.Where(tp => tp.TourId == id);
            _context.TourPOI.RemoveRange(pois);
            _context.Tours.Remove(tour);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // POST /api/tours/{id}/pois — thêm POI vào tour
        [HttpPost("{id}/pois")]
        public async Task<IActionResult> AddPoi(int id, [FromBody] AddPoiRequest req)
        {
            if (await _context.TourPOI.AnyAsync(tp => tp.TourId == id && tp.PoiId == req.PoiId))
                return BadRequest("POI đã có trong tour.");
            var maxOrder = await _context.TourPOI
                .Where(tp => tp.TourId == id)
                .Select(tp => (int?)tp.OrderIndex)
                .MaxAsync() ?? 0;
            _context.TourPOI.Add(new TourPOI { TourId = id, PoiId = req.PoiId, OrderIndex = maxOrder + 1 });
            await _context.SaveChangesAsync();
            return Ok();
        }

        // DELETE /api/tours/{id}/pois/{poiId} — xóa POI khỏi tour
        [HttpDelete("{id}/pois/{poiId}")]
        public async Task<IActionResult> RemovePoi(int id, int poiId)
        {
            var tp = await _context.TourPOI.FirstOrDefaultAsync(x => x.TourId == id && x.PoiId == poiId);
            if (tp == null) return NotFound();
            _context.TourPOI.Remove(tp);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<List<TourDto>> BuildTourDtos(List<Tour> tours)
        {
            var result = new List<TourDto>();
            foreach (var t in tours)
                result.Add(await BuildTourDto(t));
            return result;
        }

        private async Task<TourDto> BuildTourDto(Tour tour)
        {
            var poiList = await _context.TourPOI
                .Where(tp => tp.TourId == tour.Id)
                .OrderBy(tp => tp.OrderIndex)
                .Select(tp => tp.PoiId)
                .ToListAsync();

            var pois = await _context.POI
                .Where(p => poiList.Contains(p.Id) && p.Status == "APPROVED")
                .Include(p => p.Audios)
                .ThenInclude(a => a.Language)
                .ToListAsync();

            // Sắp xếp lại theo đúng OrderIndex của TourPOI
            var resultPois = poiList
                .Select(id => pois.FirstOrDefault(p => p.Id == id))
                .Where(p => p != null)
                .Select(p => new POIDto
                {
                    Id = p!.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Address = p.Address,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Radius = p.Radius,
                    Audios = p.Audios.Select(a => new AudioDto
                    {
                        Id = a.Id,
                        PoiId = a.PoiId,
                        LanguageId = a.LanguageId,
                        LanguageCode = a.Language?.Code,
                        Script = a.Script
                    }).ToList(),
                    Images = _context.POIImages
                        .Where(img => img.PoiId == p.Id)
                        .Select(img => img.ImageUrl)
                        .ToList()
                }).ToList();

            return new TourDto
            {
                Id = tour.Id,
                Name = tour.Name,
                Description = tour.Description,
                ThumbnailUrl = tour.ThumbnailUrl,
                Status = tour.Status,
                POIs = resultPois
            };
        }
    }

    public class TourCreateRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Status { get; set; }
        public int CreatedBy { get; set; } = 1;
        public List<int>? PoiIds { get; set; }
    }

    public class AddPoiRequest
    {
        public int PoiId { get; set; }
    }
}