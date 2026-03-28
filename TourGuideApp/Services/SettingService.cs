using System.ComponentModel;

namespace TourGuideApp.Services;

public class SettingService : INotifyPropertyChanged
{
    public static SettingService Instance { get; } = new();

    // ── Language ──────────────────────────────────────────────────────────────
    public static readonly Dictionary<string, string> SupportedLanguages = new()
    {
        { "vi", "🇻🇳  Tiếng Việt" },
        { "en", "🇬🇧  English" },
        { "ja", "🇯🇵  日本語" },
        { "zh", "🇨🇳  中文" },
    };

    string _language;
    public string Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            Preferences.Set("app_language", value);
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(LanguageDisplayName));
        }
    }

    public string LanguageDisplayName =>
        SupportedLanguages.TryGetValue(_language, out var name) ? name : _language;

    // ── Auto-play when near POI ───────────────────────────────────────────────
    bool _autoPlay = true;
    public bool AutoPlay
    {
        get => _autoPlay;
        set
        {
            if (_autoPlay == value) return;
            _autoPlay = value;
            Preferences.Set("auto_play", value);
            OnPropertyChanged(nameof(AutoPlay));
        }
    }

    // ── GPS ───────────────────────────────────────────────────────────────────
    bool _gpsEnabled = true;
    public bool GpsEnabled
    {
        get => _gpsEnabled;
        set
        {
            if (_gpsEnabled == value) return;
            _gpsEnabled = value;
            Preferences.Set("gps_enabled", value);
            OnPropertyChanged(nameof(GpsEnabled));
        }
    }

    SettingService()
    {
        // Đọc từ Preferences, nếu không có thì mặc định là "vi"
        _language = Preferences.Get("app_language", "vi");
        _autoPlay = Preferences.Get("auto_play", true);
        _gpsEnabled = Preferences.Get("gps_enabled", true);

        // Log để debug
        System.Diagnostics.Debug.WriteLine($"SettingService initialized: Language = {_language}");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}