using System.ComponentModel.DataAnnotations.Schema;
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
        [ForeignKey("PoiId")]
        public POI? POI { get; set; }
        [ForeignKey("LanguageId")]
        public Language? Language { get; set; }
    }
}