window.mapInterop = (function () {
  let map, marker;

  function initMap(divId, lat, lng, dotnetRef) {
    map = L.map(divId, { zoomControl: true }).setView([lat, lng], 16);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);

    marker = L.marker([lat, lng], { draggable: true }).addTo(map);

    marker.on("dragend", () => {
      const p = marker.getLatLng();
      dotnetRef.invokeMethodAsync("OnMapMarkerMoved", p.lat, p.lng);
    });

    map.on("click", (e) => {
      marker.setLatLng(e.latlng);
      dotnetRef.invokeMethodAsync("OnMapMarkerMoved", e.latlng.lat, e.latlng.lng);
    });
  }

  function setView(lat, lng) {
    if (map) map.setView([lat, lng], 16);
    if (marker) marker.setLatLng([lat, lng]);
  }

  return { initMap, setView };
})();
