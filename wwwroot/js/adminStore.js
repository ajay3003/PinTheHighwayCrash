// Minimal IndexedDB key-value store + WebCrypto helpers
const DB_NAME = "pthc-admin";
const STORE = "kv";
let dbPromise;

function openDb() {
    if (!dbPromise) {
        dbPromise = new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, 1);
            req.onupgradeneeded = () => {
                const db = req.result;
                if (!db.objectStoreNames.contains(STORE)) db.createObjectStore(STORE);
            };
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
    }
    return dbPromise;
}

async function idbGet(key) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readonly");
        const st = tx.objectStore(STORE);
        const r = st.get(key);
        r.onsuccess = () => resolve(r.result ?? null);
        r.onerror = () => reject(r.error);
    });
}

async function idbSet(key, value) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, "readwrite");
        const st = tx.objectStore(STORE);
        const r = st.put(value, key);
        r.onsuccess = () => resolve(true);
        r.onerror = () => reject(r.error);
    });
}

// --- WebCrypto PBKDF2 (returns Base64)
async function pbkdf2Hash(password, saltBase64, iterations = 250000) {
    const enc = new TextEncoder();
    const salt = Uint8Array.from(atob(saltBase64), c => c.charCodeAt(0));
    const keyMaterial = await crypto.subtle.importKey(
        "raw", enc.encode(password), "PBKDF2", false, ["deriveBits", "deriveKey"]
    );
    const key = await crypto.subtle.deriveKey(
        { name: "PBKDF2", hash: "SHA-256", salt, iterations },
        keyMaterial, { name: "HMAC", hash: "SHA-256", length: 256 }, true, ["sign"]
    );
    const sig = await crypto.subtle.sign("HMAC", key, enc.encode("pthc-admin"));
    return btoa(String.fromCharCode(...new Uint8Array(sig)));
}

function randomSaltBase64(len = 16) {
    const arr = new Uint8Array(len);
    crypto.getRandomValues(arr);
    return btoa(String.fromCharCode(...arr));
}

export async function loadSettings() { return await idbGet("settings"); }
export async function saveSettings(json) { return await idbSet("settings", json); }

export async function getLock() { return await idbGet("adminLock"); }
export async function setLock(lock) { return await idbSet("adminLock", lock); }
export { pbkdf2Hash, randomSaltBase64 };
