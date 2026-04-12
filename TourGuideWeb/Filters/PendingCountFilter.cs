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
                var pois = await client.GetFromJsonAsync<List<PendingItem>>("poi/pending") ?? [];
                var tourPois = await client.GetFromJsonAsync<List<PendingItem>>("tours/pois/all-pending") ?? [];
                context.HttpContext.Items["PendingCount"] = pois.Count + tourPois.Count;
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