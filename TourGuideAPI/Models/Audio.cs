using System.Text.Json.Serialization;
namespace TourGuideAPI.Models
{
    public class Audio
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public string? vi { get; set; }
        public string? en { get; set; }
        public string? ja { get; set; }
        public string? zh { get; set; }
        [JsonIgnore]
        public POI? POI { get; set; }
    }
}