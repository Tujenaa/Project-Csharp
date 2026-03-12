using MapsuiMap = Mapsui.Map;
using TourGuideApp.Services;

namespace TourGuideApp.ViewModels;

public class MapViewModel
{
    public MapsuiMap Map { get; set; }

    public MapViewModel()
    {
        Map = new MapService().CreateMap();
    }
}