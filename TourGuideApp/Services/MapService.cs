namespace TourGuideApp.Services;

/// <summary>
/// Tạo nội dung HTML nhúng Leaflet.js.
///
/// JS API (gọi từ C# qua EvaluateJavaScriptAsync):
///   setUserLocation(lat, lon)
///   flyTo(lat, lon, zoom)
///   setPOIs(jsonArray)              – đặt markers + vẽ đường giữa các POI theo thứ tự
///                                     + tự động gọi drawNavigationRoute user→poi[0]
///   clearRoutes()                   – xóa tất cả layers (markers + đường)
///   drawNavigationRoute(fLat,fLon,tLat,tLon) – vẽ đường OSRM (đỏ) từ điểm A → B
///
/// C# nhận sự kiện từ JS qua URL scheme: "tourguide://poi/{id}"
/// </summary>
public class MapService
{
    public string BuildLeafletHtml() => """
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no"/>
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"/>
<style>
  * { margin:0; padding:0; box-sizing:border-box; }
  html, body, #map { width:100%; height:100%; }
</style>
</head>
<body>
<div id="map"></div>
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
<script>
// ── Map init ──────────────────────────────────────────────────────────────────
const map = L.map('map', { zoomControl: true }).setView([10.7769, 106.7009], 13);

L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  maxZoom: 19,
  attribution: '© OpenStreetMap'
}).addTo(map);

// ── State ─────────────────────────────────────────────────────────────────────
let userMarker  = null;
let userLatLng  = null;
let poiMarkers  = [];      // marker của từng POI
let routeLayer  = null;    // đường nối giữa các POI (tím, nét đứt)
let navLayer    = null;    // đường chỉ đường user → POI đầu (đỏ, OSRM)

// ── Icons ─────────────────────────────────────────────────────────────────────
const userIcon = L.divIcon({
  className: '',
  html: '<div style="width:20px;height:20px;border-radius:50%;background:#2563EB;border:3px solid white;box-shadow:0 2px 6px rgba(0,0,0,.5);"></div>',
  iconSize: [20, 20], iconAnchor: [10, 10]
});

function makePOIIcon(label) {
  return L.divIcon({
    className: '',
    html: `<div style="width:36px;height:36px;border-radius:50%;background:#512BD4;border:3px solid white;
           box-shadow:0 2px 6px rgba(0,0,0,.4);display:flex;align-items:center;justify-content:center;
           color:white;font-size:13px;font-weight:bold;">${label}</div>`,
    iconSize: [36, 36], iconAnchor: [18, 18]
  });
}

// ── Public API ────────────────────────────────────────────────────────────────

function setUserLocation(lat, lon) {
  userLatLng = [lat, lon];
  if (userMarker) userMarker.setLatLng(userLatLng);
  else userMarker = L.marker(userLatLng, { icon: userIcon, zIndexOffset: 1000 }).addTo(map);
}

function flyTo(lat, lon, zoom) {
  map.flyTo([lat, lon], zoom || 15, { animate: true, duration: 0.8 });
}

/// Xóa toàn bộ markers POI + cả 2 đường, giữ lại marker user
function clearRoutes() {
  map.eachLayer(layer => {
    if (layer !== userMarker && !(layer instanceof L.TileLayer)) {
      map.removeLayer(layer);
    }
  });

  poiMarkers = [];
  routeLayer = null;
  navLayer = null;
}

/// Đặt markers theo thứ tự mảng POI, vẽ đường nối giữa chúng,
/// rồi tự gọi drawNavigationRoute từ user → poi[0].
function setPOIs(jsonArray) {
  clearRoutes();

  const pois = typeof jsonArray === 'string' ? JSON.parse(jsonArray) : jsonArray;
  if (!pois || pois.length === 0) return;

  pois.forEach((poi, idx) => {
    const m = L.marker([poi.latitude, poi.longitude], { icon: makePOIIcon(idx + 1) })
      .addTo(map)
      .bindTooltip(poi.name, { permanent: false, direction: 'top' });

    m.on('click', () => {
      window.location.href = 'tourguide://poi/' + poi.id;
    });
    poiMarkers.push(m);
  });

  // Đường nối giữa các POI (tím, nét đứt)
  if (pois.length >= 2) {
    const latlngs = pois.map(p => [p.latitude, p.longitude]);
    routeLayer = L.polyline(latlngs, {
      color: '#512BD4', weight: 4, opacity: 0.75, dashArray: '8,5'
    }).addTo(map);
  }

  // Đường chỉ đường từ user → poi đầu tiên (đỏ)
  if (userLatLng) {
    drawNavigationRoute(userLatLng[0], userLatLng[1], pois[0].latitude, pois[0].longitude);
  }
}

/// Vẽ đường thực tế (OSRM) từ (fLat,fLon) → (tLat,tLon).
/// Xóa navLayer cũ trước khi vẽ.
async function drawNavigationRoute(fLat, fLon, tLat, tLon) {
  if (navLayer) { map.removeLayer(navLayer); navLayer = null; }

  try {
    const url = `https://router.project-osrm.org/route/v1/driving/${fLon},${fLat};${tLon},${tLat}?overview=full&geometries=geojson`;
    const res  = await fetch(url);
    const data = await res.json();

    if (data.code === 'Ok' && data.routes.length > 0) {
      navLayer = L.geoJSON(data.routes[0].geometry, {
        style: { color: '#EF4444', weight: 5, opacity: 0.9 }
      }).addTo(map);
    } else {
      throw new Error('no route');
    }
  } catch {
    // Fallback: đường thẳng đỏ nét đứt
    navLayer = L.polyline([[fLat, fLon], [tLat, tLon]], {
      color: '#EF4444', weight: 4, opacity: 0.7, dashArray: '6,4'
    }).addTo(map);
  }
}
</script>
</body>
</html>
""";
}