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

    [ObservableProperty]
    private string oldPassword;

    [ObservableProperty]
    private string newPassword;

    [ObservableProperty]
    private string confirmPassword;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isProfileFormVisible = true;

    [ObservableProperty]
    private bool isPasswordFormVisible = false;

    public AccountViewModel()
    {
        Username = AuthService.Username;
        Name = AuthService.Name;
        Email = AuthService.Email;
        Phone = AuthService.Phone;
    }
    public bool HasMessage => !string.IsNullOrEmpty(StatusMessage);

    [RelayCommand]
    private void ToggleProfileForm() => IsProfileFormVisible = !IsProfileFormVisible;

    [RelayCommand]
    private void TogglePasswordForm() => IsPasswordFormVisible = !IsPasswordFormVisible;

    [RelayCommand]
    private async Task SaveProfile()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = LocalizationService.Get("name_required");
            IsError = true;
            return;
        }

        IsBusy = true;
        var ok = await AuthService.UpdateProfileAsync(Name, Phone);
        IsBusy = false;

        if (!ok)
        {
            StatusMessage = LocalizationService.Get("failed");
            IsError = true;

            await Application.Current.MainPage.DisplayAlert(
                LocalizationService.Get("error"), 
                LocalizationService.Get("failed"), 
                LocalizationService.Get("ok"));
            return;
        }

        StatusMessage = LocalizationService.Get("saved");
        IsError = false;
        await Application.Current.MainPage.DisplayAlert(
            LocalizationService.Get("success"), 
            LocalizationService.Get("profile_updated"), 
            LocalizationService.Get("ok"));

        await Task.Delay(2000);
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ChangePassword()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(OldPassword) || 
            string.IsNullOrWhiteSpace(NewPassword) || 
            string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            StatusMessage = LocalizationService.Get("name_required");
            IsError = true;
            return;
        }

        if (NewPassword.Length < 6)
        {
            StatusMessage = LocalizationService.Get("password_too_short");
            IsError = true;
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            StatusMessage = LocalizationService.Get("password_not_match");
            IsError = true;
            return;
        }

        IsBusy = true;
        StatusMessage = LocalizationService.Get("updating_password");
        IsError = false;

        var ok = await AuthService.ChangePasswordAsync(OldPassword, NewPassword);
        IsBusy = false;

        if (ok)
        {
            StatusMessage = LocalizationService.Get("password_changed_success");
            IsError = false;
            await Application.Current.MainPage.DisplayAlert(
                LocalizationService.Get("success"),
                LocalizationService.Get("password_changed_success"),
                LocalizationService.Get("ok"));
            
            OldPassword = NewPassword = ConfirmPassword = string.Empty;
        }
        else
        {
            StatusMessage = LocalizationService.Get("change_password_failed");
            IsError = true;
        }

        await Task.Delay(3000);
        StatusMessage = string.Empty;
    }
}