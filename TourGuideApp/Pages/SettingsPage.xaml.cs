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
        bool isGuest = AuthService.Username == "guest";
        AvatarLabel.Text = AuthService.AvatarLetter;
        NameLabel.Text = AuthService.Name;
        EmailLabel.Text = isGuest ? "" : AuthService.Email;

        // Cập nhật phần thông tin app dựa trên trạng thái khách
        if (isGuest)
        {
            AppInfoTitleLabel.Text = LocalizationService.Get("guest_login_notification");
            AppInfoTitleLabel.TextColor = Color.FromArgb("#E94560"); // Màu đỏ nhẹ cho thông báo
            VersionLabel.IsVisible = false;
            DescriptionLabel.IsVisible = false;

            // Đổi nút Đăng xuất thành Đăng nhập cho khách
            LogoutButton.Text = LocalizationService.Get("login_button");
            LogoutButton.BackgroundColor = Color.FromArgb("#EDE7FF");
            LogoutButton.TextColor = Color.FromArgb("#512BD4");
            LogoutButton.BorderColor = Color.FromArgb("#D1C4E9");
        }
        else
        {
            AppInfoTitleLabel.Text = LocalizationService.Get("app_name");
            AppInfoTitleLabel.TextColor = Color.FromArgb("#1A1035");
            VersionLabel.IsVisible = true;
            DescriptionLabel.IsVisible = true;
            VersionLabel.Text = LocalizationService.Get("version");
            DescriptionLabel.Text = LocalizationService.Get("app_desc");

            // Trả lại nút Đăng xuất bình thường
            LogoutButton.Text = LocalizationService.Get("logout_button");
            LogoutButton.BackgroundColor = Color.FromArgb("#FEF0F3");
            LogoutButton.TextColor = Color.FromArgb("#E94560");
            LogoutButton.BorderColor = Color.FromArgb("#FCA5A5");
        }
    }

    // ─── Navigate to AccountPage ──────────────────────────────────────────────

    private async void OnProfileCardTapped(object sender, EventArgs e)
    {
        if (AuthService.Username == "guest")
        {
            await DisplayAlert(
                LocalizationService.Get("notification"),
                LocalizationService.Get("guest_account_access_denied"),
                LocalizationService.Get("ok"));
            return;
        }

        await Navigation.PushAsync(new AccountPage(new AccountViewModel()));
    }

    // ─── Language picker ─────────────────────────────────────────────────────

    private async void OnLanguageTapped(object sender, EventArgs e)
    {
        var languages = SettingService.Instance.AvailableLanguages;
        var names = languages.Values.ToArray();

        var chosen = await DisplayActionSheet(
            LocalizationService.Get("choose_language"), 
            LocalizationService.Get("cancel"), 
            null, names);

        if (chosen == null || chosen == LocalizationService.Get("cancel")) return;

        var pair = languages.FirstOrDefault(kv => kv.Value == chosen);

        if (pair.Key != null)
        {
            SettingService.Instance.Language = pair.Key;
            RefreshProfile(); // Cập nhật lại các nhãn thủ công (cho khách) ngay lập tức
        }
    }

    // ─── Logout ───────────────────────────────────────────────────────────────

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        // Nếu là khách, không cần hỏi xác nhận, chuyển thẳng về trang đăng nhập
        if (AuthService.Username == "guest")
        {
            AuthService.Logout();
            if (Application.Current != null)
                Application.Current.MainPage = new NavigationPage(new LoginPage());
            return;
        }

        bool confirm = await DisplayAlert(
            LocalizationService.Get("confirm_title"), 
            LocalizationService.Get("logout_msg"), 
            LocalizationService.Get("yes"), 
            LocalizationService.Get("no"));

        if (!confirm) return;

        AuthService.Logout();

        // Chuyển hướng về trang đăng nhập
        if (Application.Current != null)
        {
            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }
    }
}