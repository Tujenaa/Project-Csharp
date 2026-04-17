using TourGuideApp.Pages;
using TourGuideApp.Services;

namespace TourGuideApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("placeDetail", typeof(PlaceDetailPage));

            // Đảm bảo heartbeat chạy ngay khi vào dashboard (đặc biệt sau khi Login)
            var heartbeat = Handler?.MauiContext?.Services.GetService<DeviceHeartbeatSender>() 
                           ?? Application.Current?.Handler?.MauiContext?.Services.GetService<DeviceHeartbeatSender>();
            
            heartbeat?.Start();
        }
    }
}
