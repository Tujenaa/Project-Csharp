using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class HistoryPage : ContentPage
{
    private HistoryViewModel? _vm;

    public HistoryPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        _vm = new HistoryViewModel();
        BindingContext = _vm;
        
        // Tải dữ liệu từ API khi vào trang
        _ = _vm.LoadDataAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Huỷ đăng ký event tránh memory leak khi trang bị dispose
        if (_vm != null)
        {
            HistoryStore.OnItemAdded -= _vm.HandleItemAdded;
        }
    }
}