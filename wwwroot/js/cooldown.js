// wwwroot/js/cooldown.js
function removeAllWithPrefix(storage, prefix) {
    const keys = [];
    for (let i = 0; i < storage.length; i++) {
        const key = storage.key(i);
        if (key && key.startsWith(prefix)) keys.push(key);
    }
    for (const k of keys) storage.removeItem(k);
}

window.pthc = window.pthc || {};
window.pthc.cooldown = (function () {
    function getStorage(useLocal) { return useLocal ? localStorage : sessionStorage; }
    function nowMs() { return Date.now(); }

    function readRaw(s, k) { return s.getItem(k); }
    function readJson(s, k) {
        const r = s.getItem(k);
        if (!r) return null;       // NOTE: keep for backwards-compat (not used by C# now)
        try { return JSON.parse(r); } catch { return null; }
    }
    function write(s, k, v) { s.setItem(k, JSON.stringify(v)); }
    function remove(s, k) { s.removeItem(k); }

    return {
        // time
        nowMs,

        // raw string access (NEW: preferred for C# to avoid null→value-type casting issues)
        getRaw: (useLocal, key) => readRaw(getStorage(useLocal), key),

        // legacy JSON helpers (still here if you use them elsewhere)
        get: (useLocal, key) => readJson(getStorage(useLocal), key),
        set: (useLocal, key, obj) => write(getStorage(useLocal), key, obj),
        remove: (useLocal, key) => remove(getStorage(useLocal), key),
        removeAllWithPrefix: (useLocal, prefix) => removeAllWithPrefix(getStorage(useLocal), prefix)
    };
})();
