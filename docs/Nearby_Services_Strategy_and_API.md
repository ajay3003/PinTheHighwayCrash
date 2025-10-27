# Nearby Services Strategy — Offline JSON First, API-Ready Integration

**Project:** PinTheHighwayCrash  
**Goal:** Suggest the *nearest 1–2 hospitals and 1–2 police posts* based on the **pinned accident location**, working **offline** via a local JSON dataset today, and **seamlessly switchable** to an online API later with the same data shape.

---

## 1) Why this approach

- **Field-ready & resilient:** Works **offline** using a cached JSON dataset (and cached map tiles).  
- **Low-risk rollout:** Start with curated JSON per country/region; iterate on data quality before wiring live APIs.  
- **Zero-breaking-change path to API:** UI and services consume the **same schema**, regardless of source (JSON vs API).

---

## 2) Data model (shared by JSON and API)

A single, portable schema — identical for local JSON and API responses.

```jsonc
{
  "version": 1,
  "country": "IN",                    // ISO2 of the dataset's focus (optional if global)
  "updatedUtc": "2025-01-01T00:00:00Z",
  "pois": [
    {
      "id": "h_aiims_trauma",
      "type": "hospital",             // "hospital" | "police"
      "name": "AIIMS Trauma Centre",
      "phone": "+9111XXXXXXXX",
      "lat": 28.56891,
      "lng": 77.20663,
      "addr": "Ring Rd, New Delhi",
      "extras": {
        "twentyFourSeven": true,      // hint for ranking
        "level": "tertiary",          // "primary" | "secondary" | "tertiary"
        "jurisdiction": null,         // used by police (if known)
        "source": "osm:node/123456"   // provenance (OSM/CSV/etc.)
      }
    }
  ]
}
```

> **Notes**
> - `phone` may contain country code or short codes (e.g., `100`), normalize at use-time.
> - `extras` is open for future enrichment without schema churn (triage capability, trauma centers, etc.).

---

## 3) Client config (no UI change needed)

Add to `appsettings.json`:

```json
"NearbyLookup": {
  "Enabled": true,
  "DataFile": "data/emergency_poi_in.json",   // local JSON (cached by service worker)
  "MaxRadiusKm": 40,
  "MaxResultsPerType": 2,
  "Prefer247Hospitals": true,
  "HospitalLevelOrder": ["tertiary","secondary","primary"]
}
```

Switching country is swapping `DataFile` (e.g., `emergency_poi_no.json`) or toggling to API mode (below).

---

## 4) Ranking logic (deterministic, explainable)

**Hospitals:**
1. `extras.twentyFourSeven` = true first (if `Prefer247Hospitals`).
2. `extras.level` priority according to `HospitalLevelOrder` (tertiary > secondary > primary).
3. Distance (Haversine) ascending.

**Police:**
- Distance ascending. Optional tie-breakers: `extras.jurisdiction` match, then name A→Z.

Return **top N per type** (default 2) **within MaxRadiusKm** (default 40 km).  
If none found, show universal fallback (**112 / 100**) and a short notice.

---

## 5) UX placement

- On the **Report** page, below the map:
  - **Nearby help** (from offline index; may be approximate)
    - *Hospitals* → Name • Address • **Call**
    - *Police* → Name • Address • **Call**
- Respect **Anonymous** mode: never prefill sender-identifying text; only include pinned coordinates & incident note.

---

## 6) Offline-first behavior

- **Local JSON** is served and cached by the Service Worker.  
- **Map tiles** are cached; previously viewed areas render offline.  
- If online checks (reverse/forward geocode) fail, UI still functions with local data.

---

## 7) Data sourcing (to build JSON)

- **Now:** Export from OpenStreetMap/Overpass (e.g., `amenity=hospital`, `amenity=police`), clean phone numbers, add 24/7/level if known.
- **Later:** Automate via weekly job to refresh the JSON file or host an API that emits the **same schema**.

---

## 8) API design (drop-in replacement)

Two forms are supported without UI change:

### 8.1) **Static JSON endpoint** (CDN or blob)
- **GET** `/emergency-poi/{country}.json`
- **Response:** Entire document in the **same schema** as local file (see §2).  
- **Caching:** Long `Cache-Control` with ETag; client keeps latest in IndexedDB/Cache Storage.

### 8.2) **Query API** (filtered on the server)
- **GET** `/api/nearby`
- **Query params:**
  - `lat` (double, required)
  - `lng` (double, required)
  - `radiusKm` (int, optional; default 40)
  - `maxHospitals` (int, optional; default 2)
  - `maxPolice` (int, optional; default 2)
  - `country` (string, optional; ISO2)
- **Response 200 (application/json):**
```json
{
  "version": 1,
  "updatedUtc": "2025-01-01T00:00:00Z",
  "pois": [
    // Same POI objects as schema §2, already filtered and ranked
  ]
}
```
- **Errors:**
  - `400` invalid/missing lat/lng
  - `429` rate-limited
  - `5xx` server issues

> **Client swap rule:** If `NearbyLookup:DataFile` is empty **and** `NearbyLookup:ApiBaseUrl` is set, the client calls `/api/nearby` with pinned lat/lng; otherwise it loads the local JSON file.

---

## 9) Security, privacy & abuse controls

- **Anonymous option:** Do not include name/phone; only incident text + coordinates + time.
- **Rate limiting** on API (`/api/nearby`) by IP + key if public.
- **Data provenance:** keep `extras.source` and `updatedUtc` for traceability.
- **Respect OSM Nominatim policy** for any geocoding (proper UA + optional From email).

---

## 10) Versioning & compatibility

- **Top-level `version`:** bump on breaking schema change.
- **Forward compatible** via `extras` for new attributes.
- Clients should **ignore unknown fields** gracefully.

---

## 11) Testing checklist

- ✅ Haversine ranking against known coordinates (golden tests).  
- ✅ Empty results → show national fallback numbers.  
- ✅ Offline: JSON & tiles available; “Nearby help” appears without network.  
- ✅ Internationalization: swap to `emergency_poi_no.json` (Norway) and verify flow.  
- ✅ Phone number normalization (strip spaces, keep leading +).

---

## 12) Migration path (JSON → API)

1. Ship with local JSON (`NearbyLookup:DataFile`).  
2. Add **optional** `NearbyLookup:ApiBaseUrl` and a toggle `UseApiFirst` (default false).  
3. When ready, flip `UseApiFirst=true`; keep JSON as **fallback** if API fails.  
4. Eventually remove JSON per region when API coverage is complete.

**Client config example (API-first with JSON fallback):**
```json
"NearbyLookup": {
  "Enabled": true,
  "UseApiFirst": true,
  "ApiBaseUrl": "https://example.org",
  "DataFile": "data/emergency_poi_in.json",
  "MaxRadiusKm": 40,
  "MaxResultsPerType": 2
}
```

---

## 13) Minimal client responsibilities

- Load JSON (or call API) **after the user pins the location**.  
- Compute distance (Haversine) and rank if using JSON; with `/api/nearby`, the server already ranks.  
- Render the **top 1–2 hospitals & police** with **Call** buttons.  
- Never block emergency actions if lookup fails — always allow **Call 112/100**.

---

## 14) Future enhancements

- Jurisdiction-aware police routing; state/zone helplines.  
- Trauma center metadata; bed/ER capacity (when partners allow).  
- Local caching + delta updates (ETag/If-None-Match).  
- Background refresh while app is open (respecting battery/data).

---

### TL;DR
Start **offline with JSON**, surface the **nearest 1–2** hospital/police, then flip a **config switch** to use an API with the **same schema**. No UI churn, robust in the field, ready to scale.
