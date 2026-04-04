namespace TourGuideApp.Models
{
    public class HistoryDto
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public string? PoiName { get; set; }
        public string? PoiImage { get; set; }
        public DateTime PlayTime { get; set; }
    }
}