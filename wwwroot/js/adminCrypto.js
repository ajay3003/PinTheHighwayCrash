// AES-GCM + PBKDF2 helpers (no server needed)

const b2a = (b) => btoa(String.fromCharCode(...new Uint8Array(b)));
const a2b = (s) => Uint8Array.from(atob(s), c => c.charCodeAt(0));

export function randomB64(len = 16) {
    const a = new Uint8Array(len);
    crypto.getRandomValues(a);
    return b2a(a);
}

export async function deriveKEK(pass, saltB64, iterations = 300000) {
    const enc = new TextEncoder();
    const salt = a2b(saltB64);
    const mat = await crypto.subtle.importKey("raw", enc.encode(pass), "PBKDF2", false, ["deriveKey"]);
    return crypto.subtle.deriveKey(
        { name: "PBKDF2", hash: "SHA-256", salt, iterations },
        mat,
        { name: "AES-GCM", length: 256 },
        true,
        ["wrapKey", "unwrapKey"]
    );
}

export async function genDataKey() {
    return crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, true, ["encrypt", "decrypt"]);
}

export function newIvB64() {
    const iv = new Uint8Array(12);
    crypto.getRandomValues(iv);
    return b2a(iv);
}

export async function wrapKey(kek, dataKey) {
    // Wrap the raw AES key using AES-GCM with a random IV
    const iv = a2b(newIvB64());
    const jwk = await crypto.subtle.exportKey("raw", dataKey);
    const ct = await crypto.subtle.encrypt({ name: "AES-GCM", iv }, kek, jwk);
    return { iv: b2a(iv), wrapped: b2a(ct) };
}

export async function unwrapKey(kek, wrapped) {
    const iv = a2b(wrapped.iv);
    const ct = a2b(wrapped.wrapped);
    const raw = await crypto.subtle.decrypt({ name: "AES-GCM", iv }, kek, ct);
    const key = await crypto.subtle.importKey("raw", raw, { name: "AES-GCM" }, true, ["encrypt", "decrypt"]);
    return key;
}

export async function encryptJson(dataKey, obj) {
    const iv = a2b(newIvB64());
    const pt = new TextEncoder().encode(JSON.stringify(obj));
    const ct = await crypto.subtle.encrypt({ name: "AES-GCM", iv }, dataKey, pt);
    return { iv: b2a(iv), data: b2a(ct) };
}

export async function decryptJson(dataKey, pack) {
    const iv = a2b(pack.iv);
    const ct = a2b(pack.data);
    const pt = await crypto.subtle.decrypt({ name: "AES-GCM", iv }, dataKey, ct);
    return JSON.parse(new TextDecoder().decode(new Uint8Array(pt)));
}
