using System.ComponentModel.DataAnnotations;

namespace TourGuideAPI.Models
{
    public class Language
    {
        [Key]
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty; // 'vi', 'en', etc.
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int OrderIndex { get; set; } = 0;
    }
}
