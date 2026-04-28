using System.Security.Cryptography;
using System.Text;

namespace TourGuideApp.Services;

public class ImageCacheService
{
    private static ImageCacheService? _instance;
    public static ImageCacheService Instance => _instance ??= new ImageCacheService();

    private readonly string _cacheDir = Path.Combine(FileSystem.AppDataDirectory, "cached_images");
    private readonly HttpClient _httpClient = new();

    private ImageCacheService()
    {
        if (!Directory.Exists(_cacheDir))
        {
            Directory.CreateDirectory(_cacheDir);
        }
    }

    public async Task CachePoisImagesAsync(IEnumerable<Models.POI> pois)
    {
        var urls = pois.SelectMany(p => p.FullImages).Where(url => url.StartsWith("http")).Distinct();
        await DownloadAndCacheImagesAsync(urls);
    }

    public async Task DownloadAndCacheImagesAsync(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            try
            {
                var localPath = GetLocalPath(url);
                if (File.Exists(localPath)) continue;

                var bytes = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(localPath, bytes);
                System.Diagnostics.Debug.WriteLine($"[ImageCache] Cached: {url}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageCache] Error caching {url}: {ex.Message}");
            }
        }
    }

    public string GetLocalPath(string url)
    {
        var fileName = GetHash(url);
        return Path.Combine(_cacheDir, fileName);
    }

    public string? GetImageSource(string remoteUrl)
    {
        if (string.IsNullOrEmpty(remoteUrl) || !remoteUrl.StartsWith("http")) return remoteUrl;

        var localPath = GetLocalPath(remoteUrl);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        return remoteUrl;
    }

    private string GetHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
