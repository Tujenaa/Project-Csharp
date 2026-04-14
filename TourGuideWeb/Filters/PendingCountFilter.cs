using Microsoft.AspNetCore.Mvc.Filters;
using System.Net.Http.Json;

namespace GPSGuide.Web.Filters;

public class PendingCountFilter : IAsyncPageFilter
{
    private readonly IHttpClientFactory _http;
    public PendingCountFilter(IHttpClientFactory http) => _http = http;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var role = context.HttpContext.Session.GetString("Role") ?? "";
        if (role == "ADMIN")
        {
            try
            {
                var client = _http.CreateClient("API");

                // 1. Lấy danh sách POI chờ duyệt mới
                var pois = await client.GetFromJsonAsync<List<PendingItem>>("poi/pending") ?? [];

                // 2. Lấy danh sách xin VÀO tour
                var tourPois = await client.GetFromJsonAsync<List<PendingItem>>("tours/pois/all-pending") ?? [];

                // 3. Lấy danh sách xin RỜI tour (Thêm dòng này)
                var removeTourPois = await client.GetFromJsonAsync<List<PendingItem>>("tours/pois/all-remove-pending") ?? [];

                // 4. Cộng tổng cả 3 loại lại
                context.HttpContext.Items["PendingCount"] = pois.Count + tourPois.Count + removeTourPois.Count;
            }
            catch { }
        }

        var result = await next();

        if (context.HttpContext.Items.TryGetValue("PendingCount", out var count))
        {
            if (result.Result is Microsoft.AspNetCore.Mvc.RazorPages.PageResult pageResult)
                pageResult.ViewData["PendingCount"] = count;
        }
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    private record PendingItem(int Id, string Status);
}