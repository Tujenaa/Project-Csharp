using System.Net.Http.Json;
using System.Text.Json;
using TourGuideApp.Models;

namespace TourGuideApp.Services;

public class ApiService
{
    readonly HttpClient client = new();

    public async Task<List<POI>> GetPOI()
    {
        var pois = await client.GetFromJsonAsync<List<POI>>(
            "http://192.168.1.125:5266/api/POI");

        return pois ?? new List<POI>();
    }
    public async Task SaveHistory(int poiId)
    {
        var client = new HttpClient();

        var json = JsonSerializer.Serialize(new
        {
            poiId = poiId
        });

        var content = new StringContent(json);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        await client.PostAsync("http://192.168.1.125:5266/api/POI/history", content);
    }
}