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
            StatusMessage = LocalizationService.Get("name_required");
            IsError = true;
            return;
        }

        var ok = await AuthService.UpdateProfileAsync(Name, Phone);

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
}