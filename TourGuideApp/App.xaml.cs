using System;
using TourGuideApp.Models;
using TourGuideApp.Pages;
using TourGuideApp.Services;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Networking;

namespace TourGuideApp
{
    public partial class App : Application
    {
        private readonly DeviceHeartbeatSender _heartbeat;

        public App(DeviceHeartbeatSender heartbeat)
        {
            _heartbeat = heartbeat;

            InitializeComponent();

            if (AuthService.IsLoggedIn)
            {
                _ = TourGuideApp.ViewModels.HistoryStore.LoadFromApiAsync();

                // Sync history offline queue khi có mạng
                if (ConnectivityService.IsConnected)
                {
                    int userId = Preferences.Get("user_id", 0);
                    if (userId > 0)
                        _ = new ApiService().SyncPendingHistoriesAsync(userId);
                }

                // Bắt đầu gửi vị trí GPS lên server (chỉ khi đã đăng nhập)
                _heartbeat.Start();
            }

            MainPage = AuthService.IsLoggedIn
                ? (Page)new AppShell()
                : new NavigationPage(new LoginPage());

            // Ghi nhật ký mở App
            if (ConnectivityService.IsConnected)
            {
                var api = new ApiService();
                var userId = Preferences.Get("user_id", 0);
                var username = Preferences.Get("user_username", "guest");
                var role = username == "guest" ? "GUEST" : "CUSTOMER";
                _ = api.LogActivity(userId, username, role, "APP_OPEN", $"Người dùng đã mở ứng dụng trên thiết bị {DeviceInfo.Current.Model}");
            }

            // Lắng nghe thay đổi kết nối mạng
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        }

        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            bool isOnline = e.NetworkAccess == NetworkAccess.Internet;

            System.Diagnostics.Debug.WriteLine(
                $"[App] Network changed → {(isOnline ? "ONLINE" : "OFFLINE")}");

            if (isOnline)
            {
                // Khi có mạng trở lại: sync history queue
                int userId = Preferences.Get("user_id", 0);
                if (userId > 0)
                    _ = new ApiService().SyncPendingHistoriesAsync(userId);
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            // Reset flag để khi vào lại app (từ background) vẫn hỏi lại lân cận
            var mapVm = Handler?.MauiContext?.Services.GetService<TourGuideApp.ViewModels.MapViewModel>();
            mapVm?.ResetStartupFlag();

            // Tiếp tục gửi heartbeat khi app trở lại foreground
            if (AuthService.IsLoggedIn)
            {
                System.Diagnostics.Debug.WriteLine("[App] Resuming... starting heartbeat");
                _heartbeat.Start();
            }
        }

        protected override void OnSleep()
        {
            base.OnSleep();

            // Dừng gửi heartbeat khi app vào background
            System.Diagnostics.Debug.WriteLine("[App] Sleeping... stopping heartbeat");
            _heartbeat.Stop();

            // Thông báo ngay cho server là tôi offline
            _ = _heartbeat.NotifyOfflineAsync();
        }

        protected override void OnAppLinkRequestReceived(Uri uri)
        {
            base.OnAppLinkRequestReceived(uri);

            // Nếu link là guest và chưa đăng nhập thì mới xử lý
            bool isGuestLink = string.Equals(uri.Scheme, "tourguideapp", StringComparison.OrdinalIgnoreCase) && 
                               string.Equals(uri.Host, "guest", StringComparison.OrdinalIgnoreCase);

            if (isGuestLink && !AuthService.IsLoggedIn)
            {
                AuthService.LoginOfflineAsGuest();
                MainPage = new AppShell();
                return;
            }

            // 2. Kiểm tra link tourguideapp://poi/{id}
            if (string.Equals(uri.Scheme, "tourguideapp", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(uri.Host, "poi", StringComparison.OrdinalIgnoreCase))
            {
                // Parse ID từ path hoặc host
                // Link có dạng: tourguideapp://poi/15
                string lastSegment = uri.Segments.LastOrDefault()?.Trim('/') ?? "";
                if (int.TryParse(lastSegment, out int poiId))
                {
                    if (!AuthService.IsLoggedIn)
                        AuthService.LoginOfflineAsGuest();

                    // Chuyển sang màn hình bản đồ và báo hiệu cần focus vào POI này
                    MapTourState.FocusPoiId = poiId;
                    MainPage = new AppShell();

                    // Tự động phát audio sau khi app đã ổn định một chút
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000); // Chờ cho API nạp xong POI
                        var poi = await new ApiService().GetPOIById(poiId);
                        if (poi != null)
                        {
                            await AudioPlaybackService.Instance.PlayAsync(poi);
                        }
                    });
                }
            }
        }

        protected override void CleanUp()
        {
            Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
            _heartbeat.Stop();
            if (AuthService.IsLoggedIn)
                _ = _heartbeat.NotifyOfflineAsync();
            base.CleanUp();
        }
    }
}