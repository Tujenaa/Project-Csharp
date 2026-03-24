using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TourGuideApp.Services;

namespace TourGuideApp.ViewModels;

public partial class AccountViewModel : ObservableObject
{
    [ObservableProperty]
    private string username;

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
        Username = AuthService.Username;
        Name = AuthService.Name;
        Email = AuthService.Email;
        Phone = AuthService.Phone;
    }
    public bool HasMessage => !string.IsNullOrEmpty(StatusMessage);
    [RelayCommand]
    private async Task SaveProfile()
    {

        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Họ tên không được để trống.";
            IsError = true;
            return;
        }

        var ok = await AuthService.UpdateProfileAsync(Name, Phone);

        if (!ok)
        {
            StatusMessage = "Cập nhật thất bại!";
            IsError = true;

            await Application.Current.MainPage.DisplayAlert("Lỗi", "Cập nhật thất bại!", "OK");
            return;
        }

        StatusMessage = "Đã lưu thành công!";
        IsError = false;
        await Application.Current.MainPage.DisplayAlert("Thành công", "Cập nhật thông tin thành công 🎉", "OK");

        await Task.Delay(2000);
        StatusMessage = string.Empty;
    }
}