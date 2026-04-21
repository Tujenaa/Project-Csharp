namespace TourGuideAPI.Models
{
    public class UserLoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? DeviceId { get; set; }
    }
}
