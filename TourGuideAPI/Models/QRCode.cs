using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourGuideAPI.Models
{
    public class QRCode
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? PoiId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        [ForeignKey("PoiId")]
        public POI? POI { get; set; }
    }

    public class QRCodeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? PoiId { get; set; }
        public string? PoiName { get; set; }
        public string Content { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
