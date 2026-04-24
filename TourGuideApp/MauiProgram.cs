using Microsoft.Extensions.Logging;
using TourGuideApp.Services;
using TourGuideApp.ViewModels;
using TourGuideApp.Pages;
using BarcodeScanner.Mobile;

namespace TourGuideApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddBarcodeScannerHandler();
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // --- SERVICES ---
        builder.Services.AddSingleton<ApiService>(); // Thêm dòng này
        builder.Services.AddSingleton<MapService>();
        builder.Services.AddSingleton<DeviceHeartbeatSender>();
        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<LocalDbService>(_ => LocalDbService.Instance);

        // --- VIEWMODELS ---
        builder.Services.AddSingleton<MapViewModel>();

        // --- PAGES ---
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<WelcomePage>();

        var app = builder.Build();

        // Khởi tạo SQLite database ngay khi app start
        _ = LocalDbService.Instance.InitAsync();

        _ = app.Services.GetService<MapViewModel>();

        return app;
    }
}
