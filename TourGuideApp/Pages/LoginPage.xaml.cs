using TourGuideApp.Services;
using TourGuideApp.ViewModels;

namespace TourGuideApp.Pages;

public partial class LoginPage : ContentPage
{
    private bool _showingLogin = true;

    public LoginPage()
    {
        InitializeComponent();
    }

    // ─── Tab switching ────────────────────────────────────────────────────────

    private void OnLoginTabTapped(object sender, EventArgs e) => ShowLogin();
    private void OnRegisterTabTapped(object sender, EventArgs e) => ShowRegister();

    private void ShowLogin()
    {
        _showingLogin = true;

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
        _showingLogin = false;

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
            ShowLoginError("Vui lòng nhập username và mật khẩu.");
            return;
        }

        SetLoginLoading(true);

        if (!await ConnectivityService.CanReachApiAsync())
        {
            SetLoginLoading(false);
            ShowLoginError("Bạn đang offline. Không thể kết nối đến máy chủ.");
            return;
        }

        try
        {
            bool ok = await AuthService.LoginAsync(username, password);

            if (ok)
            {
                // Tải lịch sử ngay khi đăng nhập thành công
                _ = HistoryStore.LoadFromApiAsync();
                GoToShell();
            }
            else
                ShowLoginError("Username hoặc mật khẩu không đúng.");
        }
        catch (Exception ex)
        {
            ShowLoginError("Đăng nhập thất bại. Vui lòng thử lại.");
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
            ShowRegError("Vui lòng nhập username."); return;
        }

        if (string.IsNullOrEmpty(name))
        {
            ShowRegError("Vui lòng nhập họ tên."); return;
        }

        if (string.IsNullOrEmpty(email))
        {
            ShowRegError("Vui lòng nhập email."); return;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            ShowRegError("Mật khẩu phải ít nhất 6 ký tự."); return;
        }

        if (password != confirm)
        {
            ShowRegError("Mật khẩu xác nhận không khớp."); return;
        }

        SetRegisterLoading(true);

        if (!await ConnectivityService.CanReachApiAsync())
        {
            SetRegisterLoading(false);
            ShowRegError("Bạn đang offline. Không thể kết nối đến máy chủ.");
            return;
        }

        try
        {
            bool ok = await AuthService.RegisterAsync(username, name, email, password);

            if (ok)
                GoToShell();
            else
                ShowRegError("Đăng ký thất bại. Vui lòng thử lại.");
        }
        catch (Exception ex)
        {
            ShowRegError("Có lỗi xảy ra. Vui lòng thử lại.");
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
        LoginButton.Text = loading ? "Đang đăng nhập..." : "Đăng nhập";
    }

    private void SetRegisterLoading(bool loading)
    {
        RegisterButton.IsEnabled = !loading;
        RegisterButton.Text = loading ? "Đang tạo tài khoản..." : "Tạo tài khoản";
    }
}