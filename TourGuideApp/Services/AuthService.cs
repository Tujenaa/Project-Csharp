namespace TourGuideApp.Services;

public static class AuthService
{
    private const string KeyToken = "user_token";
    private const string KeyEmail = "user_email";
    private const string KeyName = "user_name";
    private const string KeyPhone = "user_phone";

    // ─── Read ────────────────────────────────────────────────────────────────
    public static bool IsLoggedIn => Preferences.ContainsKey(KeyToken);
    public static string Email => Preferences.Get(KeyEmail, string.Empty);
    public static string Name => Preferences.Get(KeyName, "Người dùng");
    public static string Phone => Preferences.Get(KeyPhone, string.Empty);
    public static string AvatarLetter
    {
        get
        {
            var n = Name;
            return n.Length > 0 ? n[0].ToString().ToUpper() : "U";
        }
    }

    // ─── Write ───────────────────────────────────────────────────────────────

    /// <summary>Simulated login. Returns true on success.</summary>
    public static async Task<bool> LoginAsync(string email, string password)
    {
        await Task.Delay(800); // simulate network

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return false;

        // TODO: replace with real API call
        Preferences.Set(KeyToken, Guid.NewGuid().ToString());
        Preferences.Set(KeyEmail, email.Trim());

        var existing = Preferences.Get(KeyName, string.Empty);
        if (string.IsNullOrEmpty(existing))
            Preferences.Set(KeyName, email.Split('@')[0]);

        return true;
    }

    /// <summary>Simulated registration. Returns true on success.</summary>
    public static async Task<bool> RegisterAsync(string name, string email, string password)
    {
        await Task.Delay(800); // simulate network

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
            return false;

        // TODO: replace with real API call
        Preferences.Set(KeyToken, Guid.NewGuid().ToString());
        Preferences.Set(KeyEmail, email.Trim());
        Preferences.Set(KeyName, name.Trim());

        return true;
    }

    /// <summary>Update profile fields.</summary>
    public static void UpdateProfile(string name, string phone)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Preferences.Set(KeyName, name.Trim());

        Preferences.Set(KeyPhone, phone?.Trim() ?? string.Empty);
    }

    public static void Logout()
    {
        Preferences.Remove(KeyToken);
        Preferences.Remove(KeyEmail);
        Preferences.Remove(KeyName);
        Preferences.Remove(KeyPhone);
    }
}