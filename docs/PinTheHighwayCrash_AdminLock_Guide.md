# PinTheHighwayCrash Admin Lock & Encrypted Settings Guide

## How the solution works

### 1. Passphrase → KEK (Key Encryption Key)
- The user enters a passphrase in **AdminLock.razor**.
- `AdminCryptoJs` uses PBKDF2 via WebCrypto to derive a KEK from the passphrase, salt, and iteration count.

### 2. Data Key (AES-GCM)
- On enrollment, a random AES-GCM data key is generated.
- The data key is **wrapped** (encrypted) with the KEK.
- The encrypted result `{iv, wrapped}` is saved along with salt and iteration metadata.
- The plain data key exists only in memory while the admin is unlocked.

### 3. Settings Encryption
- `SettingsService` calls `ISettingsStore` (backed by `EncryptedSettingsStore`).
- `EncryptedSettingsStore` uses the in-memory AES data key to:
  - **Encrypt** `AdminSettings` to `{iv, ciphertext}` and store it locally.
  - **Decrypt** it back when the admin unlocks.

### 4. Unlock Process
- When the user re-enters the passphrase, the KEK is re-derived.
- The wrapped key is **unwrapped** using the KEK, restoring the AES data key.
- The data key is held in-memory for decrypting and saving settings.

---

## What is Stored vs. Not Stored

| Type | Stored? | Description |
|------|----------|-------------|
| Salt / Iterations | ✅ Yes | Used to re-derive KEK |
| Wrapped Data Key | ✅ Yes | AES key encrypted with KEK |
| Encrypted Settings | ✅ Yes | `{iv, ciphertext}` JSON payload |
| Plain Data Key | ❌ No | Held in memory only |
| Passphrase | ❌ No | Never stored |

---

## How to Test

### A. Happy Path

1. **First-time Enrollment**
   - Navigate to `/admin` → see *Set Admin Passphrase*.
   - Enter ≥8 chars → **Save** → switches to *Unlock* or *Admin Panel*.

2. **Reload & Unlock**
   - Refresh → see *Admin Unlock* → enter same passphrase.
   - Expected: Admin UI appears.

3. **Change & Save Settings**
   - Modify a value (e.g., Geofence = 5km) → **Save**.
   - Refresh → Unlock → values should persist.

4. **Storage Inspection**
   - In DevTools → *Application → Storage*:
     - Lock entry: contains `salt`, `iterations`, wrapped key.
     - Settings entry: `{iv, ciphertext}` (no plaintext).

### B. Failure / Edge Cases

5. **Wrong Passphrase**
   - Enter wrong passphrase → “Wrong passphrase.” + backoff timer.

6. **Manual Relock**
   - Click *Lock* → returns to unlock screen → DataKey cleared.

7. **Corrupted Storage**
   - Delete settings entry → refresh → app resets to defaults.

8. **Lost Passphrase**
   - You cannot decrypt settings → use *Reset* to re-enroll.

---

## Developer Sanity Checks

- **Network Tab:** `adminCrypto.js` loads as module (HTTP 200).
- **Console:** No JS exceptions; log “Settings updated and saved.” if logging enabled.
- **Storage:** Encrypted payloads only, no cleartext JSON.

---

## Troubleshooting

| Issue | Likely Cause |
|-------|---------------|
| Values not persisted after unlock | `LoadAsync` not called after unlock or decryption failed |
| Save button does nothing | DataKeyHolder.Ref is null (locked state) |
| “Failed to fetch dynamically imported module” | Service Worker still active on localhost |
| Plaintext in storage | Encryption layer not applied or wrong key used |

---

## Security Recommendations

- Use ≥300,000 PBKDF2 iterations.
- Run only under HTTPS.
- Never log keys or passphrases.
- Always clear memory key on lock or logout.

---

## Optional Diagnostic Idea

Add a small *Self-Test* button in Admin UI that verifies:
- The encrypted payload exists in storage.
- It can be decrypted with the current AES key.
- Displays “✅ Encrypted settings integrity verified.” on success.
