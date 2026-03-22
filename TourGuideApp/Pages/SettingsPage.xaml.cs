using TourGuideApp.Services;

namespace TourGuideApp.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private async void OnLanguageTapped(object sender, EventArgs e)
    {
        // Build display list
        var names = SettingService.SupportedLanguages.Values.ToArray();

        var chosen = await DisplayActionSheet(
            "Chọn ngôn ngữ", "Huỷ", null, names);

        if (chosen == null || chosen == "Huỷ") return;

        // Map display name → language code
        var pair = SettingService.SupportedLanguages
            .FirstOrDefault(kv => kv.Value == chosen);

        if (pair.Key != null)
            SettingService.Instance.Language = pair.Key;
    }
}