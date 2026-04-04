namespace TourGuideAPI.Models
{
    public class POIImages
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public string ImageUrl { get; set; } = "";
        public bool IsThumbnail { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}