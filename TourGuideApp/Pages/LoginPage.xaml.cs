using TourGuideApp.Services;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class LoginPage : ContentPage
{

    public LoginPage()
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
        }
    }

    // ─── Tab switching ────────────────────────────────────────────────────────

    private void OnLoginTabTapped(object sender, EventArgs e) => ShowLogin();
    private void OnRegisterTabTapped(object sender, EventArgs e) => ShowRegister();

    private void ShowLogin()
    {

        // Tab styles
        LoginTabBorder.BackgroundColor = Color.FromArgb("#512BD4");
        LoginTabLabel.TextColor = Colors.White;
        RegisterTabBorder.BackgroundColor = Colors.Transparent;
        RegisterTabLabel.TextColor = Color.FromArgb("#9B7FEA");

        LoginForm.IsVisible = true;
        RegisterForm.IsVisible = false;
        ClearErrors();
    }

    private void ShowRegister()
    {

        RegisterTabBorder.BackgroundColor = Color.FromArgb("#512BD4");
        RegisterTabLabel.TextColor = Colors.White;
        LoginTabBorder.BackgroundColor = Colors.Transparent;
        LoginTabLabel.TextColor = Color.FromArgb("#9B7FEA");

        RegisterForm.IsVisible = true;
        LoginForm.IsVisible = false;
        ClearErrors();
    }

    // ─── Login ────────────────────────────────────────────────────────────────

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = LoginUsernameEntry.Text?.Trim();
        var password = LoginPasswordEntry.Text?.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowLoginError(LocalizationService.Get("login_fields_required"));
            return;
        }

        SetLoginLoading(true);

        if (!await ConnectivityService.CanReachApiAsync())
        {
            SetLoginLoading(false);
            ShowLoginError(LocalizationService.Get("offline_error"));
            return;
        }

        try
        {
            var result = await AuthService.LoginAsync(username, password);

            if (result.Success)
            {
                // Tải lịch sử ngay khi đăng nhập thành công
                _ = HistoryStore.LoadFromApiAsync();
                GoToShell();
            }
            else
            {
                // result.Message lúc này là các key như "account_disabled", "login_failed_msg"...
                ShowLoginError(LocalizationService.Get(result.Message));
            }
        }
        catch (Exception ex)
        {
            ShowLoginError(LocalizationService.Get("failed"));
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
        finally
        {
            SetLoginLoading(false);
        }
    }

    // ─── Register ─────────────────────────────────────────────────────────────

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var username = RegUsernameEntry.Text?.Trim();
        var name = RegNameEntry.Text?.Trim();
        var email = RegEmailEntry.Text?.Trim();
        var password = RegPasswordEntry.Text;
        var confirm = RegConfirmPasswordEntry.Text;

        if (string.IsNullOrEmpty(username))
        {
            ShowRegError(LocalizationService.Get("username_required")); return;
        }

        if (string.IsNullOrEmpty(name))
        {
            ShowRegError(LocalizationService.Get("name_required")); return;
        }

        if (string.IsNullOrEmpty(email))
        {
            ShowRegError(LocalizationService.Get("email_required")); return;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            ShowRegError(LocalizationService.Get("password_too_short")); return;
        }

        if (password != confirm)
        {
            ShowRegError(LocalizationService.Get("password_not_match")); return;
        }

        SetRegisterLoading(true);

        if (!await ConnectivityService.CanReachApiAsync())
        {
            SetRegisterLoading(false);
            ShowRegError(LocalizationService.Get("offline_error"));
            return;
        }

        try
        {
            bool ok = await AuthService.RegisterAsync(username, name, email, password);

            if (ok)
                GoToShell();
            else
                ShowRegError(LocalizationService.Get("registration_failed"));
        }
        catch (Exception ex)
        {
            ShowRegError(LocalizationService.Get("error"));
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
        finally
        {
            SetRegisterLoading(false);
        }
    }

    // ─── Guest ────────────────────────────────────────────────────────────────

    private void OnGuestLoginClicked(object sender, EventArgs e)
    {
        AuthService.LoginOfflineAsGuest();
        GoToShell();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void GoToShell()
        => Application.Current!.MainPage = new AppShell();

    private void OnLoginTogglePasswordTapped(object sender, EventArgs e)
    {
        LoginPasswordEntry.IsPassword = !LoginPasswordEntry.IsPassword;
        LoginTogglePwdLabel.Text = LoginPasswordEntry.IsPassword ? "👁" : "🙈";
    }

    private void OnRegTogglePasswordTapped(object sender, EventArgs e)
    {
        RegPasswordEntry.IsPassword = !RegPasswordEntry.IsPassword;
        RegTogglePwdLabel.Text = RegPasswordEntry.IsPassword ? "👁" : "🙈";
    }

    private void ShowLoginError(string msg)
    {
        LoginErrorLabel.Text = msg;
        LoginErrorLabel.IsVisible = true;
    }

    private void ShowRegError(string msg)
    {
        RegErrorLabel.Text = msg;
        RegErrorLabel.IsVisible = true;
    }

    private void ClearErrors()
    {
        LoginErrorLabel.IsVisible = false;
        RegErrorLabel.IsVisible = false;
    }

    private void SetLoginLoading(bool loading)
    {
        LoginButton.IsEnabled = !loading;
        LoginButton.Text = loading ? LocalizationService.Get("logging_in") : LocalizationService.Get("login_button");
    }

    private void SetRegisterLoading(bool loading)
    {
        RegisterButton.IsEnabled = !loading;
        RegisterButton.Text = loading ? LocalizationService.Get("creating_account") : LocalizationService.Get("register_button");
    }
}