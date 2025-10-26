// Registers a service worker (idempotent). Safe to call multiple times.
window.offlineInterop = (function () {
    async function register(path) {
        try {
            if (!('serviceWorker' in navigator)) return false;
            const reg = await navigator.serviceWorker.register(path, { scope: "/" });
            // Optional: wait until active the first time
            await navigator.serviceWorker.ready;
            return !!reg;
        } catch {
            return false;
        }
    }
    return { register };
})();
