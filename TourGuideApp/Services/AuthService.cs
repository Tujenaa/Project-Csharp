using System.Net.Http.Json;
using TourGuideApp.Models;
namespace TourGuideApp.Services;

public static class AuthService
{
    private const string KeyUsername = "user_username";
    private const string KeyToken = "user_token";
    private const string KeyEmail = "user_email";
    private const string KeyName = "user_name";
    private const string KeyPhone = "user_phone";
    public static bool IsLoggedIn => Preferences.ContainsKey(KeyToken);
    public static string Username => Preferences.Get(KeyUsername, string.Empty);
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

    // Đăng nhập dạng khách offline
    public static void LoginOfflineAsGuest()
    {
        Preferences.Set(KeyToken, "offline_guest_token");
        Preferences.Set(KeyUsername, "guest");
        Preferences.Set(KeyEmail, "guest@tourguide.local");
        Preferences.Set(KeyName, "Khách");
        Preferences.Set(KeyPhone, "000000000");
        Preferences.Set("user_id", 0);
    }

    // Đang nhập bằng username 
    public static async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var api = new ApiService();

            var user = await api.Login(username, password);

            if (user == null)
                return false;
          
            Preferences.Set(KeyToken, Guid.NewGuid().ToString());
            Preferences.Set(KeyUsername, user.Username);
            Preferences.Set(KeyEmail, user.Email);
            Preferences.Set(KeyName, user.Name);
            Preferences.Set(KeyPhone, user.Phone);

            Preferences.Set("user_id", user.Id); 

            return true;
        }
        catch
        {
            return false;
        }
    }

    // Đăng ký bằng username, email, password
    public static async Task<bool> RegisterAsync(string username, string name, string email, string password)
    {
        try
        {
            var api = new ApiService();

            var user = await api.Register(username, name, email, password);

            if (user == null)
                return false;

            Preferences.Set(KeyToken, Guid.NewGuid().ToString());
            Preferences.Set(KeyEmail, user.Email);
            Preferences.Set(KeyName, user.Name);
            Preferences.Set(KeyPhone, user.Phone);

            Preferences.Set("user_id", user.Id); 

            return true;
        }
        catch
        {
            return false;
        }
    }

    // Cập nhật thông tin người dùng (chỉ name và phone)
    public static async Task<bool> UpdateProfileAsync(string name, string phone)
    {
        try
        {
            var api = new ApiService();
            var userId = Preferences.Get("user_id", 0);

            if (userId == 0) return false;

            var ok = await api.UpdateProfile(userId, name, phone);

            if (!ok) return false;

            // update local
            Preferences.Set(KeyName, name);
            Preferences.Set(KeyPhone, phone);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        try
        {
            var api = new ApiService();
            var userId = Preferences.Get("user_id", 0);

            if (userId == 0) return false;

            return await api.ChangePassword(userId, oldPassword, newPassword);
        }
        catch
        {
            return false;
        }
    }
    // Đăng xuất
    public static void Logout()
    {
        Preferences.Remove(KeyUsername);
        Preferences.Remove(KeyToken);
        Preferences.Remove(KeyEmail);
        Preferences.Remove(KeyName);
        Preferences.Remove(KeyPhone);
        Preferences.Remove("user_id");
        Preferences.Remove(KeyUsername);
    }
}