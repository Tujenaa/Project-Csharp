using TourGuideApp.Services;

namespace TourGuideApp.Pages;

public partial class WelcomePage : ContentPage
{
    public WelcomePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateLanguageLabel();
    }

    private void UpdateLanguageLabel()
    {
        var displayName = SettingService.Instance.LanguageDisplayName;
        // Extract emoji part
        LanguageLabel.Text = displayName.Split(' ').FirstOrDefault() ?? "🌐";
    }

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
            UpdateLanguageLabel();
            
            // Reload the page to translate texts immediately
            Application.Current!.MainPage = new NavigationPage(new WelcomePage());
        }
    }

    private void OnStartClicked(object sender, EventArgs e)
    {
        Preferences.Set("is_first_run_complete", true);
        AuthService.LoginOfflineAsGuest();
        Application.Current!.MainPage = new AppShell();
    }
}
