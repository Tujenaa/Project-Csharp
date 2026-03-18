using Mapsui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using TourGuideApp.Services;
using TourGuideApp.ViewModels;

namespace TourGuideApp;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // SERVICES
        builder.Services.AddSingleton<MapService>();
        builder.Services.AddSingleton<LocationService>();

        // VIEWMODELS
        builder.Services.AddSingleton<MapViewModel>();

        return builder.Build();
    }
}