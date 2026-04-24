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
        AppInfoTitleLabel.Text = LocalizationService.Get("app_name");
        AppInfoTitleLabel.TextColor = Color.FromArgb("#1A1035");
        VersionLabel.IsVisible = true;
        DescriptionLabel.IsVisible = true;
        VersionLabel.Text = LocalizationService.Get("version");
        DescriptionLabel.Text = LocalizationService.Get("app_desc");
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


}