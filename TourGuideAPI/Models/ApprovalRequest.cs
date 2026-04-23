using System;

namespace TourGuideAPI.Models
{
    public class ApprovalRequest
    {
        public int Id { get; set; }
        public int? EntityId { get; set; } // ID của đối tượng (POI Id, Tour Id, v.v.) - Null nếu là tạo mới
        public string EntityType { get; set; } = string.Empty; // "POI", "TOUR", "AUDIO"
        public string RequestType { get; set; } = string.Empty; // "CREATE", "UPDATE", "DELETE"
        public string Content { get; set; } = string.Empty; // JSON chứa nội dung mới
        public int RequesterId { get; set; }
        public string RequesterName { get; set; } = string.Empty;
        public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED
        public string? AdminNote { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
