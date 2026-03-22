using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TourGuideApp.Models;

namespace TourGuideApp.Services;

public class ApiService
{
    readonly HttpClient client = new();

    public static class ApiConfig
    {
        public const string BaseUrl = "http://192.168.1.182:5266/api/";
       // public const string BaseUrl = "http://10.0.2.2:5266/api/";
    }

    // Lấy danh sách POI
    public async Task<List<POI>> GetPOI()
    {
        var pois = await client.GetFromJsonAsync<List<POI>>(
            $"{ApiConfig.BaseUrl}POI");

        return pois ?? new List<POI>();
    }

    // Lưu lịch sử
    public async Task SaveHistory(int poiId)
    {
        var json = JsonSerializer.Serialize(new
        {
            poiId = poiId
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await client.PostAsync($"{ApiConfig.BaseUrl}POI/history", content);
    }

    // Top POI
    public async Task<List<POI>> GetTopPOI()
    {
        return await client.GetFromJsonAsync<List<POI>>(
            $"{ApiConfig.BaseUrl}POI/top")
            ?? new List<POI>();
    }

    // Count POI
    public async Task<int> GetPoiCount()
    {
        return await client.GetFromJsonAsync<int>(
            $"{ApiConfig.BaseUrl}POI/count");
    }

    // Count Audio
    public async Task<int> GetAudioCount()
    {
        return await client.GetFromJsonAsync<int>(
            $"{ApiConfig.BaseUrl}POI/audio-count");
    }
}