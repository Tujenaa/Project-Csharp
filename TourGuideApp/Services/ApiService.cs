using System.Net.Http.Json;
using System.Text.Json;
using TourGuideApp.Models;

namespace TourGuideApp.Services;

public class ApiService
{
    readonly HttpClient client = new();

    // Lấy danh sách POI từ API
    public async Task<List<POI>> GetPOI()
    {
        var pois = await client.GetFromJsonAsync<List<POI>>(
            "http://192.168.1.168:5266/api/POI");

        return pois ?? new List<POI>();
    }

    // Lưu lịch sử nghe POI
    public async Task SaveHistory(int poiId)
    {
        var client = new HttpClient();

        var json = JsonSerializer.Serialize(new
        {
            poiId = poiId
        });

        var content = new StringContent(json);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        await client.PostAsync("http://192.168.1.168:5266/api/POI/history", content);
    }

    // Lấy top 5 POI được nghe nhiều nhất
    public async Task<List<POI>> GetTopPOI()
    {
        return await client.GetFromJsonAsync<List<POI>>(
            "http://192.168.1.168:5266/api/POI/top")
            ?? new List<POI>();
    }

    // Lấy tổng số POI
    public async Task<int> GetPoiCount()
    {
        return await client.GetFromJsonAsync<int>(
            "http://192.168.1.168:5266/api/POI/count");
    }

    // Lấy tổng số audio 
    public async Task<int> GetAudioCount()
    {
        return await client.GetFromJsonAsync<int>(
            "http://192.168.1.168:5266/api/POI/audio-count");
    }
}