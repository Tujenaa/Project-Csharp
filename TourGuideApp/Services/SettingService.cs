using System.ComponentModel;

namespace TourGuideApp.Services;

public class SettingService : INotifyPropertyChanged
{
    public static SettingService Instance { get; } = new();

    // ── Language ──────────────────────────────────────────────────────────────
    
    // Lưu trữ danh sách ngôn ngữ động từ API
    public Dictionary<string, string> AvailableLanguages { get; private set; } = new()
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
        AvailableLanguages.TryGetValue(_language, out var name) ? name : _language;

    /// <summary>
    /// Tải danh sách ngôn ngữ từ API và cập nhật AvailableLanguages.
    /// </summary>
    public async Task LoadLanguagesAsync()
    {
        try
        {
            var api = new ApiService();
            var list = await api.GetLanguages();
            
            if (list != null && list.Count > 0)
            {
                // 1. Lấy danh sách POI để kiểm tra ngôn ngữ nào thực tế có script
                var pois = await LocalDbService.Instance.GetCachedPOIsAsync();
                var usedLanguageCodes = pois
                    .SelectMany(p => p.Audios)
                    .Where(a => !string.IsNullOrWhiteSpace(a.Script))
                    .Select(a => a.LanguageCode?.ToLowerInvariant())
                    .Where(code => code != null)
                    .ToHashSet();

                var dict = new Dictionary<string, string>();
                
                // 2. Chỉ hiển thị các ngôn ngữ đang hoạt động VÀ có ít nhất 1 script
                foreach (var l in list.Where(l => l.IsActive))
                {
                    string normalizedCode = l.Code.ToLowerInvariant();
                    if (!usedLanguageCodes.Contains(normalizedCode)) continue;

                    string emoji = GetFlagEmoji(l.Code);
                    dict[l.Code] = string.IsNullOrEmpty(emoji) ? l.Name : $"{emoji}  {l.Name}";
                }

                AvailableLanguages = dict;
                OnPropertyChanged(nameof(LanguageDisplayName));
                System.Diagnostics.Debug.WriteLine($"[SettingService] Loaded {dict.Count} languages (filtered by scripts)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingService] LoadLanguages failed: {ex.Message}");
        }
    }

    private string GetFlagEmoji(string langCode)
    {
        return langCode.ToLower() switch
        {
            "vi" => "🇻🇳",
            "en" => "🇬🇧",
            "ja" => "🇯🇵",
            "zh" => "🇨🇳",
            "ko" => "🇰🇷",
            "fr" => "🇫🇷",
            "de" => "🇩🇪",
            "es" => "🇪🇸",
            "it" => "🇮🇹",
            "ru" => "🇷🇺",
            _ => "🌐"
        };
    }

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