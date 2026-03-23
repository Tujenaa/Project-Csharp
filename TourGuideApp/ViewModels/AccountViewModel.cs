using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TourGuideApp.Services;

namespace TourGuideApp.ViewModels;

public partial class AccountViewModel : ObservableObject
{
    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private string email;

    [ObservableProperty]
    private string phone;

    [ObservableProperty]
    private string statusMessage;

    [ObservableProperty]
    private bool isError;

    public AccountViewModel()
    {
        Name = AuthService.Name;
        Email = AuthService.Email;
        Phone = AuthService.Phone;
    }

    [RelayCommand]
    private async Task SaveProfile()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Họ tên không được để trống.";
            IsError = true;
            return;
        }

        AuthService.UpdateProfile(Name, Phone);

        StatusMessage = "Đã lưu thành công!";
        IsError = false;

        await Task.Delay(2000);
        StatusMessage = string.Empty;
    }
}