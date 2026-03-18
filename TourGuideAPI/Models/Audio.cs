namespace TourGuideAPI.Models
{
    public class Audio
    {
        public int Id { get; set; }
        public int PoiId { get; set; }

        public string? AudioUrl { get; set; }
        public string? Script { get; set; }

        public string? Language { get; set; }

        public POI? POI { get; set; }
    }
}