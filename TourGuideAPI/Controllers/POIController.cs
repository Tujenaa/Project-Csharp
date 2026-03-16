using Microsoft.AspNetCore.Mvc;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

namespace TourGuideAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class POIController : ControllerBase
    {
        private readonly AppDbContext _context;

        public POIController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetPOI()
        {
            var pois = _context.POI.ToList();
            return Ok(pois);
        }
    }
}