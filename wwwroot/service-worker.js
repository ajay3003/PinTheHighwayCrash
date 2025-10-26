/* PinTheHighwayCrash SW (minimal) */
const SW_VERSION = "ptc-v1";
const APP_SHELL = [
    "/",                           // root
    "/index.html",
    "/css/app.css",
    "/js/mapInterop.js",
    "/js/healthInterop.js",
    "/js/shareInterop.js",
    "/js/offlineInterop.js",
    "/_framework/blazor.webassembly.js",
    "/_framework/dotnet.js",
    // The rest of framework files will be requested and cached on first run
];

const SHELL_CACHE = "ptc-shell-" + SW_VERSION;
const TILE_CACHE = "ptc-tiles-" + SW_VERSION;

// Defaults; app can send overrides via postMessage
let cacheTiles = true;
let tileHosts = ["tile.openstreetmap.org"];
let maxTiles = 800;
let maxDays = 21;

self.addEventListener("install", (e) => {
    e.waitUntil(
        caches.open(SHELL_CACHE).then(c => c.addAll(APP_SHELL)).then(() => self.skipWaiting())
    );
});

self.addEventListener("activate", (e) => {
    e.waitUntil(
        (async () => {
            const names = await caches.keys();
            await Promise.all(
                names
                    .filter(n => ![SHELL_CACHE, TILE_CACHE].includes(n))
                    .map(n => caches.delete(n))
            );
            await self.clients.claim();
        })()
    );
});

// Receive runtime settings from the app (optional)
self.addEventListener("message", (e) => {
    const data = e.data || {};
    if (typeof data.CacheTiles === "boolean") cacheTiles = data.CacheTiles;
    if (Array.isArray(data.TileHosts)) tileHosts = data.TileHosts;
    if (typeof data.MaxCachedTiles === "number") maxTiles = data.MaxCachedTiles;
    if (typeof data.MaxTileAgeDays === "number") maxDays = data.MaxTileAgeDays;
});

self.addEventListener("fetch", (e) => {
    const url = new URL(e.request.url);

    // App shell: NetworkFirst -> Cache
    if (url.origin === location.origin) {
        e.respondWith(networkFirstThenCache(e.request, SHELL_CACHE));
        return;
    }

    // Tile caching (CacheFirst) if enabled and host matches
    if (cacheTiles && tileHosts.some(h => url.hostname.endsWith(h))) {
        e.respondWith(cacheFirstTiles(e.request));
        return;
    }
    // Default: passthrough
});

async function networkFirstThenCache(req, cacheName) {
    try {
        const res = await fetch(req);
        const cache = await caches.open(cacheName);
        cache.put(req, res.clone());
        return res;
    } catch {
        const cache = await caches.open(cacheName);
        const hit = await cache.match(req, { ignoreVary: true });
        if (hit) return hit;
        // Last resort: offline page? (not needed here)
        throw new Error("Offline and not cached");
    }
}

async function cacheFirstTiles(req) {
    const cache = await caches.open(TILE_CACHE);
    const cached = await cache.match(req, { ignoreVary: true });
    if (cached) return cached;

    try {
        const res = await fetch(req, { mode: "no-cors" }); // OSM allows tile fetch
        // Store and prune
        await cache.put(req, res.clone());
        pruneTiles(cache);
        return res;
    } catch {
        // No tile available: return a 1x1 transparent PNG
        return new Response(
            Uint8Array.from([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 12, 73, 68, 65, 84, 120, 156, 99, 96, 0, 0, 0, 2, 0, 1, 226, 33, 188, 33, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130]),
            { headers: { "Content-Type": "image/png" } }
        );
    }
}

async function pruneTiles(cache) {
    const keys = await cache.keys();
    if (keys.length <= maxTiles) return;

    // crude LRU-ish: delete oldest first
    // (Cache API has no timestamps; we approximate by order)
    const toDelete = keys.length - maxTiles;
    for (let i = 0; i < toDelete; i++) await cache.delete(keys[i]);

    // age-based purge (best-effort)
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
