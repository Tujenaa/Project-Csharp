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

// type: 'selected' = Cam (bấm thủ công), 'nearest' = Tím (tự động gần nhất), false = Đỏ (bình thường)
function makePOIIcon(type = false) {
  let bgColor, scale;
  if (type === 'selected') {
    bgColor = '#F59E0B'; // Cam – người dùng bấm vào
    scale = 'scale(1.25)';
  } else if (type === 'nearest') {
    bgColor = '#512BD4'; // Tím – điểm gần nhất tự động
    scale = 'scale(1.2)';
  } else {
    bgColor = '#EF4444'; // Đỏ – bình thường
    scale = 'scale(1)';
  }
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

/// Xóa POI markers
function clearMarkers() {
  poiMarkers.forEach(m => map.removeLayer(m));
  poiMarkers = [];
}

/// Xóa các đường chỉ đường
function clearDirections() {
  if (routeLine) { map.removeLayer(routeLine); routeLine = null; }
  tourLegs.forEach(l => map.removeLayer(l));
  tourLegs = [];
}

/// Xóa tất cả (markers + directions)
function clearRoutes() {
  clearMarkers();
  clearDirections();
}

/// Đặt markers POI – KHÔNG vẽ đường nối, KHÔNG auto nav route
function setPOIs(jsonArray) {
  clearMarkers();

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

// Highlight khi người dùng bấm chọn thủ công → màu Cam
function highlightPOI(id) {
  poiMarkers.forEach(m => {
    const isSelected = (m.poiId === id);
    m.setIcon(makePOIIcon(isSelected ? 'selected' : false));
    m.setZIndexOffset(isSelected ? 1000 : 100);
  });
}

// Highlight điểm gần nhất tự động → màu Tím
function highlightNearest(id) {
  poiMarkers.forEach(m => {
    const isNearest = (m.poiId === id);
    m.setIcon(makePOIIcon(isNearest ? 'nearest' : false));
    m.setZIndexOffset(isNearest ? 500 : 100);
  });
}
// ── Routing ──────────────────────────────────────────────────────────────────
let routeLine = null;
let tourLegs = [];
const colorActive = '#2563EB'; // Xanh biển - Đang tới
const colorVisited = '#94A3B8'; // Xám - Đã qua

async function drawTourRoute(coordsJson) {
  // Clear cũ
  if (routeLine) { map.removeLayer(routeLine); routeLine = null; }
  tourLegs.forEach(l => map.removeLayer(l));
  tourLegs = [];

  const points = typeof coordsJson === 'string' ? JSON.parse(coordsJson) : coordsJson;
  if (!points || points.length < 2) return;

  const coordsStr = points.map(p => `${p[0]},${p[1]}`).join(';');
  // Sử dụng OSRM Foot profile từ OpenStreetMap.de cho chính xác
  const url = `https://routing.openstreetmap.de/routed-foot/route/v1/foot/${coordsStr}?overview=full&geometries=geojson`;

  try {
    const res = await fetch(url);
    const data = await res.json();
    if (!data.routes || data.routes.length === 0) return;

    const route = data.routes[0];
    
    // Nếu chỉ có 2 điểm (user -> 1 POI), OSRM trả về 1 leg.
    // Nếu n điểm, trả về n-1 legs.
    route.legs.forEach((leg, index) => {
      // Trích xuất tọa độ từ leg (hoặc từ full geometry nếu leg không có geometry riêng)
      // OSRM full geometry chứa tất cả, legs chứa info từng chặng.
      // Để đơn giản, ta trích xuất từng phần của geometry dựa trên annotation hoặc index.
      // Ở đây ta dùng annotation (nếu có) hoặc split đơn giản.
      // Tuy nhiên, OSRM API mặc định trả về full geometry.
      // Để tách legs chính xác nhất, ta nên dùng kết quả từ 'steps=true' hoặc 'annotations=true'.
    });

    // Cách đơn giản và hiệu quả nhất để vẽ từng chặng:
    // Vẽ full đường đi trước, hoặc chia nhỏ mảng coordinates.
    const fullCoords = route.geometry.coordinates.map(c => [c[1], c[0]]);
    
    // Tìm các điểm nút (waypoints) trong mảng geometry để cắt tỉa
    // Do OSRM trả về geometry liên tục, ta sẽ tạo các Polyline cho từng chặng.
    // Leg 0: Waypoint 0 -> Waypoint 1
    // Leg 1: Waypoint 1 -> Waypoint 2 ...
    
    // Vì Leaflet cho phép vẽ nhiều chặng, ta sẽ lưu lại
    // Tạm thời vẽ 1 đường polyline duy nhất nếu xử lý cắt chặng quá phức tạp, 
    // NHƯNG user yêu cầu đổi màu nên ta sẽ dùng logic:
    // Vẽ nguyên con đường Xanh, sau đó đè đường Xám lên phần đã đi.
    
    routeLine = L.polyline(fullCoords, {
      color: colorActive,
      weight: 6,
      opacity: 0.8,
      lineJoin: 'round'
    }).addTo(map);

    map.fitBounds(routeLine.getBounds(), { padding: [50, 50] });
  } catch (err) {
    console.error("OSRM Error:", err);
  }
}

// Cập nhật tiến độ: đổi màu đoạn đường đã đi
// visitedIndex: index của POI người dùng vừa chạm tới trong mảng points
async function updateProgress(visitedIndex, pointsJson) {
  if (!routeLine) return;

  // Xóa các lớp đè cũ
  tourLegs.forEach(l => map.removeLayer(l));
  tourLegs = [];

  const points = typeof pointsJson === 'string' ? JSON.parse(pointsJson) : pointsJson;
  if (!points || visitedIndex < 0) return;

  // Lấy các điểm từ đầu đến visitedIndex
  const visitedPoints = points.slice(0, visitedIndex + 1);
  if (visitedPoints.length < 2) return;

  const coordsStr = visitedPoints.map(p => `${p[0]},${p[1]}`).join(';');
  const url = `https://routing.openstreetmap.de/routed-foot/route/v1/foot/${coordsStr}?overview=full&geometries=geojson`;

  try {
    const res = await fetch(url);
    const data = await res.json();
    if (data.routes && data.routes.length > 0) {
      const visitedCoords = data.routes[0].geometry.coordinates.map(c => [c[1], c[0]]);
      const grayLine = L.polyline(visitedCoords, {
        color: colorVisited,
        weight: 6,
        opacity: 0.7
      }).addTo(map);
      tourLegs.push(grayLine);
    }
  } catch(e) {}
}

async function drawRoute(lat1, lon1, lat2, lon2) {
  await drawTourRoute([[lon1, lat1], [lon2, lat2]]);
}
</script>
</body>
</html>
""";
}