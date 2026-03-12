using Mapsui;
using Mapsui.Layers;
using Mapsui.Tiling;

namespace TourGuideApp.Services;

public class MapService
{
    // 1. Chỉ định rõ kiểu trả về là Mapsui.Map
    public Mapsui.Map CreateMap()
    {
        // 2. Cấp phát đúng đối tượng Mapsui.Map
        var map = new Mapsui.Map();

        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        return map;
    }
}