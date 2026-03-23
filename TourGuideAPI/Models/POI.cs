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
        public int? OwnerId { get; set; }  // nullable — web cần, app bỏ qua null
        public string? OwnerName { get; set; }

    
    }
}