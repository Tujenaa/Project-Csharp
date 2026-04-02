using System.ComponentModel.DataAnnotations.Schema;

namespace TourGuideAPI.Models
{
    public class POI
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Radius { get; set; }
        public int? OwnerId { get; set; }
        [NotMapped]
        public string? OwnerName { get; set; }
        // ── MỚI ──
        [NotMapped]
        public string? ImageUrl { get; set; }
        public string Status { get; set; } = "APPROVED";
        public string? RejectReason { get; set; }
    }
}