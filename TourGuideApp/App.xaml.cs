using System;
using TourGuideApp.Pages;
using TourGuideApp.Services;

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
            if (AuthService.IsLoggedIn)
                _ = _heartbeat.NotifyOfflineAsync();
        }

        protected override void OnAppLinkRequestReceived(Uri uri)
        {
            base.OnAppLinkRequestReceived(uri);

            // Nếu đã đăng nhập thì không làm gì cả
            if (AuthService.IsLoggedIn)
                return;

            // Kiểm tra link tourguideapp://guest
            if (string.Equals(uri.Scheme, "tourguideapp", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(uri.Host, "guest", StringComparison.OrdinalIgnoreCase))
            {
                AuthService.LoginOfflineAsGuest();
                MainPage = new AppShell();
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