window.pthc = window.pthc || {};
window.pthc.cooldown = (function () {
    function getStorage(useLocal) { return useLocal ? localStorage : sessionStorage; }
    function nowMs() { return Date.now(); }
    function read(s, k) { const r = s.getItem(k); if (!r) return null; try { return JSON.parse(r); } catch { return null; } }
    function write(s, k, v) { s.setItem(k, JSON.stringify(v)); }
    function remove(s, k) { s.removeItem(k); }
    return {
        nowMs,
        get: (useLocal, key) => read(getStorage(useLocal), key),
        set: (useLocal, key, obj) => write(getStorage(useLocal), key, obj),
        remove: (useLocal, key) => remove(getStorage(useLocal), key)
    };
})();
