# Accident Hotspot Integration Strategy for PinTheHighwayCrash

**Version:** 1.0  
**Status:** Design Proposal  
**Goal:** Add accident hotspot awareness and authority contact suggestions (police + hospitals) to the *PinTheHighwayCrash* PWA, while staying offline-capable and API-ready.

---

## 🧭 Overview

To improve contextual awareness, *PinTheHighwayCrash* should suggest:
- **Nearby hospitals and police stations**, and  
- **Accident hotspot warnings** based on user location.

The feature will first use **static JSON files** (offline-friendly) and later support **live APIs** (starting with Norway as a demo, India planned).

---

## ⚙️ Core Concept

When a user pins a location on the map:

1. The app checks the coordinates against:
   - `places.json` → for nearest **hospital** and **police**.
   - `accidents.json` → for nearby **high-accident zones**.
2. If matches found:
   - Show the top 1–2 results on screen.
   - Include phone numbers for instant contact.
   - Optionally display a “⚠️ High accident zone” warning.
3. If online, and an API endpoint is configured in `appsettings.json`, fetch live data.
4. If offline, fallback to cached JSON files stored in `wwwroot/`.

---

## 📁 File Structure

```
wwwroot/
│
├── appsettings.json        # General config
├── places.json             # Police & hospital contact data
└── accidents.json          # Accident hotspots (static dataset)
```

---

## 🧩 File Format Examples

### 1️⃣ `places.json`

```json
{
  "version": 1,
  "police": [
    {
      "id": "pol-001",
      "name": "Highway Police Post – NH48",
      "phone": "+911800112",
      "address": "NH48, Near Toll Plaza, Surat",
      "lat": 21.1702,
      "lng": 72.8311
    }
  ],
  "hospitals": [
    {
      "id": "hosp-001",
      "name": "Civil Hospital, Surat",
      "phone": "+912612266000",
      "address": "Opp. New Civil Hospital Campus, Surat",
      "lat": 21.2030,
      "lng": 72.8400,
      "beds": 200,
      "traumaCenter": true
    }
  ]
}
```

### 2️⃣ `accidents.json`

```json
{
  "country": "IN",
  "lastUpdated": "2025-10-01",
  "hotspots": [
    {
      "id": "ahmedabad-001",
      "name": "Sarkhej–Gandhinagar Highway",
      "lat": 23.0303,
      "lng": 72.5462,
      "severity": "High",
      "accidentsPerYear": 134
    },
    {
      "id": "mumbai-001",
      "name": "Western Express Highway, Andheri",
      "lat": 19.1321,
      "lng": 72.8420,
      "severity": "Very High",
      "accidentsPerYear": 221
    }
  ]
}
```

---

## 🌍 Country Data Sources

### 🇮🇳 **India**
| Source | Data Type | Format | Integration |
|--------|------------|--------|--------------|
| Ministry of Road Transport (MoRTH) | Annual accident stats | PDF/CSV | Manual conversion to JSON |
| Data.gov.in | District-wise accidents | JSON/CSV | Direct import possible |
| NCRB | Accidental deaths & causes | PDF | Optional reference |

### 🇳🇴 **Norway (for testing)**
| Source | API | Endpoint |
|--------|-----|-----------|
| Statens vegvesen | ✅ Live accident data | `https://www.vegvesen.no/trafikkdata/api` |

### 🌐 **Global (fallback)**
| Source | Description |
|--------|--------------|
| Open Data Soft | Public accident datasets | Multiple countries |
| OpenStreetMap (Overpass API) | Community accident tags | Optional enrichment |

---

## 🧠 Data Flow

```
User pins location
    ↓
MapInterop → get coordinates
    ↓
GeoService → queries:
    • places.json → hospitals & police (nearest 2)
    • accidents.json → hotspots within 5 km
    ↓
UI → shows contact info and warning badge
    ↓
(If online) → optional call to /v1/suggest or /v1/hotspots
```

---

## 🧩 Appsettings Extension

Extend your existing `appsettings.json` to include:

```json
"Integration": {
  "UseApi": false,
  "SuggestAuthoritiesApi": "",
  "HotspotApi": "",
  "OfflineFallback": true
}
```

---

## 🚀 Upgrade Path

| Phase | Data Source | Description |
|-------|--------------|-------------|
| **Phase 1** | Static JSON (`wwwroot/*.json`) | Fully offline capable |
| **Phase 2** | REST API (e.g., Norway demo) | Adds dynamic updates |
| **Phase 3** | India National / State API | Full real-time data integration |

---

## 🔐 Security & Abuse Considerations

- Only load **read-only** JSON data (no personal info).
- Validate data signature or checksum if downloaded online.
- For API mode:
  - Use `X-Api-Key` authentication.
  - Limit to read-only queries per minute.

---

## 🧱 Optional Enhancements

1. Display hotspot zones as **red overlays** on the map (Leaflet polygons).
2. Include **road type**, **severity**, and **last updated** labels.
3. Use clustering for dense accident areas.
4. Cache API responses in IndexedDB for offline reuse.

---

## 🧾 Summary

| Component | Type | Offline? | API Ready? | Purpose |
|------------|------|-----------|-------------|----------|
| `places.json` | Static JSON | ✅ | ✅ | Nearest police & hospital data |
| `accidents.json` | Static JSON | ✅ | ✅ | High-risk zones & statistics |
| `IntegrationApi` | Config (optional) | ❌ | ✅ | Fetch real-time data |
| `AdminConfig.razor` | Blazor UI | ✅ | ✅ | Manage & edit JSON files |

---

**End of Document**  
*Author: System Design Team — PinTheHighwayCrash Project (2025)*  
