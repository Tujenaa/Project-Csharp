namespace TourGuideAPI.Models
{
    public class POIDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Radius { get; set; }
        public string? Status { get; set; }
        public string? TourRelationshipStatus { get; set; }
        public List<AudioDto> Audios { get; set; } = new();
        public List<string> Images { get; set; } = new();
    }
}