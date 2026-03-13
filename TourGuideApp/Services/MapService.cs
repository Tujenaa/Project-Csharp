using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Utilities;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

using MapsuiMap = Mapsui.Map;
using Point = NetTopologySuite.Geometries.Point;
using MapsuiColor = Mapsui.Styles.Color;

namespace TourGuideApp.Services;

public class MapService
{
    public MapsuiMap CreateMap()
    {
        var map = new MapsuiMap();

        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        return map;
    }

    public void AddMarker(MapsuiMap map, double lon, double lat)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);

        var feature = new GeometryFeature
        {
            Geometry = new Point(x, y)
        };

        feature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 0.8
        });

        var layer = new MemoryLayer
        {
            Features = new[] { feature }
        };

        map.Layers.Add(layer);
    }

    public void ZoomToLocation(MapsuiMap map, double lon, double lat)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);

        var center = new MPoint(x, y);

        map.Navigator.CenterOnAndZoomTo(center, 1);
    }

    public void DrawRoute(MapsuiMap map, List<(double lon, double lat)> points)
    {
        var coordinates = new List<Coordinate>();

        foreach (var p in points)
        {
            var (x, y) = SphericalMercator.FromLonLat(p.lon, p.lat);
            coordinates.Add(new Coordinate(x, y));
        }

        var line = new LineString(coordinates.ToArray());

        var feature = new GeometryFeature
        {
            Geometry = line
        };

        feature.Styles.Add(new VectorStyle
        {
            Line = new Pen
            {
                Color = MapsuiColor.Red,
                Width = 4
            }
        });

        var layer = new MemoryLayer
        {
            Features = new[] { feature }
        };

        map.Layers.Add(layer);
    }
}