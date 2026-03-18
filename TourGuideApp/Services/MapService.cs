using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Utilities;
using NetTopologySuite.Geometries;
using TourGuideApp.Models;

using MapsuiMap = Mapsui.Map;
using Point = NetTopologySuite.Geometries.Point;
using MapsuiColor = Mapsui.Styles.Color;

namespace TourGuideApp.Services;

public class MapService
{
    // Layer dùng chung để chứa tất cả marker (tránh tạo nhiều layer gây lag)
    private readonly MemoryLayer markerLayer = new MemoryLayer
    {
        Features = new List<Mapsui.IFeature>()
    };

    // Tạo bản đồ và thêm layer nền + layer marker

    public MapsuiMap CreateMap()
    {
        var map = new MapsuiMap();

        // Layer bản đồ (OpenStreetMap)
        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        // Layer chứa marker (POI + vị trí user)
        map.Layers.Add(markerLayer);

        return map;
    }

    // Thêm marker POI lên bản đồ
    public void AddMarker(MapsuiMap map, POI poi)
    {
        var (x, y) = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);

        var feature = new GeometryFeature
        {
            Geometry = new Point(x, y)
        };

        //  Gắn POI vào marker để xử lý khi click
        feature["POI"] = poi;

        feature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 0.8
        });

        ((List<Mapsui.IFeature>)markerLayer.Features).Add(feature);

        //  cập nhật lại map
        markerLayer.DataHasChanged();
    }

    /// Zoom tới 1 vị trí 
    public void ZoomToLocation(MapsuiMap map, double lon, double lat)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);

        var center = new MPoint(x, y);

        map.Navigator.CenterOnAndZoomTo(center, 5);
    }

    // Vẽ đường nối giữa các điểm 
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

    /// Thêm marker vị trí hiện tại của người dùng (màu xanh)
    public void AddCurrentLocationMarker(MapsuiMap map, double lon, double lat)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);

        var feature = new GeometryFeature
        {
            Geometry = new Point(x, y)
        };

        feature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 1.2,
            Fill = new Mapsui.Styles.Brush(MapsuiColor.Blue) // khác POI
        });

        ((List<Mapsui.IFeature>)markerLayer.Features).Add(feature);

        markerLayer.DataHasChanged();
    }
}