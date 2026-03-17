var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

<<<<<<< HEAD
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7134/api/";

builder.Services.AddHttpClient("API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
=======
builder.Services.AddHttpClient("API", client =>
{
    var url = builder.Configuration["ApiUrl"] ?? "http://localhost:5266/api/";
    if (!url.EndsWith("/")) url += "/";
    client.BaseAddress = new Uri(url);
>>>>>>> a774dae7a97873962cbcdc147ce51e50f1dd3c0e
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();