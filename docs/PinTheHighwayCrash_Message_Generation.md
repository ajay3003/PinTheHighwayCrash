# PinTheHighwayCrash — Message Generation & Templates

This document explains **how a pinned location becomes an emergency message** across Call, SMS, WhatsApp, and Email, and how to configure the message **without changing code**.

---

## 🔎 What this covers

- End‑to‑end **data flow** from map pin to message
- **Templates & placeholders** (what you can customize)
- **Examples** for India (works for any country)
- **Anonymous mode** behavior
- **Nearest services enrichment** (hospital & police)
- Offline behavior & graceful fallbacks
- Testing checklist

---

## 🧭 Data Flow (High Level)

```
Pin location (GPS or manual paste)
   ↓
Reverse geocode (road/area) — if online
   ↓
Nearest services lookup (hospital/police) — JSON or API
   ↓
Merge into message templates (SMS/WA/Email)
   ↓
Launch system app (tel:, sms:, wa.me, mailto:)
```

**Notes**

- Messaging actions are **system handled** on the device; the app has **no backend dependency**.
- When offline, reverse/forward geocoding is skipped, but the app still works with pinned coordinates and cached map tiles.

---

## ⚙️ Configuration (appsettings.json)

Add or adjust these fields under `Emergency`:

```jsonc
"Emergency": {
  // Used automatically when pressing CALL
  "Phone": "112",

  // WhatsApp target (digits extracted automatically)
  "WhatsAppNumber": "+911234567890",

  // Email subject/body (when pressing EMAIL)
  "EmailSubject": "Highway Accident Alert",
  "EmailBodyTemplate": "Accident near {roadName}, {area}.\nCoordinates: {lat},{lng}\nNearest hospital: {hospital}\nNearest police post: {police}\nTime: {timestamp}\n\nPlease dispatch assistance.",

  // SMS template (when pressing SMS)
  "SmsTemplate": "Accident near {roadName}, {area}. Coords: {lat},{lng}. Hospital: {hospital}. Police: {police}. Sent via PinTheHighwayCrash."
}
```

The UI will **merge runtime values** into these templates before launching the appropriate app.

---

## 🧩 Placeholders

| Placeholder    | Description                                              | Example                         |
|----------------|----------------------------------------------------------|---------------------------------|
| `{lat}`        | Pinned latitude (6 dp)                                   | `28.568900`                     |
| `{lng}`        | Pinned longitude (6 dp)                                  | `77.206600`                     |
| `{roadName}`   | Road or highway name (from reverse geocode)              | `AIIMS Flyover`                 |
| `{area}`       | Area/city/neighbourhood (from reverse geocode)           | `New Delhi`                     |
| `{hospital}`   | Nearest hospital name (from local JSON / API)            | `AIIMS Trauma Centre`           |
| `{police}`     | Nearest police post/PS (from local JSON / API)           | `PS Sarojini Nagar (100)`       |
| `{timestamp}`  | UTC timestamp (`yyyy-MM-dd HH:mm:ss`)                    | `2025-10-27 12:31:45`           |
| `{mapUrl}`     | Google Maps link for the pinned coordinates              | `https://maps.google.com/?q=…`  |

> If a value is **unknown** (e.g., offline), the app can omit that line or insert a fallback (e.g., `"Unknown"`).

---

## ✉️ Generated Messages (Examples)

**Pinned**: `28.5689, 77.2066` \
**Road**: `AIIMS Flyover` — **Area**: `New Delhi` \
**Nearest**: Hospital=`AIIMS Trauma Centre`, Police=`PS Sarojini Nagar (100)`

### 📱 SMS
```
Accident near AIIMS Flyover, New Delhi.
Coords: 28.5689,77.2066.
Hospital: AIIMS Trauma Centre. Police: PS Sarojini Nagar (100).
Sent via PinTheHighwayCrash.
```

### 💬 WhatsApp
```
🚨 Highway accident reported
📍 AIIMS Flyover, New Delhi
🧭 28.5689, 77.2066
🏥 AIIMS Trauma Centre
👮 PS Sarojini Nagar (100)
🗺️ https://maps.google.com/?q=28.568900,77.206600
— Sent via PinTheHighwayCrash
```

### 📧 Email
**Subject**: Highway Accident Alert  
**Body**
```
Accident near AIIMS Flyover, New Delhi.
Coordinates: 28.5689,77.2066
Nearest hospital: AIIMS Trauma Centre
Nearest police post: PS Sarojini Nagar (100)
Time (UTC): 2025-10-27 12:31:45

Please dispatch assistance.

— Sent via PinTheHighwayCrash
```

### 📞 Call
- Opens the dialer using `tel:{Phone}` (e.g., `tel:112`).

---

## 🕵️ Anonymous Mode (Optional)

If the sender selects **“Send Anonymously”**:
- Remove personal information (name/phone/notes) from message composition.
- Keep only location, road, and nearest services.
- The launch method (`sms:`, `mailto:`, `wa.me`, `tel:`) is unchanged.

This reduces hesitation in regions where users avoid contacting authorities.

---

## 🏥👮 Nearest Services Enrichment

**Goal**: Automatically suggest 1–2 nearby hospitals and a nearby police post **based on the pinned location**.

### Local JSON (offline‑friendly)
Store basic facility lists in `wwwroot/data/places.json`:

```json
{
  "hospitals": [
    { "name": "AIIMS Trauma Centre", "phone": "+9111XXXXXXX", "lat": 28.567, "lng": 77.206 },
    { "name": "Safdarjung Hospital", "phone": "+9111YYYYYYY", "lat": 28.566, "lng": 77.209 }
  ],
  "police": [
    { "name": "PS Sarojini Nagar", "phone": "100", "lat": 28.575, "lng": 77.199 }
  ]
}
```

At runtime, the app computes **Haversine distance** from the pin and picks the nearest items.

### API‑ready later
You can swap the JSON read with an **API call** (e.g., for India‑specific registries). The message templates **do not change**.

---

## 🌐 Online vs Offline

| Capability                  | Online | Offline |
|----------------------------|:------:|:------:|
| App launch (cached)        |   ✅   |   ✅   |
| Reverse/forward geocode    |   ✅   |   ❌   |
| Nearest services (JSON)    |   ✅   |   ✅   |
| New map tiles              |   ✅   |   ❌   |
| Call / SMS                 |   ✅   |   ✅   |
| WhatsApp / Email send      |   ✅   |   ⚠️ opens app; send when back online |

---

## ✅ Testing Checklist

- Pin a known location and verify the **road/area** appear in messages.
- Toggle airplane mode: app should **load from cache** and still allow **Call/SMS**.
- Paste coordinates/Maps links/Plus Codes and check pin movement.
- Confirm **nearest hospital/police** resolve from local JSON.
- Verify WhatsApp/Email **open** with prefilled text (send requires data).

---

## 🔒 Responsible Use Notes

- Avoid spamming authorities; consider **rate‑limits**, **cooldown** after sends, and optional **terms acknowledgement**.
- Make emergency numbers **region‑correct** via configuration.
- Respect OpenStreetMap/Nominatim usage policy (identify User‑Agent and contact email).

---

## 📎 Summary

- Messages are **template‑driven** and **data‑enriched**.
- Works **fully client‑side**, with **offline resilience**.
- Easy to localize for any region by editing **`appsettings.json`** and **`data/places.json`**.
- API‑friendly without redesigning message formats.

