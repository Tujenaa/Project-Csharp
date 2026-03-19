namespace TourGuideApp.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("Vui lòng nhập email và mật khẩu.");
            return;
        }

        // Disable button to prevent double-tap
        LoginButton.IsEnabled = false;
        LoginButton.Text = "Đang đăng nhập...";

        try
        {
            // TODO: replace with real auth service call
            await Task.Delay(800); // simulate network

            // On success navigate to shell
            Application.Current!.MainPage = new AppShell();
        }
        catch (Exception ex)
        {
            ShowError("Đăng nhập thất bại. Vui lòng thử lại.");
            Console.WriteLine(ex.Message);
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "Đăng nhập";
        }
    }

    private async void OnGuestLoginClicked(object sender, EventArgs e)
    {
        Application.Current!.MainPage = new AppShell();
        await Task.CompletedTask;
    }

    private void OnTogglePasswordTapped(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        TogglePasswordLabel.Text = PasswordEntry.IsPassword ? "👁" : "🙈";
    }

    private async void OnForgotPasswordTapped(object sender, EventArgs e)
    {
        await DisplayAlert("Quên mật khẩu", "Tính năng đặt lại mật khẩu sẽ sớm được hỗ trợ.", "OK");
    }

    private async void OnSignUpTapped(object sender, EventArgs e)
    {
        await DisplayAlert("Đăng ký", "Tính năng đăng ký sẽ sớm được hỗ trợ.", "OK");
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
