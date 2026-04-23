using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Antiforgery;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages().AddMvcOptions(options =>
{
    options.Filters.Add<GPSGuide.Web.Filters.PendingCountFilter>();
});

builder.Services.AddHttpClient("API", client =>
{
    var url = builder.Configuration["ApiUrl"] ?? "http://localhost:5266/api/";
    if (!url.EndsWith("/")) url += "/";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddHttpClient("ImageProxy");

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    options.Cookie.Name = ".GPSGuide.Session";
});

// Tắt antiforgery validation để tránh lỗi khi HTTPS
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    options.HeaderName = "X-CSRF-TOKEN";
});

var app = builder.Build();

var invariant = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(invariant),
    SupportedCultures = new[] { invariant },
    SupportedUICultures = new[] { invariant }
});

app.UseStaticFiles();
app.UseRouting();

// Fix Edge Tracking Prevention: đặt CookiePolicy trước UseSession
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Lax,
    Secure = CookieSecurePolicy.None
});

app.UseSession();

// Proxy ảnh từ API
app.MapGet("/img-proxy", async (string url, IHttpClientFactory httpFactory, IConfiguration config) =>
{
    if (string.IsNullOrEmpty(url)) return Results.NotFound();
    try
    {
        var apiBase = config["ApiBaseUrl"] ?? "http://localhost:5266";
        var fullUrl = url.StartsWith("http") ? url : apiBase + url;
        var client = httpFactory.CreateClient("ImageProxy");
        var bytes = await client.GetByteArrayAsync(fullUrl);
        var ext = Path.GetExtension(fullUrl.Split('?')[0]).ToLower();
        var mime = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        return Results.File(bytes, mime);
    }
    catch { return Results.NotFound(); }
});

// Proxy danh sách thiết bị online để tránh lỗi CORS/Mixed Content trên điện thoại thật
app.MapGet("/api-device-proxy/active", async (IHttpClientFactory httpFactory) =>
{
    try
    {
        var client = httpFactory.CreateClient("API");
        var resp = await client.GetAsync("device/active");
        if (!resp.IsSuccessStatusCode) return Results.StatusCode((int)resp.StatusCode);
        var content = await resp.Content.ReadFromJsonAsync<object>();
        return Results.Ok(content);
    }
    catch { return Results.Problem("Không thể kết nối tới API server."); }
});

// Auth middleware
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";
    var publicPaths = new[] { "/auth", "/register", "/img-proxy" };
    var isPublic = publicPaths.Any(p => path.StartsWith(p));
    if (!isPublic && string.IsNullOrEmpty(context.Session.GetString("UserId")))
    {
        context.Response.Redirect("/Auth");
        return;
    }
    await next();
});

app.MapRazorPages();
app.Run();