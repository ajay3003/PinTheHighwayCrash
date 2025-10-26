// @ts-nocheck   // Tell VS/TypeScript to stop type-checking this JS file

// ---------------- Geolocation helpers (robust with hard timeout) ----------------
(function () {
    // Polyfill Object.assign if needed
    if (typeof Object.assign !== "function") {
        Object.assign = function (target) {
            if (target == null) throw new TypeError("Cannot convert undefined or null to object");
            target = Object(target);
            for (var i = 1; i < arguments.length; i++) {
                var source = arguments[i];
                if (source != null) {
                    for (var key in source) {
                        if (Object.prototype.hasOwnProperty.call(source, key)) {
                            target[key] = source[key];
                        }
                    }
                }
            }
            return target;
        };
    }

    // Legacy promise wrapper for backward compatibility
    if (!navigator.geolocation.getCurrentPositionPromise) {
        navigator.geolocation.getCurrentPositionPromise = function (options) {
            return new Promise(function (resolve, reject) {
                if (!navigator.geolocation) {
                    reject({ code: "UNSUPPORTED", message: "Geolocation API not supported." });
                    return;
                }
                var opts = Object.assign({ enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }, options || {});

                // Hard timeout guard (some envs ignore built-in timeout)
                var guard = setTimeout(function () {
                    reject({ code: "TIMEOUT", message: "Timed out waiting for a location fix." });
                }, opts.timeout || 10000);

                navigator.geolocation.getCurrentPosition(
                    function (pos) { clearTimeout(guard); resolve(pos); },
                    function (err) { clearTimeout(guard); reject(err); },
                    opts
                );
            });
        };
    }

    // Rich wrapper with friendlier errors + hard timeout
    if (!window.geoHelpers) window.geoHelpers = {};
    if (!window.geoHelpers.getPosition) {
        window.geoHelpers.getPosition = function (options) {
            return new Promise(function (resolve, reject) {
                if (!navigator.geolocation) {
                    reject({ code: "UNSUPPORTED", message: "Geolocation API not supported by this browser." });
                    return;
                }

                var opts = Object.assign({ enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }, options || {});
                var guard = setTimeout(function () {
                    reject({ code: "TIMEOUT", message: "Timed out waiting for a location fix." });
                }, opts.timeout || 10000);

                navigator.geolocation.getCurrentPosition(
                    function (pos) {
                        clearTimeout(guard);
                        resolve({
                            ok: true,
                            coords: {
                                latitude: pos.coords.latitude,
                                longitude: pos.coords.longitude,
                                accuracy: pos.coords.accuracy
                            }
                        });
                    },
                    function (err) {
                        clearTimeout(guard);
                        var map = {
                            1: { code: "PERMISSION_DENIED", message: "Permission denied by browser or policy." },
                            2: { code: "POSITION_UNAVAILABLE", message: "Position unavailable (OS location likely disabled by admin)." },
                            3: { code: "TIMEOUT", message: "Timed out waiting for a location fix." }
                        };
                        var known = (err && map[err.code]) || {
                            code: "UNKNOWN",
                            message: (err && err.message) ? err.message : "Unknown error."
                        };
                        reject(known);
                    },
                    opts
                );
            });
        };
    }
})();

// --------------------------- Leaflet map interop ------------------------------
window.mapInterop = (function () {
    var map, marker, _dotnetRef;

    function ensureLeaflet() {
        if (typeof L === "undefined") {
            throw new Error("Leaflet (L) is not loaded. Include leaflet.js and leaflet.css in index.html.");
        }
    }

    function initMap(divId, lat, lng, dotnetRef) {
        ensureLeaflet();
        _dotnetRef = dotnetRef;

        // If re-initialized, dispose previous instance
        if (map) {
            dispose();
        }

        map = L.map(divId, { zoomControl: true }).setView([lat, lng], 16);

        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap contributors"
        }).addTo(map);

        marker = L.marker([lat, lng], { draggable: true }).addTo(map);

        marker.on("dragend", function () {
            var p = marker.getLatLng();
            if (_dotnetRef) _dotnetRef.invokeMethodAsync("OnMapMarkerMoved", p.lat, p.lng);
        });

        map.on("click", function (e) {
            marker.setLatLng(e.latlng);
            if (_dotnetRef) _dotnetRef.invokeMethodAsync("OnMapMarkerMoved", e.latlng.lat, e.latlng.lng);
        });

        // Handle container resize (e.g., when sidebar toggles)
        setTimeout(function () {
            if (map) map.invalidateSize();
        }, 0);
    }

    function setView(lat, lng) {
        if (map) map.setView([lat, lng], 16);
        if (marker) marker.setLatLng([lat, lng]);
    }

    // Optional helpers (non-breaking)
    function setPin(lat, lng) {
        if (marker) marker.setLatLng([lat, lng]);
    }

    function getPin() {
        if (!marker) return null;
        var p = marker.getLatLng();
        return { lat: p.lat, lng: p.lng };
    }

    function dispose() {
        try {
            if (marker) {
                marker.off();
                marker.remove();
                marker = null;
            }
            if (map) {
                map.off();
                map.remove();
                map = null;
            }
            _dotnetRef = null;
        } catch (_) { /* no-op */ }
    }

    return {
        initMap: initMap,
        setView: setView,
        setPin: setPin,
        getPin: getPin,
        dispose: dispose
    };
})();
