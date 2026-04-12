namespace TourGuideAPI.Models
{
    public class Tour
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string Status { get; set; } = "PUBLISHED";
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class TourPOI
    {
        public int Id { get; set; }
        public int TourId { get; set; }
        public int PoiId { get; set; }
        public int OrderIndex { get; set; }
        public string Status { get; set; } = "APPROVED";
    }

    public class TourDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Status { get; set; }
        public List<POIDto> POIs { get; set; } = new();
    }
}
