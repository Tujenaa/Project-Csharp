var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddHttpClient("API", client =>
{
    var url = builder.Configuration["ApiUrl"] ?? "http://localhost:5266/api/";
    if (!url.EndsWith("/")) url += "/";
    client.BaseAddress = new Uri(url);
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();