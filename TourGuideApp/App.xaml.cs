using TourGuideApp.Pages;
using TourGuideApp.Services;

namespace TourGuideApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = AuthService.IsLoggedIn
                ? (Page)new AppShell()
                : new NavigationPage(new LoginPage());
        }
    }
}