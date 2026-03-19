using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class HistoryPage : ContentPage
{
    public HistoryPage()
    {
        InitializeComponent();
        BindingContext = new HistoryViewModel();
    }

    private void OnClearHistoryClicked(object sender, EventArgs e)
    {
        if (BindingContext is HistoryViewModel vm)
            vm.ClearHistory();
    }
}
