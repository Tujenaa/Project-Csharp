using System.Net.Http.Json;
using TourGuideApp.Models;

namespace TourGuideApp.Services;

public class ApiService
{
    readonly HttpClient client = new();

    public async Task<List<POI>> GetPOI()
    {
        var pois = await client.GetFromJsonAsync<List<POI>>(
            "http://10.0.2.2:5266/api/POI");

        return pois ?? new List<POI>();
    }
}