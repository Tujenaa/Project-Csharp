using System.Text.Json.Serialization;
namespace TourGuideAPI.Models
{
    public class AudioExtra
    {
        public int Id { get; set; }
        public int AudioId { get; set; }
        public string LangCode { get; set; } = "";   // vd: ko, fr, de
        public string LangName { get; set; } = "";   // vd: 한국어, Français
        public string? Script { get; set; }

        [JsonIgnore]
        public Audio? Audio { get; set; }
    }
}