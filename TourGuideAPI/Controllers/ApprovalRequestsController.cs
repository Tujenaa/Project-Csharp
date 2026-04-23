using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TourGuideAPI.Data;
using TourGuideAPI.Models;

namespace TourGuideAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApprovalRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ApprovalRequestsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/ApprovalRequests
        [HttpGet]
        public async Task<IActionResult> GetPendingRequests()
        {
            var requests = await _context.ApprovalRequests
                .Where(r => r.Status == "PENDING")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return Ok(requests);
        }

        // GET: api/ApprovalRequests/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRequest(int id)
        {
            var request = await _context.ApprovalRequests.FindAsync(id);
            if (request == null) return NotFound();
            return Ok(request);
        }

        // PUT: api/ApprovalRequests/{id}/approve
        [HttpPut("{id}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.ApprovalRequests.FindAsync(id);
            if (request == null) return NotFound();
            if (request.Status != "PENDING") return BadRequest("Yêu cầu này已 được xử lý.");

            try
            {
                if (request.EntityType == "POI")
                {
                    var poiData = JsonSerializer.Deserialize<POI>(request.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (poiData == null) return BadRequest("Dữ liệu yêu cầu không hợp lệ.");

                    if (request.RequestType == "CREATE")
                    {
                        poiData.Id = 0; // Đảm bảo tạo mới
                        poiData.Status = "APPROVED";
                        _context.POI.Add(poiData);
                    }
                    else if (request.RequestType == "UPDATE")
                    {
                        var existing = await _context.POI.FindAsync(request.EntityId);
                        if (existing == null) return NotFound("Không tìm thấy POI gốc để cập nhật.");

                        existing.Name = poiData.Name;
                        existing.Description = poiData.Description;
                        existing.Address = poiData.Address;
                        existing.Phone = poiData.Phone;
                        existing.Latitude = poiData.Latitude;
                        existing.Longitude = poiData.Longitude;
                        existing.Radius = poiData.Radius;
                        existing.ImageUrl = poiData.ImageUrl ?? existing.ImageUrl;
                        existing.Status = "APPROVED";
                        existing.RejectReason = null;

                        // Đồng bộ ảnh mới (nếu có)
                        if (poiData.Images != null && poiData.Images.Count > 0)
                        {
                            foreach (var img in poiData.Images)
                            {
                                img.PoiId = existing.Id;
                                img.Id = 0; // Đảm bảo chèn mới
                                _context.POIImages.Add(img);
                            }
                        }
                    }
                }
                else if (request.EntityType == "TOUR")
                {
                    var tourData = JsonSerializer.Deserialize<TourCreateRequest>(request.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (tourData == null) return BadRequest("Dữ liệu yêu cầu không hợp lệ.");

                    if (request.RequestType == "CREATE")
                    {
                        var newTour = new Tour
                        {
                            Name = tourData.Name,
                            Description = tourData.Description,
                            ThumbnailUrl = tourData.ThumbnailUrl,
                            Status = "PUBLISHED",
                            CreatedBy = tourData.CreatedBy,
                            CreatedAt = DateTime.Now
                        };
                        _context.Tours.Add(newTour);
                        await _context.SaveChangesAsync();

                        if (tourData.PoiIds != null)
                        {
                            for (int i = 0; i < tourData.PoiIds.Count; i++)
                            {
                                _context.TourPOI.Add(new TourPOI
                                {
                                    TourId = newTour.Id,
                                    PoiId = tourData.PoiIds[i],
                                    OrderIndex = i + 1,
                                    Status = "APPROVED"
                                });
                            }
                        }
                    }
                    else if (request.RequestType == "UPDATE")
                    {
                        var existing = await _context.Tours.FindAsync(request.EntityId);
                        if (existing == null) return NotFound("Không tìm thấy Tour gốc để cập nhật.");

                        existing.Name = tourData.Name ?? existing.Name;
                        existing.Description = tourData.Description ?? existing.Description;
                        existing.ThumbnailUrl = tourData.ThumbnailUrl ?? existing.ThumbnailUrl;

                        if (tourData.PoiIds != null)
                        {
                            var oldPois = _context.TourPOI.Where(tp => tp.TourId == existing.Id);
                            _context.TourPOI.RemoveRange(oldPois);
                            for (int i = 0; i < tourData.PoiIds.Count; i++)
                            {
                                _context.TourPOI.Add(new TourPOI
                                {
                                    TourId = existing.Id,
                                    PoiId = tourData.PoiIds[i],
                                    OrderIndex = i + 1,
                                    Status = "APPROVED"
                                });
                            }
                        }
                    }
                }

                request.Status = "APPROVED";
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã phê duyệt yêu cầu thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest($"Lỗi khi phê duyệt: {ex.Message}");
            }
        }

        // PUT: api/ApprovalRequests/{id}/reject
        [HttpPut("{id}/reject")]
        public async Task<IActionResult> Reject(int id, [FromBody] RejectFeedback feedback)
        {
            var request = await _context.ApprovalRequests.FindAsync(id);
            if (request == null) return NotFound();
            if (request.Status != "PENDING") return BadRequest("Yêu cầu này đã được xử lý.");

            request.Status = "REJECTED";
            request.AdminNote = feedback.Reason;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã từ chối yêu cầu." });
        }

        // DELETE: api/ApprovalRequests/{id}
        // Owner hủy yêu cầu đang chờ duyệt (POI ảo có Id âm ở CMS)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var request = await _context.ApprovalRequests.FindAsync(id);
            if (request == null) return NotFound();
            if (request.Status != "PENDING") return BadRequest("Chỉ có thể hủy yêu cầu đang chờ duyệt.");
            _context.ApprovalRequests.Remove(request);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã hủy yêu cầu thành công." });
        }

        public record RejectFeedback(string Reason);
    }
}