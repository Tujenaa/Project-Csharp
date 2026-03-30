using TourGuideApp.Models;

namespace TourGuideApp.Services
{
    // Lưu thông tin người dùng hiện tại trong session.
    public static class SessionService
    {
        public static UserDto? CurrentUser { get; set; }
    }
}