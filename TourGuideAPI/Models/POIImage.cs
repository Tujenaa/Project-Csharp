using System;
using System.Text.Json.Serialization;

namespace TourGuideAPI.Models
{
    public class POIImage
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsThumbnail { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public POI? POI { get; set; }
    }
}
