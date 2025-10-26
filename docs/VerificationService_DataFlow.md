# VerificationService Data Flow — Example Scenario

## 🧭 Scenario
A driver opens **PinTheHighwayCrash** on their phone after witnessing a collision on the highway.

They tap **“Report Crash”**, and the app automatically tries to detect if they’re **actually on a road** using live GPS and OpenStreetMap data.

---

## 🔄 Data Flow

1. **Frontend (Blazor + JS interop)**
   - The user’s browser requests their GPS position via `navigator.geolocation`.
   - That position (`latitude`, `longitude`, `accuracy`) is sent into Blazor (`GeoService`).

2. **Blazor calls the backend service (VerificationService)**
   - The coordinates are passed into `VerifyIfOnRoadAsync(lat, lng)`.
   - This method builds a query URL for **OpenStreetMap Nominatim’s reverse-geocoding API**, such as:
     ```
     https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=59.9127&lon=10.7461&zoom=18&addressdetails=1
     ```
   - It includes a user-agent and optional email (as required by OSM’s fair-use policy).

3. **The Nominatim API responds**
   - Nominatim returns JSON describing what’s at those coordinates, for example:
     ```json
     {
       "category": "highway",
       "type": "primary",
       "address": { "road": "E18", "city": "Oslo" }
     }
     ```

4. **The service interprets the response**
   - `VerificationService` checks `category` and `type`.
   - If it sees values like `"highway"`, `"road"`, or `"motorway"`, it concludes the point is **likely on a road**.
   - It also extracts a **location hint**, e.g. “near E18, Oslo”.

5. **Result returned to Blazor**
   - The result looks like:
     ```
     IsOnRoad = true
     Note = "Nominatim: highway/primary near E18"
     ```
   - The Blazor page updates to show:
     ✅ “On road (verified)”.

6. **User submits the report**
   - The app verifies:
     - The user’s position.
     - The pinned crash location.
     - The location is actually on a road.
   - It then allows calling, SMS, WhatsApp, or email to emergency contacts.

---

## 🧩 Summary

**End-to-end flow:**  
**Browser GPS → GeoService → VerificationService → OpenStreetMap (Nominatim API) → VerificationService → Blazor UI**

This ensures users can only send valid, *on-road* crash reports — reducing spam and improving emergency accuracy.
