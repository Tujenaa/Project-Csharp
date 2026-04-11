using System.Text.Json.Serialization;
namespace TourGuideAPI.Models
{
    public class Audio
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public int LanguageId { get; set; }
        public string Script { get; set; } = string.Empty;

        [JsonIgnore]
        public POI? POI { get; set; }

        [JsonIgnore]
        public Language? Language { get; set; }
    }
}