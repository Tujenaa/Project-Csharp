using System.Globalization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddHttpClient("API", client =>
{
    var url = builder.Configuration["ApiUrl"] ?? "http://localhost:5266/api/";
    if (!url.EndsWith("/")) url += "/";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
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
app.UseSession();

// Auth middleware
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";
    var publicPaths = new[] { "/auth", "/register" };
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