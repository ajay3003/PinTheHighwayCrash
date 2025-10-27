// @ts-nocheck   // Disable TypeScript checking — plain ES5 file

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

                // Hard timeout guard
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

    function safeInvokeDotNet(method, arg1, arg2) {
        try {
            if (_dotnetRef && typeof _dotnetRef.invokeMethodAsync === "function") {
                _dotnetRef.invokeMethodAsync(method, arg1, arg2);
            }
        } catch (_) { /* swallow — avoid unhandled promise rejections */ }
    }

    // Initialize Leaflet map
    function initMap(divId, lat, lng, dotnetRef, opts) {
        ensureLeaflet();
        _dotnetRef = dotnetRef || null;

        // DOM guard: bail quietly if container isn't in the DOM yet
        var el = document.getElementById(divId);
        if (!el) {
            // Avoid throwing; Blazor may call again after render
            console.warn("[mapInterop] map container not found:", divId);
            return false;
        }

        // If re-initialized, dispose previous instance
        if (map) {
            dispose();
        }

        opts = opts || {};
        var initialZoom = typeof opts.initialZoom === "number" ? opts.initialZoom : 16;
        var maxZoom = typeof opts.maxZoom === "number" ? opts.maxZoom : 19;
        var tileUrl = opts.tileUrl || "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
        var detectRetina = opts.detectRetina !== false; // default true
        var scrollZoom = opts.scrollWheelZoom !== false; // default true

        map = L.map(el, {
            zoomControl: true,
            scrollWheelZoom: scrollZoom
        }).setView([lat, lng], initialZoom);

        L.tileLayer(tileUrl, {
            maxZoom: maxZoom,
            detectRetina: detectRetina,
            attribution: "&copy; OpenStreetMap contributors"
        }).addTo(map);

        marker = L.marker([lat, lng], { draggable: true }).addTo(map);

        marker.on("dragend", function () {
            var p = marker.getLatLng();
            safeInvokeDotNet("OnMapMarkerMoved", p.lat, p.lng);
        });

        map.on("click", function (e) {
            marker.setLatLng(e.latlng);
            safeInvokeDotNet("OnMapMarkerMoved", e.latlng.lat, e.latlng.lng);
        });

        // Handle container resize (e.g., after sidebar open)
        setTimeout(function () {
            try { if (map) map.invalidateSize(); } catch (_) { /* no-op */ }
        }, 0);

        return true;
    }

    // Center map on given coordinates (keep current zoom)
    function setView(lat, lng) {
        try {
            if (map) map.setView([lat, lng], map.getZoom());
            if (marker) marker.setLatLng([lat, lng]);
        } catch (_) { /* no-op */ }
    }

    // Optional helpers
    function setPin(lat, lng) {
        try { if (marker) marker.setLatLng([lat, lng]); } catch (_) { /* no-op */ }
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

    // Public API exposed to Blazor
    return {
        initMap: initMap,
        setView: setView,
        setPin: setPin,
        getPin: getPin,
        dispose: dispose
    };
})();
