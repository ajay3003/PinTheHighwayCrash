/* wwwroot/service-worker.js
   PinTheHighwayCrash SW (SPA-friendly, cache-busting, tile caching)
   Bump SW_VERSION on each deploy to force an update.
*/
const SW_VERSION = "ptc-v2"; // ← increment each release
const SHELL_CACHE = "ptc-shell-" + SW_VERSION;
const TILE_CACHE = "ptc-tiles-" + SW_VERSION;

// Figure out base path (works for GitHub Pages subpaths like /PinTheHighwayCrash/)
const SCOPE = self.registration.scope; // e.g. https://user.github.io/PinTheHighwayCrash/
const ORIGIN = new URL(SCOPE).origin; // e.g. https://user.github.io
const BASE = SCOPE.replace(ORIGIN, "").replace(/\/$/, ""); // e.g. /PinTheHighwayCrash

// Helper to prefix app-relative urls with BASE
const withBase = (p) => (BASE || "") + p;

// App shell to pre-cache (only your own static assets; Blazor framework caches itself)
const APP_SHELL = [
    withBase("/"),
    withBase("/index.html"),
    withBase("/css/app.css"),
    withBase("/js/mapInterop.js"),
    withBase("/js/healthInterop.js"),
    withBase("/js/shareInterop.js"),
    withBase("/js/offlineInterop.js"),
];

// ------- Runtime tile cache options (overridable by postMessage) -------
let cacheTiles = true;
let tileHosts = ["tile.openstreetmap.org"]; // matches a/b/c via endsWith
let maxTiles = 800;  // max cached tiles
let maxDays = 21;   // eviction by age (best-effort)

// ---------------------- Install / Activate ----------------------
self.addEventListener("install", (event) => {
    event.waitUntil((async () => {
        try {
            const cache = await caches.open(SHELL_CACHE);
            await cache.addAll(APP_SHELL.map(u => new Request(u, { cache: "reload" })));
        } catch { /* don't fail install if a file is missing */ }
        await self.skipWaiting(); // take over ASAP
    })());
});

self.addEventListener("activate", (event) => {
    event.waitUntil((async () => {
        // Drop old caches
        const keep = new Set([SHELL_CACHE, TILE_CACHE]);
        const names = await caches.keys();
        await Promise.all(names.filter(n => !keep.has(n)).map(n => caches.delete(n)));

        // Enable navigation preload if supported
        if ("navigationPreload" in self.registration) {
            try { await self.registration.navigationPreload.enable(); } catch { /* ignore */ }
        }

        await self.clients.claim(); // start controlling pages immediately
    })());
});

// Allow the page to push settings and to request skipWaiting()
self.addEventListener("message", (event) => {
    const d = event.data || {};
    if (d && d.type === "SKIP_WAITING") {
        self.skipWaiting();
        return;
    }
    if (typeof d.CacheTiles === "boolean") cacheTiles = d.CacheTiles;
    if (Array.isArray(d.TileHosts)) tileHosts = d.TileHosts;
    if (typeof d.MaxCachedTiles === "number") maxTiles = d.MaxCachedTiles;
    if (typeof d.MaxTileAgeDays === "number") maxDays = d.MaxTileAgeDays;
});

// --------------------------- Fetch strategy ---------------------------
self.addEventListener("fetch", (event) => {
    const req = event.request;
    if (req.method !== "GET") return;

    const url = new URL(req.url);
    const sameOrigin = url.origin === ORIGIN;

    // 1) SPA navigations: Network-first, fallback to cached index.html
    if (req.mode === "navigate") {
        event.respondWith(handleSPARequest(event, req));
        return;
    }

    // 2) Same-origin static assets: cache-first for speed; respect ?v= busting naturally
    if (sameOrigin) {
        event.respondWith(cacheFirstStatic(req, SHELL_CACHE));
        return;
    }

    // 3) Map tiles (OSM): cache-first with size/age control
    if (cacheTiles && tileHosts.some(h => url.hostname.endsWith(h))) {
        event.respondWith(cacheFirstTiles(req));
        return;
    }

    // 4) Default passthrough
});

// --------------------------- Handlers ---------------------------

async function handleSPARequest(event, req) {
    try {
        // Navigation preload is fastest if available
        const preloaded = await event.preloadResponse;
        if (preloaded) {
            const cache = await caches.open(SHELL_CACHE);
            cache.put(req, preloaded.clone()).catch(() => { });
            return preloaded;
        }
    } catch { /* ignore */ }

    try {
        const res = await fetch(req);
        const cache = await caches.open(SHELL_CACHE);
        cache.put(req, res.clone()).catch(() => { });
        return res;
    } catch {
        // Offline fallback to index.html from cache (respect BASE)
        const cache = await caches.open(SHELL_CACHE);
        const fallback = await cache.match(withBase("/index.html"), { ignoreVary: true });
        return fallback || new Response("Offline", { status: 503, statusText: "Offline" });
    }
}

async function cacheFirstStatic(req, cacheName) {
    const cache = await caches.open(cacheName);
    const hit = await cache.match(req, { ignoreVary: true });
    if (hit) return hit;

    const res = await fetch(req).catch(() => undefined);
    if (res && (res.ok || res.type === "opaqueredirect" || res.type === "opaque")) {
        cache.put(req, res.clone()).catch(() => { });
    }
    return res || new Response("Offline", { status: 503 });
}

async function cacheFirstTiles(req) {
    const cache = await caches.open(TILE_CACHE);
    const hit = await cache.match(req, { ignoreVary: true });
    if (hit) return hit;

    try {
        const res = await fetch(req, { mode: "no-cors" });
        cache.put(req, res.clone()).catch(() => { });
        pruneTiles(cache).catch(() => { });
        return res;
    } catch {
        // 1x1 transparent PNG
        return new Response(
            Uint8Array.from([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 12, 73, 68, 65, 84, 120, 156, 99, 96, 0, 0, 0, 2, 0, 1, 226, 33, 188, 33, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130]),
            { headers: { "Content-Type": "image/png" } }
        );
    }
}

async function pruneTiles(cache) {
    // enforce max entries
    const keys = await cache.keys();
    if (keys.length > maxTiles) {
        const toDelete = keys.length - maxTiles;
        for (let i = 0; i < toDelete; i++) await cache.delete(keys[i]);
    }

    // best-effort age eviction based on Date header (if present)
    const now = Date.now();
    const maxAge = maxDays * 24 * 60 * 60 * 1000;
    for (const k of await cache.keys()) {
        const res = await cache.match(k);
        const date = res?.headers?.get("Date");
        if (date) {
            const age = now - new Date(date).getTime();
            if (age > maxAge) await cache.delete(k);
        }
    }
}
