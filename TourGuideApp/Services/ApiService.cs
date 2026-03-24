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
        public const string BaseUrl = "http://192.168.1.211:5266/api/";
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

    // Đăng nhập
    public async Task<UserDto?> Login(string username, string password)
    {
        var response = await client.PostAsJsonAsync(
            $"{ApiConfig.BaseUrl}users/login",
            new { Username = username, Password = password });

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<UserDto>();
    }

    // Đăng ký người dùng mới
    public async Task<UserDto?> Register(string username, string name, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            $"{ApiConfig.BaseUrl}users/register",
            new
            {
                Username = username,
                Name = name,
                Email = email,
                PasswordHash = password
            });

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<UserDto>();
    }

    // Cập nhật thông tin người dùng
    public async Task<bool> UpdateProfile(int userId, string name, string phone)
    {
        var response = await client.PutAsJsonAsync(
            $"{ApiConfig.BaseUrl}users/customer/{userId}",
            new
            {
                Name = name,
                Phone = phone
            });

        return response.IsSuccessStatusCode;
    }
}