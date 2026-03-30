using System.Text.Json.Serialization;

namespace TourGuideAPI.Models
{
    public class History
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public int UserId { get; set; }
        public DateTime PlayTime { get; set; }

        [JsonIgnore] // tránh FK conflict khi POST
        public POI? POI { get; set; }
    }
}