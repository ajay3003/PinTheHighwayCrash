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
        var plat = "unknown";
        try {
            if (navigator.userAgentData && navigator.userAgentData.platform) {
                plat = navigator.userAgentData.platform;
            } else if (navigator.userAgentData && navigator.userAgentData.getHighEntropyValues) {
                navigator.userAgentData.getHighEntropyValues(["platform"]).then(function (res) {
                    if (res && res.platform) plat = res.platform;
                });
            } else {
                if (/Windows/i.test(ua)) plat = "Windows";
                else if (/Mac/i.test(ua)) plat = "macOS";
                else if (/Linux/i.test(ua)) plat = "Linux";
                else if (/Android/i.test(ua)) plat = "Android";
                else if (/iPhone|iPad|iPod/i.test(ua)) plat = "iOS";
            }
        } catch (_) { }
        var lang = toStr(navigator.language || (navigator.languages && navigator.languages[0]));
        return { ua: ua, platform: plat, language: lang };
    }

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

    function probeOsmTile(timeoutMs) {
        return new Promise(function (resolve) {
            var t = timeoutMs || 5000;
            var done = false;
            var timer = setTimeout(function () {
                if (done) return;
                done = true;
                resolve({ ok: false, message: "Timed out after " + t + " ms" });
            }, t);
            var url = "https://a.tile.openstreetmap.org/1/1/1.png?_=" + Date.now();
            var img = new Image();
            img.onload = function () {
                if (done) return; done = true; clearTimeout(timer);
                resolve({ ok: true });
            };
            img.onerror = function () {
                if (done) return; done = true; clearTimeout(timer);
                resolve({ ok: false, message: "Tile request failed (network/CORS/firewall)" });
            };
            img.src = url;
        });
    }

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

    function scriptOrderLooksGood() {
        try {
            var scripts = document.getElementsByTagName("script");
            var sawInterop = false;
            for (var i = 0; i < scripts.length; i++) {
                var src = scripts[i].getAttribute("src") || "";
                if (src.indexOf("js/mapInterop.js") !== -1 || src.indexOf("js/healthInterop.js") !== -1)
                    sawInterop = true;
                if (src.indexOf("_framework/blazor.webassembly.js") !== -1 && !sawInterop)
                    return { ok: false, message: "Blazor script appears before interop scripts" };
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

        results.push({ name: "Browser", status: "info", detail: info.ua, fix: "" });
        results.push({ name: "Platform / Language", status: "info", detail: (info.platform || "unknown") + " / " + (info.language || "unknown"), fix: "" });

        var secure = isSecureOrigin();
        results.push({
            name: "Secure origin (HTTPS or localhost)",
            status: secure ? "pass" : "warn",
            detail: secure ? location.origin : "Origin: " + location.origin,
            fix: secure ? "" : "Run under HTTPS or localhost. Some browsers restrict geolocation on plain HTTP."
        });

        var order = scriptOrderLooksGood();
        results.push({
            name: "Script order",
            status: order.ok ? "pass" : "fail",
            detail: order.ok ? "Interop scripts load before Blazor" : (order.message || "Blazor may be loading before interop."),
            fix: order.ok ? "" : "Include mapInterop.js/healthInterop.js BEFORE _framework/blazor.webassembly.js in index.html"
        });

        var hasLjs = leafletJsLoaded();
        var hasLcss = leafletCssLoaded();
        results.push({ name: "Leaflet JS loaded", status: hasLjs ? "pass" : "fail", detail: hasLjs ? "L is defined" : "Leaflet JS not found", fix: hasLjs ? "" : "Ensure leaflet.js script is included before Blazor." });
        results.push({ name: "Leaflet CSS loaded", status: hasLcss ? "pass" : "warn", detail: hasLcss ? "leaflet.css detected" : "Leaflet CSS not detected", fix: hasLcss ? "" : "Include leaflet.css in <head> for proper map rendering." });

        var canLeaf = leafletCanInstantiate();
        results.push({
            name: "Leaflet can instantiate",
            status: canLeaf.ok ? "pass" : "fail",
            detail: canLeaf.ok ? "Map container created OK" : (canLeaf.message || "Failed to create map"),
            fix: canLeaf.ok ? "" : "Check console for Leaflet errors and ensure container exists."
        });

        var geo = hasGeolocation();
        results.push({
            name: "Geolocation API present",
            status: geo ? "pass" : "fail",
            detail: geo ? "navigator.geolocation available" : "navigator.geolocation is missing",
            fix: geo ? "" : "Use a modern browser (Edge/Chrome/Firefox) and avoid IE mode."
        });

        var clip = clipboardSupported();
        results.push({
            name: "Clipboard API",
            status: clip ? "pass" : "warn",
            detail: clip ? "navigator.clipboard available" : "Limited clipboard support",
            fix: clip ? "" : "Copy may be blocked in some contexts; use SMS/WhatsApp share instead."
        });

        var mobile = isMobileUA();
        results.push({
            name: "Device for tel:/sms:",
            status: mobile ? "pass" : "warn",
            detail: mobile ? "Looks like a mobile device" : "Desktop detected",
            fix: mobile ? "" : "Dial/SMS links may not work on desktop; test on a phone."
        });

        return queryPermission("geolocation").then(function (perm) {
            var state = (perm && perm.state) || "unknown";
            results.push({
                name: "Geolocation permission",
                status: state === "granted" ? "pass" : (state === "prompt" ? "info" : (state === "denied" ? "fail" : "info")),
                detail: "Permission state: " + state,
                fix: state === "denied" ? "Click lock icon → Allow location → Reload." : ""
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
                    fix: "Ensure OS location services are enabled or test on mobile."
                });
            }
            return probeOsmTile(4000);
        }).then(function (tile) {
            if (tile.ok) {
                results.push({ name: "OSM tile reachability", status: "pass", detail: "openstreetmap.org tile reachable", fix: "" });
            } else {
                results.push({
                    name: "OSM tile reachability",
                    status: "warn",
                    detail: tile.message || "Tile CDN may be blocked by firewall/proxy",
                    fix: "Ensure a|b|c.tile.openstreetmap.org is reachable."
                });
            }

            var overall = "pass";
            for (var i = 0; i < results.length; i++) {
                var st = results[i].status;
                if (st === "fail") { overall = "fail"; break; }
                if (st === "warn" && overall !== "fail") overall = "warn";
            }
            var durMs = Math.round(now() - startedAt);
            results.push({ name: "Health check duration", status: "info", detail: durMs + " ms", fix: "" });
            return { overall: overall, items: results };
        }).catch(function (e) {
            return {
                overall: "fail",
                items: [{ name: "Health check exception", status: "fail", detail: (e && e.message) ? e.message : "Unknown JS error", fix: "Open console for details and verify script order." }]
            };
        });
    }

    // Expose to Blazor
    window.healthInterop = {
        run: run,
        _isSecureOrigin: isSecureOrigin,
        _hasGeolocation: hasGeolocation,
        _leafletJsLoaded: leafletJsLoaded,
        _leafletCssLoaded: leafletCssLoaded,
        _leafletCanInstantiate: leafletCanInstantiate,
        _probeOsmTile: probeOsmTile,
        _testGetPosition: testGetPosition
    };
})();
