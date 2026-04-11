namespace TourGuideAPI.Models
{
    public class AudioDto
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public int LanguageId { get; set; }
        public string? LanguageCode { get; set; }
        public string? Script { get; set; }
    }
}
