namespace TourGuideAPI.Models
{
    public class History
    {
        public int Id { get; set; }
        public int PoiId { get; set; }

        public DateTime PlayTime { get; set; }
        public POI? POI { get; set; }
    }
}