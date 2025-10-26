# PinTheHighwayCrash — Data Flow & Message Handling (No Code)

This document explains **how the app works end‑to‑end** and how the **default emergency messages** configured in `appsettings.json` are used across **Phone, SMS, WhatsApp, and Email** — without going into code.

---

## 1) Startup & Configuration

1. The app loads in the browser (Blazor WebAssembly).
2. `wwwroot/appsettings.json` is read and bound into typed options via DI:
   - `EmergencyOptions` (phone, SMS intro, email, WhatsApp)
   - `GeoOptions` (accuracy thresholds, timeouts, on‑road verification preferences)
   - `MapOptions` (tile URL, fallback center, zoom)
   - `FeatureFlags` (show/hide Health or Diagnostics pages)
   - `LoggingOptions` (console verbosity, enable/disable console provider)
3. Razor components and services resolve these options via `IOptions<T>` — the same values are shared across the app.

**Result:** all components/services can read consistent configuration without hard‑coding.

---

## 2) Map & Location

**On the Report page:**
- The app requests **device location** via the browser’s geolocation API.
- If granted and successful → it centers the map and places a **draggable pin**.
- If blocked or unavailable → the app presents a **fallback parser** where the user can paste:
  - Raw coordinates (`lat,lng`),
  - A Google Maps link (`?q=lat,lng` or `/@lat,lng,zoomz`),
  - A **Plus Code** / place text, which is **forward‑geocoded** using Nominatim.

**Simplified flow:**

```
Device → Geolocation OK → Map pin set
              ↓
         Geolocation fails
              ↓
  User pastes link/text → Forward Geocode → Map pin set
```

---

## 3) Verification & Gating

- The app computes the **distance** between the user's GPS fix and the **pinned** location (Haversine).
- If `GeoOptions` says it must be close (e.g., ≤ 150 m), the **send actions are gated** until that is true.
- (Optional) **On‑road** check using Nominatim reverse geocoding provides a hint (on‑road / off‑road / unknown).
- The UI displays clear status badges and messages.

---

## 4) Emergency Contact Actions (How messages are constructed)

When the user taps a contact button, the app builds a **single message body** (used for SMS, WhatsApp, and Email body) and launches the appropriate system handler. **No server** is required; the OS handles the action.

### 4.1 What the message body contains
A consistent, human‑readable text composed of:
1. **Intro line** (from config):
   - `Emergency:SmsIntro` is used as the **first line** of the body.
2. **Pinned Location**: `lat,lng` (the accident location).
3. **Reporter GPS**: `lat,lng` with `±accuracy` (device fix).
4. **Road Status**: on‑road / off‑road / unknown.
5. **Map Link**: a Google Maps link to the **pinned** coordinates.
6. **Timestamp (UTC)**.

> This same body is reused for **SMS**, **WhatsApp**, and the **Email** body. Line breaks are preserved by URL‑encoding.

### 4.2 Channel‑specific details

- **Phone (tel:)**
  - **Uses:** `Emergency:Phone`
  - **Action:** Opens the dialer with the number. (No text body here.)

- **SMS (sms:)**
  - **Uses:** `Emergency:Phone` + **Message Body** (starts with `Emergency:SmsIntro`).
  - **Action:** Opens the SMS app with **recipient set** and **body prefilled**.

- **WhatsApp (wa.me)**
  - **Uses:** `Emergency:WhatsAppNumber` + **Message Body** (starts with `Emergency:SmsIntro`).
  - **Action:** Opens WhatsApp chat composer with **message prefilled** for that number.

- **Email (mailto:)**
  - **Uses:** `Emergency:Email` + `Emergency:EmailSubject` + **Message Body**.
  - **Action:** Opens the default email client with **To**, **Subject**, and **Body** prefilled.

**Summary mapping:**

| Channel   | Target                                | Text Used                                      |
|-----------|---------------------------------------|------------------------------------------------|
| Call      | `tel:{Phone}`                         | *(none — dialer only)*                         |
| SMS       | `sms:{Phone}?body={EncodedBody}`      | `SmsIntro` + body details                      |
| WhatsApp  | `https://wa.me/{Digits}?text={Body}`  | `SmsIntro` + body details                      |
| Email     | `mailto:{Email}?subject=...&body=...` | `EmailSubject` for subject; body as above      |

---

## 5) Offline & Service Worker

- A **service worker** caches the app shell (HTML, CSS, JS) on first load.
- Subsequent visits work even if the network is down.
- **Map tiles** from OpenStreetMap can be cached (bounded by a max count and age) so previously viewed areas still render offline.
- The app listens for **online/offline** events to show a **warning banner** and degrade gracefully (verification may be unavailable).

**Offline behavior:**

```
First visit (online) → Shell + tiles cached
Later visit (offline) → App loads from cache; tiles shown if cached; contact actions still launch OS apps
```

---

## 6) Logging & Feature Flags

- `LoggingOptions` controls browser console verbosity.
- `FeatureFlags` can enable/disable the Health page or Diagnostics panel without changing code — just toggle JSON.

---

## 7) End‑to‑End User Journey Snapshot

1. **Open app** → config loaded, options bound.
2. **Location** → GPS success **or** paste fallback → map pin set.
3. **Verify** → distance/road checks (if enabled).
4. **Report** → user taps **Call / SMS / WhatsApp / Email**.
5. **System app opens** with **prefilled info**, ready to send.

All of this runs **client‑side**; no backend is required to initiate contact.

---

## Appendix: Config keys referenced

From `appsettings.json`:

```json
{
  "Emergency": {
    "Phone": "112",
    "WhatsAppNumber": "+911234567890",
    "Email": "controlroom@example.in",
    "EmailSubject": "EMERGENCY: Highway accident",
    "SmsIntro": "EMERGENCY: Highway accident reported"
  },
  "Geo": { ... },
  "Map": { ... },
  "FeatureFlags": { ... },
  "Logging": { ... }
}
```

These values **flow into** the message channels exactly as described above.
