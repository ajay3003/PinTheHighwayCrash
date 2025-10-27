// -----------------------------------------------------------------------------
// adminCrypto.js (ES module)
// AES-GCM + PBKDF2 helpers (browser-native, no server needed)
// Compatible with AdminCryptoJs (.NET interop layer)
// -----------------------------------------------------------------------------

// --- Base64 helpers -----------------------------------------------------------
const b2a = (b) => btoa(String.fromCharCode(...new Uint8Array(b)));
const a2b = (s) => Uint8Array.from(atob(s), c => c.charCodeAt(0));

// --- Random base64 ------------------------------------------------------------
export function randomB64(len = 16) {
    const a = new Uint8Array(len);
    crypto.getRandomValues(a);
    return b2a(a);
}

// --- PBKDF2-derived Key Encryption Key (KEK) ----------------------------------
// NOTE: usages changed to ["encrypt","decrypt"] because we use AES-GCM encrypt/decrypt.
export async function deriveKEK(pass, saltB64, iterations = 300000) {
    const enc = new TextEncoder();
    const salt = a2b(saltB64);
    const mat = await crypto.subtle.importKey("raw", enc.encode(pass), "PBKDF2", false, ["deriveKey"]);
    return crypto.subtle.deriveKey(
        { name: "PBKDF2", hash: "SHA-256", salt, iterations },
        mat,
        { name: "AES-GCM", length: 256 },
        false, // not extractable (kek)
        ["encrypt", "decrypt"]
    );
}

// --- Generate new AES-GCM 256-bit Data Key ------------------------------------
export async function genDataKey() {
    return crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, true, ["encrypt", "decrypt"]);
}

// --- IV helper (96-bit) -------------------------------------------------------
export function newIvB64() {
    const iv = new Uint8Array(12);
    crypto.getRandomValues(iv);
    return b2a(iv);
}

// --- Wrap (encrypt) AES key with KEK ------------------------------------------
export async function wrapKey(kek, dataKey) {
    const iv = a2b(newIvB64());
    const jwk = await crypto.subtle.exportKey("raw", dataKey);
    const ct = await crypto.subtle.encrypt({ name: "AES-GCM", iv }, kek, jwk);
    return { iv: b2a(iv), wrapped: b2a(ct) };
}

// --- Unwrap (decrypt) AES key with KEK ----------------------------------------
export async function unwrapKey(kek, wrapped) {
    if (!wrapped || typeof wrapped.iv !== "string" || typeof wrapped.wrapped !== "string") {
        throw new Error("Invalid wrapped key payload");
    }
    const iv = a2b(wrapped.iv);
    const ct = a2b(wrapped.wrapped);
    try {
        const raw = await crypto.subtle.decrypt({ name: "AES-GCM", iv }, kek, ct);
        const key = await crypto.subtle.importKey("raw", raw, { name: "AES-GCM" }, true, ["encrypt", "decrypt"]);
        return key;
    } catch {
        throw new Error("Failed to unwrap key (wrong passphrase or corrupted data).");
    }
}

// --- Encrypt JSON using AES-GCM -----------------------------------------------
export async function encryptJson(dataKey, obj) {
    const iv = a2b(newIvB64());
    const pt = new TextEncoder().encode(JSON.stringify(obj));
    const ct = await crypto.subtle.encrypt({ name: "AES-GCM", iv }, dataKey, pt);
    return { iv: b2a(iv), data: b2a(ct) };
}

// --- Decrypt JSON using AES-GCM -----------------------------------------------
export async function decryptJson(dataKey, pack) {
    if (!pack || typeof pack.iv !== "string" || typeof pack.data !== "string") {
        throw new Error("Invalid ciphertext payload");
    }
    const iv = a2b(pack.iv);
    const ct = a2b(pack.data);
    try {
        const pt = await crypto.subtle.decrypt({ name: "AES-GCM", iv }, dataKey, ct);
        return JSON.parse(new TextDecoder().decode(new Uint8Array(pt)));
    } catch {
        throw new Error("Decryption failed (bad key or tampered data).");
    }
}
