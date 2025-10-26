/* PinTheHighwayCrash SW (minimal, SPA-friendly) */
const SW_VERSION = "ptc-v1";
const SHELL_CACHE = "ptc-shell-" + SW_VERSION;
const TILE_CACHE = "ptc-tiles-" + SW_VERSION;

const APP_SHELL = [
    "/",                 // root for SPA fallback
    "/index.html",
    "/css/app.css",
    "/js/mapInterop.js",
    "/js/healthInterop.js",
    "/js/shareInterop.js",
    "/js/offlineInterop.js",
];

/* Tile caching defaults; can be overridden via postMessage */
let cacheTiles = true;
let tileHosts = ["tile.openstreetmap.org"]; // matches a/b/c subdomains via endsWith
let maxTiles = 800;
let maxDays = 21;

self.addEventListener("install", (event) => {
    event.waitUntil((async () => {
        try {
            const cache = await caches.open(SHELL_CACHE);
            await cache.addAll(APP_SHELL);
        } catch (_) {
            // Don't fail install if a file is missing
        }
        await self.skipWaiting();
    })());
});

self.addEventListener("activate", (event) => {
    event.waitUntil((async () => {
        const keep = new Set([SHELL_CACHE, TILE_CACHE]);
        const names = await caches.keys();
        await Promise.all(names.filter(n => !keep.has(n)).map(n => caches.delete(n)));
        await self.clients.claim();
        if ("navigationPreload" in self.registration) {
            try { await self.registration.navigationPreload.enable(); } catch { }
        }
    })());
});

/* Runtime config from the app */
self.addEventListener("message", (e) => {
    const d = e.data || {};
    if (typeof d.CacheTiles === "boolean") cacheTiles = d.CacheTiles;
    if (Array.isArray(d.TileHosts)) tileHosts = d.TileHosts;
    if (typeof d.MaxCachedTiles === "number") maxTiles = d.MaxCachedTiles;
    if (typeof d.MaxTileAgeDays === "number") maxDays = d.MaxTileAgeDays;
});

self.addEventListener("fetch", (event) => {
    const req = event.request;
    if (req.method !== "GET") return;

    const url = new URL(req.url);
    const sameOrigin = url.origin === location.origin;

    // 1) SPA navigation requests: NetworkFirst, fallback to cached index.html
    if (req.mode === "navigate") {
        event.respondWith(handleSPARequest(event, req));
        return;
    }

    // 2) Same-origin static assets: NetworkFirst → Cache
    if (sameOrigin) {
        event.respondWith(networkFirstThenCache(req, SHELL_CACHE));
        return;
    }

    // 3) Map tiles: CacheFirst
    if (cacheTiles && tileHosts.some(h => url.hostname.endsWith(h))) {
        event.respondWith(cacheFirstTiles(req));
        return;
    }

    // 4) Default passthrough for everything else
});

/* ------- Strategies ------- */

async function handleSPARequest(event, req) {
    try {
        // Try navigation preload if available
        const preloaded = await event.preloadResponse;
        if (preloaded) {
            const cache = await caches.open(SHELL_CACHE);
            cache.put(req, preloaded.clone()).catch(() => { });
            return preloaded;
        }
    } catch { }

    try {
        const res = await fetch(req);
        const cache = await caches.open(SHELL_CACHE);
        cache.put(req, res.clone()).catch(() => { });
        return res;
    } catch {
        const cache = await caches.open(SHELL_CACHE);
        const fallback = await cache.match("/index.html", { ignoreVary: true });
        if (fallback) return fallback;
        return new Response("Offline", { status: 503, statusText: "Offline" });
    }
}

async function networkFirstThenCache(req, cacheName) {
    try {
        const res = await fetch(req);
        if (res && (res.ok || res.type === "opaqueredirect" || res.type === "opaque")) {
            const cache = await caches.open(cacheName);
            cache.put(req, res.clone()).catch(() => { });
        }
        return res;
    } catch {
        const cache = await caches.open(cacheName);
        const hit = await cache.match(req, { ignoreVary: true });
        if (hit) return hit;
        throw new Error("Offline and not cached");
    }
}

async function cacheFirstTiles(req) {
    const cache = await caches.open(TILE_CACHE);
    const cached = await cache.match(req, { ignoreVary: true });
    if (cached) return cached;

    try {
        const res = await fetch(req, { mode: "no-cors" }); // OSM allows tile fetch
        cache.put(req, res.clone()).catch(() => { });
        pruneTiles(cache).catch(() => { });
        return res;
    } catch {
        // 1x1 transparent PNG fallback
        return new Response(
            Uint8Array.from([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 12, 73, 68, 65, 84, 120, 156, 99, 96, 0, 0, 0, 2, 0, 1, 226, 33, 188, 33, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130]),
            { headers: { "Content-Type": "image/png" } }
        );
    }
}

async function pruneTiles(cache) {
    const keys = await cache.keys();
    if (keys.length > maxTiles) {
        const toDelete = keys.length - maxTiles;
        for (let i = 0; i < toDelete; i++) await cache.delete(keys[i]);
    }

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
