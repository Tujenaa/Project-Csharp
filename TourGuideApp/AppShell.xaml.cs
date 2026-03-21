using TourGuideApp.Pages;

namespace TourGuideApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("placeDetail", typeof(PlaceDetailPage));
        }
    }
}
