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

        // GET /api/tours — tất cả tour PUBLISHED kèm danh sách POI
        [HttpGet]
        public async Task<IActionResult> GetTours()
        {
            var tours = await _context.Tours
                .Where(t => t.Status == "PUBLISHED")
                .ToListAsync();

            var result = new List<TourDto>();

            foreach (var tour in tours)
            {
                var tourPois = await (
                    from tp in _context.TourPOI
                    join p in _context.POI on tp.PoiId equals p.Id
                    where tp.TourId == tour.Id && p.Status == "APPROVED"
                    orderby tp.OrderIndex
                    join a in _context.Audio on p.Id equals a.PoiId into pa
                    from a in pa.DefaultIfEmpty()
                    select new POIDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        Address = p.Address,
                        Latitude = p.Latitude,
                        Longitude = p.Longitude,
                        Radius = p.Radius,
                        ScriptVi = a != null ? a.vi : null,
                        ScriptEn = a != null ? a.en : null,
                        Images = _context.POIImages
                            .Where(img => img.PoiId == p.Id)
                            .Select(img => img.ImageUrl)
                            .ToList()
                    }
                ).ToListAsync();

                result.Add(new TourDto
                {
                    Id = tour.Id,
                    Name = tour.Name,
                    Description = tour.Description,
                    ThumbnailUrl = tour.ThumbnailUrl,
                    Status = tour.Status,
                    POIs = tourPois
                });
            }

            return Ok(result);
        }

        // GET /api/tours/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTourById(int id)
        {
            var tour = await _context.Tours.FindAsync(id);
            if (tour == null) return NotFound();

            var tourPois = await (
                from tp in _context.TourPOI
                join p in _context.POI on tp.PoiId equals p.Id
                where tp.TourId == id && p.Status == "APPROVED"
                orderby tp.OrderIndex
                join a in _context.Audio on p.Id equals a.PoiId into pa
                from a in pa.DefaultIfEmpty()
                select new POIDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Address = p.Address,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Radius = p.Radius,
                    ScriptVi = a != null ? a.vi : null,
                    ScriptEn = a != null ? a.en : null,
                    Images = _context.POIImages
                        .Where(img => img.PoiId == p.Id)
                        .Select(img => img.ImageUrl)
                        .ToList()
                }
            ).ToListAsync();

            return Ok(new TourDto
            {
                Id = tour.Id,
                Name = tour.Name,
                Description = tour.Description,
                ThumbnailUrl = tour.ThumbnailUrl,
                Status = tour.Status,
                POIs = tourPois
            });
        }
    }
}
