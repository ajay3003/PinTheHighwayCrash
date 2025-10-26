// @ts-nocheck
// healthInterop.js — environment self-test for PinTheHighwayCrash (ES5-safe)

(function () {
    // ---------- tiny utils ----------
    function now() { return (typeof performance !== "undefined" && performance.now) ? performance.now() : Date.now(); }
    function has(obj, prop) { return Object.prototype.hasOwnProperty.call(obj, prop); }
    function toStr(x) { return x == null ? "" : String(x); }
    function safe(fn, fallback) { try { return fn(); } catch (_) { return fallback; } }
    function delay(ms) { return new Promise(function (res) { setTimeout(res, ms); }); }

    // ---------- environment helpers ----------
    function isSecureOrigin() {
        var h = location.hostname;
        return location.protocol === "https:" || h === "localhost" || h === "127.0.0.1";
    }

    function hasGeolocation() {
        return !!(typeof navigator !== "undefined" && navigator && navigator.geolocation);
    }

    function queryPermission(name) {
        try {
            if (!navigator.permissions || !navigator.permissions.query) {
                return Promise.resolve({ state: "unknown" });
            }
            return navigator.permissions.query({ name: name }).then(function (p) { return { state: p.state }; })
                .catch(function () { return { state: "unknown" }; });
        } catch (_) {
            return Promise.resolve({ state: "unknown" });
        }
    }

    function leafletJsLoaded() { return typeof L !== "undefined"; }
    function leafletCssLoaded() {
        try {
            var sheets = document.styleSheets;
            for (var i = 0; i < sheets.length; i++) {
                var href = sheets[i] && sheets[i].href;
                if (!href) continue;
                if (href.indexOf("leaflet.css") !== -1) return true;
            }
        } catch (_) { /* cross-origin stylesheets can throw */ }
        // fallback heuristic
        var links = document.getElementsByTagName("link");
        for (var j = 0; j < links.length; j++) {
            var lhref = links[j].getAttribute("href") || "";
            if (lhref.indexOf("leaflet.css") !== -1) return true;
        }
        return false;
    }

    function clipboardSupported() {
        return !!(navigator.clipboard && navigator.clipboard.writeText);
    }

    function isMobileUA() {
        var ua = (navigator.userAgent || "");
        return /Android|iPhone|iPad|iPod|Windows Phone|Mobile/i.test(ua);
    }

    function browserInfo() {
        var ua = toStr(navigator.userAgent);

        // Try to use the modern User-Agent Client Hints API
        var plat = "unknown";
        try {
            if (navigator.userAgentData && navigator.userAgentData.platform) {
                plat = navigator.userAgentData.platform;
            } else if (navigator.userAgentData && navigator.userAgentData.getHighEntropyValues) {
                // Asynchronously fetch high-entropy info (synchronously simplified here)
                // NOTE: Not awaiting because we only need a basic snapshot.
                navigator.userAgentData.getHighEntropyValues(["platform"]).then(function (res) {
                    if (res && res.platform) plat = res.platform;
                });
            } else {
                // No UA-CH available — derive from UA string (last resort)
                if (/Windows/i.test(ua)) plat = "Windows";
                else if (/Mac/i.test(ua)) plat = "macOS";
                else if (/Linux/i.test(ua)) plat = "Linux";
                else if (/Android/i.test(ua)) plat = "Android";
                else if (/iPhone|iPad|iPod/i.test(ua)) plat = "iOS";
            }
        } catch (_) { /* ignore */ }

        var lang = toStr(navigator.language || (navigator.languages && navigator.languages[0]));
        return { ua: ua, platform: plat, language: lang };
    }


    // Try creating a Leaflet map container sanity
    function leafletCanInstantiate() {
        if (!leafletJsLoaded()) return { ok: false, message: "Leaflet JS not loaded" };
        try {
            var el = document.createElement("div");
            el.style.cssText = "width:1px;height:1px;position:absolute;left:-9999px;top:-9999px;";
            document.body.appendChild(el);
            var m = L.map(el).setView([0, 0], 1);
            m.remove();
            document.body.removeChild(el);
            return { ok: true };
        } catch (e) {
            return { ok: false, message: e && e.message ? e.message : "Failed to create map" };
        }
    }

    // Optional OSM tile reachability probe (HEAD via Image)
    function probeOsmTile(timeoutMs) {
        return new Promise(function (resolve) {
            var t = timeoutMs || 5000;
            var done = false;
            var timer = setTimeout(function () {
                if (done) return;
                done = true;
                resolve({ ok: false, message: "Timed out after " + t + " ms" });
            }, t);

            // Random small tile (should exist)
            var url = "https://a.tile.openstreetmap.org/1/1/1.png?_=" + Date.now();
            var img = new Image();
            img.onload = function () { if (done) return; done = true; clearTimeout(timer); resolve({ ok: true }); };
            img.onerror = function () { if (done) return; done = true; clearTimeout(timer); resolve({ ok: false, message: "Tile request failed (network/CORS/firewall)" }); };
            img.src = url;
        });
    }

    // Geolocation live probe with hard timeout guard (never hangs)
    function testGetPosition(timeoutMs) {
        if (!hasGeolocation()) return Promise.resolve({ ok: false, message: "Geolocation API not available" });

        var t = timeoutMs || 8000;
        return new Promise(function (resolve) {
            var guard = setTimeout(function () {
                resolve({ ok: false, message: "Timed out after " + t + " ms" });
            }, t);

            navigator.geolocation.getCurrentPosition(
                function (pos) {
                    clearTimeout(guard);
                    resolve({
                        ok: true,
                        latitude: pos.coords.latitude,
                        longitude: pos.coords.longitude,
                        accuracy: pos.coords.accuracy
                    });
                },
                function (err) {
                    clearTimeout(guard);
                    var map = {
                        1: "Permission denied by browser/policy",
                        2: "Position unavailable (OS location may be disabled)",
                        3: "Timed out getting a fix"
                    };
                    resolve({ ok: false, message: map[err && err.code] || (err && err.message) || "Unknown error" });
                },
                { enableHighAccuracy: true, timeout: t, maximumAge: 0 }
            );
        });
    }

    // Check that Blazor scripts are ordered after interop (heuristic)
    function scriptOrderLooksGood() {
        try {
            var scripts = document.getElementsByTagName("script");
            var sawInterop = false, sawBlazor = false;
            for (var i = 0; i < scripts.length; i++) {
                var src = scripts[i].getAttribute("src") || "";
                if (src.indexOf("_framework/blazor.webassembly.js") !== -1) sawBlazor = true;
                if (src.indexOf("js/mapInterop.js") !== -1 || src.indexOf("js/healthInterop.js") !== -1) sawInterop = true;
                // If we hit Blazor before interop -> bad
                if (src.indexOf("_framework/blazor.webassembly.js") !== -1 && !sawInterop) return { ok: false, message: "Blazor script appears before interop scripts" };
            }
            return { ok: true };
        } catch (_) {
            return { ok: true };
        }
    }

    // ---------- public runner ----------
    function run(timeoutMs) {
        var results = [];
        var info = browserInfo();
        var startedAt = now();

        // Basic info (always info)
        results.push({
            name: "Browser",
            status: "info",
            detail: info.ua,
            fix: ""
        });
        results.push({
            name: "Platform / Language",
            status: "info",
            detail: (info.platform || "unknown") + " / " + (info.language || "unknown"),
            fix: ""
        });

        // Secure origin
        var secure = isSecureOrigin();
        results.push({
            name: "Secure origin (HTTPS or localhost)",
            status: secure ? "pass" : "warn",
            detail: secure ? location.origin : "Origin: " + location.origin,
            fix: secure ? "" : "Run under HTTPS or localhost. Some browsers restrict geolocation on plain HTTP."
        });

        // Script order
        var order = scriptOrderLooksGood();
        results.push({
            name: "Script order",
            status: order.ok ? "pass" : "fail",
            detail: order.ok ? "Interop scripts load before Blazor" : (order.message || "Blazor may be loading before interop."),
            fix: order.ok ? "" : "In index.html, include mapInterop.js/healthInterop.js BEFORE _framework/blazor.webassembly.js"
        });

        // Leaflet JS & CSS
        var hasLjs = leafletJsLoaded();
        var hasLcss = leafletCssLoaded();
        results.push({
            name: "Leaflet JS loaded",
            status: hasLjs ? "pass" : "fail",
            detail: hasLjs ? "L is defined" : "Leaflet JS not found",
            fix: hasLjs ? "" : "Ensure leaflet.js script is included before Blazor."
        });
        results.push({
            name: "Leaflet CSS loaded",
            status: hasLcss ? "pass" : "warn",
            detail: hasLcss ? "leaflet.css detected" : "Leaflet CSS not detected",
            fix: hasLcss ? "" : "Include leaflet.css in <head> for proper map rendering."
        });

        // Leaflet instantiate
        var canLeaf = leafletCanInstantiate();
        results.push({
            name: "Leaflet can instantiate",
            status: canLeaf.ok ? "pass" : "fail",
            detail: canLeaf.ok ? "Map container created OK" : (canLeaf.message || "Failed to create map"),
            fix: canLeaf.ok ? "" : "Check console for Leaflet errors and ensure container exists with dimensions."
        });

        // Geolocation API presence
        var geo = hasGeolocation();
        results.push({
            name: "Geolocation API present",
            status: geo ? "pass" : "fail",
            detail: geo ? "navigator.geolocation available" : "navigator.geolocation is missing",
            fix: geo ? "" : "Use a modern browser (Edge/Chrome/Firefox) and avoid legacy IE/compat mode."
        });

        // Clipboard
        var clip = clipboardSupported();
        results.push({
            name: "Clipboard API",
            status: clip ? "pass" : "warn",
            detail: clip ? "navigator.clipboard available" : "Limited clipboard support",
            fix: clip ? "" : "Copy may be blocked in some contexts; use SMS/WhatsApp share instead."
        });

        // Device heuristic
        var mobile = isMobileUA();
        results.push({
            name: "Device for tel:/sms:",
            status: mobile ? "pass" : "warn",
            detail: mobile ? "Looks like a mobile device" : "Desktop detected",
            fix: mobile ? "" : "Dial/SMS links may not work on desktop; test on a phone."
        });

        // Chain: permission -> live geolocation -> OSM tile probe
        return queryPermission("geolocation").then(function (perm) {
            var state = (perm && perm.state) || "unknown";
            results.push({
                name: "Geolocation permission",
                status: state === "granted" ? "pass" : (state === "prompt" ? "info" : (state === "denied" ? "fail" : "info")),
                detail: "Permission state: " + state,
                fix: state === "denied" ? "Click the lock icon in the address bar and set Location to Allow, then reload." : ""
            });

            return testGetPosition(timeoutMs || 8000);
        }).then(function (probe) {
            if (probe.ok) {
                results.push({
                    name: "Live geolocation probe",
                    status: "pass",
                    detail: "lat " + probe.latitude.toFixed(5) + ", lng " + probe.longitude.toFixed(5) + " (±" + Math.round(probe.accuracy) + " m)",
                    fix: ""
                });
            } else {
                results.push({
                    name: "Live geolocation probe",
                    status: "fail",
                    detail: probe.message || "Failed to get a fix",
                    fix: "If on Windows, ensure Location Services is enabled (may be blocked by admin). Otherwise try mobile device."
                });
            }

            return probeOsmTile(4000);
        }).then(function (tile) {
            if (tile.ok) {
                results.push({
                    name: "OSM tile reachability",
                    status: "pass",
                    detail: "openstreetmap.org tile reachable",
                    fix: ""
                });
            } else {
                results.push({
                    name: "OSM tile reachability",
                    status: "warn",
                    detail: tile.message || "Tile CDN may be blocked by firewall/proxy",
                    fix: "Ensure a|b|c.tile.openstreetmap.org is reachable or use a different tile provider."
                });
            }

            // Overall severity
            var overall = "pass";
            for (var i = 0; i < results.length; i++) {
                var st = results[i].status;
                if (st === "fail") { overall = "fail"; break; }
                if (st === "warn" && overall !== "fail") overall = "warn";
            }

            // Duration info (debug)
            var durMs = Math.round(now() - startedAt);
            results.push({
                name: "Health check duration",
                status: "info",
                detail: durMs + " ms",
                fix: ""
            });

            return { overall: overall, items: results };
        }).catch(function (e) {
            // Never throw back to .NET — return a safe fallback
            return {
                overall: "fail",
                items: [{
                    name: "Health check exception",
                    status: "fail",
                    detail: (e && e.message) ? e.message : "Unknown JS error",
                    fix: "Open DevTools console for details. Verify script order and CSP."
                }]
            };
        });
    }

    // Expose public API
    window.healthInterop = {
        run: run,
        // Optional: individual checks if you ever need them
        _isSecureOrigin: isSecureOrigin,
        _hasGeolocation: hasGeolocation,
        _leafletJsLoaded: leafletJsLoaded,
        _leafletCssLoaded: leafletCssLoaded,
        _leafletCanInstantiate: leafletCanInstantiate,
        _probeOsmTile: probeOsmTile,
        _testGetPosition: testGetPosition
    };
})();
