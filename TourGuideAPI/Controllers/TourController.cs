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
                .Where(tp => tp.PoiId == poiId && tp.Status == "APPROVED")  // ← thêm vào đây
                .Select(tp => tp.TourId)
                .ToListAsync();

            var tours = await _context.Tours
                .Where(t => tourIds.Contains(t.Id))
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

        // POST /api/tours/{id}/pois
        [HttpPost("{id}/pois")]
        public async Task<IActionResult> AddPoi(int id, [FromBody] AddPoiRequest req)
        {
            if (await _context.TourPOI.AnyAsync(tp => tp.TourId == id && tp.PoiId == req.PoiId
                && (tp.Status == "APPROVED" || tp.Status == "PENDING" || tp.Status == "REMOVE_PENDING")))
                return BadRequest("POI đã có hoặc đang chờ duyệt trong tour.");

            var isAdmin = Request.Headers.TryGetValue("X-Role", out var role) && role == "ADMIN";
            var maxOrder = await _context.TourPOI.Where(tp => tp.TourId == id)
                .Select(tp => (int?)tp.OrderIndex).MaxAsync() ?? 0;

            _context.TourPOI.Add(new TourPOI
            {
                TourId = id,
                PoiId = req.PoiId,
                OrderIndex = maxOrder + 1,
                Status = isAdmin ? "APPROVED" : "PENDING"
            });
            await _context.SaveChangesAsync();
            return Ok(new { status = isAdmin ? "APPROVED" : "PENDING" });
        }

        // PUT /api/tours/{id}/pois/{tpId}/approve
        [HttpPut("{id}/pois/{tpId}/approve")]
        public async Task<IActionResult> ApprovePoi(int id, int tpId)
        {
            var tp = await _context.TourPOI.FirstOrDefaultAsync(x => x.Id == tpId && x.TourId == id);
            if (tp == null) return NotFound();
            if (tp.Status == "REMOVE_PENDING")
            {
                // Admin đồng ý cho rời tour → xóa thật sự
                _context.TourPOI.Remove(tp);
            }
            else
            {
                tp.Status = "APPROVED";
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        // PUT /api/tours/{id}/pois/{tpId}/reject
        [HttpPut("{id}/pois/{tpId}/reject")]
        public async Task<IActionResult> RejectPoi(int id, int tpId)
        {
            var tp = await _context.TourPOI.FirstOrDefaultAsync(x => x.Id == tpId && x.TourId == id);
            if (tp == null) return NotFound();
            if (tp.Status == "REMOVE_PENDING")
            {
                // Admin từ chối cho rời tour → giữ nguyên APPROVED
                tp.Status = "APPROVED";
            }
            else
            {
                // Từ chối yêu cầu vào tour → xóa bản ghi
                _context.TourPOI.Remove(tp);
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        // GET /api/tours/pois/all-approved
        [HttpGet("pois/all-approved")]
        public async Task<IActionResult> GetAllApprovedPois()
        {
            var list = await _context.TourPOI.Where(tp => tp.Status == "APPROVED").ToListAsync();
            var result = new List<object>();
            foreach (var tp in list)
            {
                var poi = await _context.POI.FindAsync(tp.PoiId);
                var tour = await _context.Tours.FindAsync(tp.TourId);
                result.Add(new
                {
                    TourPOIId = tp.Id,
                    tp.TourId,
                    TourName = tour?.Name,
                    tp.PoiId,
                    PoiName = poi?.Name,
                    Address = poi?.Address,
                    tp.OrderIndex,
                    tp.Status
                });
            }
            return Ok(result);
        }

        // GET /api/tours/pois/all-rejected
        [HttpGet("pois/all-rejected")]
        public async Task<IActionResult> GetAllRejectedPois()
        {
            var list = await _context.TourPOI.Where(tp => tp.Status == "REJECTED").ToListAsync();
            var result = new List<object>();
            foreach (var tp in list)
            {
                var poi = await _context.POI.FindAsync(tp.PoiId);
                var tour = await _context.Tours.FindAsync(tp.TourId);
                result.Add(new
                {
                    TourPOIId = tp.Id,
                    tp.TourId,
                    TourName = tour?.Name,
                    tp.PoiId,
                    PoiName = poi?.Name,
                    Address = poi?.Address,
                    tp.OrderIndex,
                    tp.Status
                });
            }
            return Ok(result);
        }

        // DELETE /api/tours/pois/rejected — xóa tất cả TourPOI bị từ chối
        [HttpDelete("pois/rejected")]
        public async Task<IActionResult> DeleteAllRejectedTourPois()
        {
            var list = _context.TourPOI.Where(tp => tp.Status == "REJECTED");
            var count = await list.CountAsync();
            _context.TourPOI.RemoveRange(list);
            await _context.SaveChangesAsync();
            return Ok($"Đã xóa {count} yêu cầu bị từ chối.");
        }

        // GET /api/tours/pois/all-pending
        [HttpGet("pois/all-pending")]
        public async Task<IActionResult> GetAllPendingPois()
        {
            var list = await _context.TourPOI.Where(tp => tp.Status == "PENDING").ToListAsync();
            var result = new List<object>();
            foreach (var tp in list)
            {
                var poi = await _context.POI.FindAsync(tp.PoiId);
                var tour = await _context.Tours.FindAsync(tp.TourId);
                result.Add(new
                {
                    TourPOIId = tp.Id,
                    tp.TourId,
                    TourName = tour?.Name,
                    tp.PoiId,
                    PoiName = poi?.Name,
                    Address = poi?.Address,
                    tp.OrderIndex,
                    tp.Status
                });
            }
            return Ok(result);
        }

        // PUT /api/tours/{id}/pois/{poiId}/request-remove — owner xin rời tour (chờ admin duyệt)
        [HttpPut("{id}/pois/{poiId}/request-remove")]
        public async Task<IActionResult> RequestRemovePoi(int id, int poiId)
        {
            var isAdmin = Request.Headers.TryGetValue("X-Role", out var role) && role == "ADMIN";
            var tp = await _context.TourPOI.FirstOrDefaultAsync(x => x.TourId == id && x.PoiId == poiId && x.Status == "APPROVED");
            if (tp == null) return NotFound();
            if (isAdmin)
            {
                // Admin xóa thẳng, không cần duyệt
                _context.TourPOI.Remove(tp);
            }
            else
            {
                tp.Status = "REMOVE_PENDING";
            }
            await _context.SaveChangesAsync();
            return Ok(new { status = isAdmin ? "REMOVED" : "REMOVE_PENDING" });
        }

        // GET /api/tours/pois/all-remove-pending
        [HttpGet("pois/all-remove-pending")]
        public async Task<IActionResult> GetAllRemovePendingPois()
        {
            var list = await _context.TourPOI.Where(tp => tp.Status == "REMOVE_PENDING").ToListAsync();
            var result = new List<object>();
            foreach (var tp in list)
            {
                var poi = await _context.POI.FindAsync(tp.PoiId);
                var tour = await _context.Tours.FindAsync(tp.TourId);
                result.Add(new
                {
                    TourPOIId = tp.Id,
                    tp.TourId,
                    TourName = tour?.Name,
                    tp.PoiId,
                    PoiName = poi?.Name,
                    Address = poi?.Address,
                    tp.OrderIndex,
                    tp.Status
                });
            }
            return Ok(result);
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
            var tourPois = await _context.TourPOI
                .Where(tp => tp.TourId == tour.Id && (tp.Status == "APPROVED" || tp.Status == "REMOVE_PENDING"))
                .Join(_context.POI.Where(p => p.Status == "APPROVED"),
                    tp => tp.PoiId,
                    p => p.Id,
                    (tp, p) => new { POI = p, tp.Status, tp.OrderIndex })
                .OrderBy(x => x.OrderIndex)
                .ToListAsync();

            var resultPois = new List<POIDto>();
            foreach (var item in tourPois)
            {
                var p = item.POI;
                var audios = await _context.Audio
                    .Where(a => a.PoiId == p.Id)
                    .Include(a => a.Language)
                    .ToListAsync();

                resultPois.Add(new POIDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Address = p.Address,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Radius = p.Radius,
                    Status = p.Status,
                    TourRelationshipStatus = item.Status,
                    Audios = audios.Select(a => new AudioDto
                    {
                        Id = a.Id,
                        PoiId = a.PoiId,
                        LanguageId = a.LanguageId,
                        LanguageCode = a.Language?.Code,
                        Script = a.Script
                    }).ToList(),
                    Images = await _context.POIImages
                        .Where(img => img.PoiId == p.Id)
                        .Select(img => img.ImageUrl)
                        .ToListAsync()
                });
            }

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