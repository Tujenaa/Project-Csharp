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

            if (AuthService.Username == "guest" || Preferences.Get("is_first_run_complete", false))
            {
                // Sync history offline queue khi có mạng
                if (ConnectivityService.IsConnected)
                {
                    int userId = Preferences.Get("user_id", 0);
                    if (userId > 0)
                        _ = new ApiService().SyncPendingHistoriesAsync(userId);
                }

                // Bắt đầu gửi vị trí GPS lên server
                _heartbeat.Start();
                
                AuthService.LoginOfflineAsGuest();
                MainPage = new AppShell();
            }
            else
            {
                MainPage = new NavigationPage(new WelcomePage());
            }

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
            if (Preferences.Get("is_first_run_complete", false))
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

            // 1. Phân tích POI ID từ link
            int? poiId = QrCodeService.ParsePoiId(uri);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (poiId.HasValue)
                {
                    // LƯU Ý QUAN TRỌNG: Phải thực hiện đăng nhập khách TRƯỚC khi chuyển trang
                    if (!Preferences.Get("is_first_run_complete", false))
                    {
                        Preferences.Set("is_first_run_complete", true);
                        AuthService.LoginOfflineAsGuest();
                    }

                    // Lưu ID để MapPage xử lý khi load (nếu cần)
                    MapTourState.FocusPoiId = poiId.Value;

                    // Chuyển sang giao diện chính nếu đang ở Welcome/Login
                    if (MainPage is not AppShell)
                    {
                        MainPage = new AppShell();
                        await Task.Delay(500); // Chờ AppShell khởi tạo
                    }
                    else
                    {
                        // Nếu đã ở AppShell, điều hướng về trang Home
                        await Shell.Current.GoToAsync("//home");
                    }

                    // Phát audio
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Lấy thông tin POI từ API
                            var poi = await new ApiService().GetPOIById(poiId.Value);
                            if (poi != null)
                            {
                                // Chờ một chút để UI ổn định trước khi phát
                                await Task.Delay(1000); 
                                await AudioPlaybackService.Instance.PlayAsync(poi);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[App] AutoPlay error: {ex.Message}");
                        }
                    });
                }
                else
                {
                    // 2. Nếu không có POI, kiểm tra link guest thuần túy: tourguideapp://guest
                    bool isGuestLink = string.Equals(uri.Scheme, "tourguideapp", StringComparison.OrdinalIgnoreCase) && 
                                       string.Equals(uri.Host, "guest", StringComparison.OrdinalIgnoreCase);

                    if (isGuestLink)
                    {
                        if (!Preferences.Get("is_first_run_complete", false))
                        {
                            Preferences.Set("is_first_run_complete", true);
                            AuthService.LoginOfflineAsGuest();
                        }

                        if (MainPage is not AppShell)
                        {
                            MainPage = new AppShell();
                        }
                        else
                        {
                            await Shell.Current.GoToAsync("//home");
                        }
                    }
                }
            });
        }

        protected override void CleanUp()
        {
            Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
            _heartbeat.Stop();
            if (Preferences.Get("is_first_run_complete", false))
                _ = _heartbeat.NotifyOfflineAsync();
            base.CleanUp();
        }
    }
}