using TourGuideApp.Pages;
using TourGuideApp.Services;

namespace TourGuideApp
{
    public partial class App : Application
    {
        public App()
        {
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

        protected override void CleanUp()
        {
            Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
            base.CleanUp();
        }
    }
}
