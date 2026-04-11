using System.Xml;
using TourGuideApp.Services;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshProfile();
        _ = SettingService.Instance.LoadLanguagesAsync();
    }

    private void RefreshProfile()
    {
        AvatarLabel.Text = AuthService.AvatarLetter;
        NameLabel.Text = AuthService.Name;
        EmailLabel.Text = AuthService.Email;
    }

    // ─── Navigate to AccountPage ──────────────────────────────────────────────

    private async void OnProfileCardTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AccountPage(new AccountViewModel())
);
    }

    // ─── Language picker ─────────────────────────────────────────────────────

    private async void OnLanguageTapped(object sender, EventArgs e)
    {
        var languages = SettingService.Instance.AvailableLanguages;
        var names = languages.Values.ToArray();

        var chosen = await DisplayActionSheet("Chọn ngôn ngữ", "Huỷ", null, names);

        if (chosen == null || chosen == "Huỷ") return;

        var pair = languages.FirstOrDefault(kv => kv.Value == chosen);

        if (pair.Key != null)
            SettingService.Instance.Language = pair.Key;
    }

    // ─── Logout ───────────────────────────────────────────────────────────────

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Xác nhận", "Bạn có muốn đăng xuất không?", "Có", "Không");

        if (!confirm) return;

        AuthService.Logout();

        Application.Current!.MainPage =
            new NavigationPage(new LoginPage());
    }
}