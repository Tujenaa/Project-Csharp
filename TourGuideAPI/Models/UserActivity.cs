using System;

namespace TourGuideAPI.Models
{
    public class UserActivity
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string Username { get; set; } = "Khách";
        public string Role { get; set; } = "GUEST";
        public string ActivityType { get; set; } = string.Empty; // LOGIN, LOGOUT, LISTEN, APP_OPEN, etc.
        public string Details { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
