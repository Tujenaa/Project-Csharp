namespace TourGuideApp.Services;

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

  /* Ẩn attribution mặc định cho gọn */
  .leaflet-control-attribution { display:none !important; }
</style>
</head>
<body>
<div id="map"></div>
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
<script>
// ── Map init ──────────────────────────────────────────────────────────────────
const map = L.map('map', {
  zoomControl: false   // ẩn nút +/- mặc định (không cần thiết trên mobile)
}).setView([10.7769, 106.7009], 14);

// Thêm zoom control ở góc trái trên (ít chiếm diện tích hơn)
//L.control.zoom({ position: 'topleft' }).addTo(map);

L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
  maxZoom: 19
}).addTo(map);

// ── State ─────────────────────────────────────────────────────────────────────
let userMarker = null;
let userLatLng = null;
let poiMarkers = [];

// ── Icons ─────────────────────────────────────────────────────────────────────
const userIcon = L.divIcon({
  className: '',
  html: `<div style="
    width:18px; height:18px; border-radius:50%;
    background:#2563EB; border:3px solid white;
    box-shadow:0 2px 8px rgba(37,99,235,.6);
  "></div>
  <div style="
    width:36px; height:36px; border-radius:50%;
    background:rgba(37,99,235,.15);
    position:absolute; top:-9px; left:-9px;
    animation:pulse 2s infinite;
  "></div>`,
  iconSize: [18, 18], iconAnchor: [9, 9]
});

function makePOIIcon(isHighlighted = false) {
  const bgColor = isHighlighted ? '#F59E0B' : '#EF4444'; // Orange or Red
  const scale = isHighlighted ? 'scale(1.2)' : 'scale(1)';
  return L.divIcon({
    className: '',
    html: `<div style="
      width:30px;
      height:30px;
      background:${bgColor};
      border-radius:50% 50% 50% 0;
      transform: rotate(-45deg) ${scale};
      border:2px solid white;
      box-shadow:0 2px 8px rgba(0,0,0,.3);
      transition: all 0.2s ease-in-out;
    "></div>`,
    iconSize: [30, 30],
    iconAnchor: [15, 30]
  });
}

// ── Pulse animation ───────────────────────────────────────────────────────────
const style = document.createElement('style');
style.textContent = `
  @keyframes pulse {
    0%   { transform: scale(1);   opacity: .6; }
    50%  { transform: scale(1.6); opacity: .2; }
    100% { transform: scale(1);   opacity: .6; }
  }`;
document.head.appendChild(style);

// ── Public API ────────────────────────────────────────────────────────────────

function setUserLocation(lat, lon) {
  userLatLng = [lat, lon];
  if (userMarker) userMarker.setLatLng(userLatLng);
  else {
    userMarker = L.marker(userLatLng, {
      icon: userIcon, zIndexOffset: 1000, interactive: false
    }).addTo(map);
  }
}

function flyTo(lat, lon, zoom) {
  map.flyTo([lat, lon], zoom || 14, { animate: true, duration: 0.8 });
}

/// Xóa POI markers (giữ lại marker user)
function clearRoutes() {
  poiMarkers.forEach(m => map.removeLayer(m));
  poiMarkers = [];
}

/// Đặt markers POI – KHÔNG vẽ đường nối, KHÔNG auto nav route
function setPOIs(jsonArray) {
  clearRoutes();

  const pois = typeof jsonArray === 'string' ? JSON.parse(jsonArray) : jsonArray;
  if (!pois || pois.length === 0) return;

  pois.forEach((poi, idx) => {
    const m = L.marker([poi.latitude, poi.longitude], {
      icon: makePOIIcon(false),
      zIndexOffset: 100
    }).addTo(map);

    m.poiId = poi.id;

    // Bấm marker → gửi sự kiện về C#
    m.on('click', () => {
      window.location.href = 'tourguide://poi/' + poi.id;
    });

    poiMarkers.push(m);
  });

  // Fit bounds để thấy tất cả markers + user
  const allPoints = pois.map(p => [p.latitude, p.longitude]);
  if (userLatLng) allPoints.push(userLatLng);
  if (allPoints.length > 1) {
    map.fitBounds(L.latLngBounds(allPoints), { padding: [40, 40], maxZoom: 16 });
  }
}

function highlightPOI(id) {
  poiMarkers.forEach(m => {
    const isHigh = (m.poiId === id);
    m.setIcon(makePOIIcon(isHigh));
    m.setZIndexOffset(isHigh ? 1000 : 100);
  });
}
// Vẽ đường nối giữa 2 điểm (ví dụ: user → POI)
let routeLine = null;

async function drawRoute(lat1, lon1, lat2, lon2) {
  if (routeLine) {
    map.removeLayer(routeLine);
  }

  try {
    const url = `https://router.project-osrm.org/route/v1/driving/${lon1},${lat1};${lon2},${lat2}?overview=full&geometries=geojson`;

    const res = await fetch(url);
    const data = await res.json();

    if (!data.routes || data.routes.length === 0) return;

    const coords = data.routes[0].geometry.coordinates;

    // OSRM trả về [lon, lat] → phải đảo lại
    const latlngs = coords.map(c => [c[1], c[0]]);

    routeLine = L.polyline(latlngs, {
      color: '#2563EB',
      weight: 6,
      opacity: 0.9
    }).addTo(map);

    map.fitBounds(routeLine.getBounds(), { padding: [50, 50] });
  }
  catch (err) {
    console.error("Route error:", err);
  }
}
</script>
</body>
</html>
""";
}