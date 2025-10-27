# 🚨 PinTheHighwayCrash — Anti-Spam & Abuse Prevention Strategy

## ⚠️ The Challenge

Since PinTheHighwayCrash lets users contact emergency authorities directly (via phone, SMS, WhatsApp, or email),
there’s a potential for **spam or false reporting** — especially because the app can function offline and without registration.

This document outlines multi-tier strategies to **minimize abuse** while keeping the app responsive in real emergencies.

---

## 🧱 Tier 0 — Client-Only Friction (No Backend Required)

These measures can be implemented immediately using only browser storage and UI logic.

### ✅ Cooldown per Device
After a report is sent, disable emergency buttons for 5–10 minutes.  
Store a timestamp in `localStorage` and display a countdown (e.g., “You can report again in 8 minutes”).

### ✅ Soft Rate Limit + Escalation
- First report → allowed instantly  
- Second report (within 30 minutes) → require a confirmation tap  
- Third report (within 2 hours) → show a “swipe to confirm” or “tap-and-hold” gesture

### ✅ Distance & Accuracy Checks
You already enforce:
- `RequireOnRoad = true`
- `MaxDistanceMeters = 150`
- `MinAccuracyMeters = 60`
This prevents random or distant pins from triggering reports.

### ✅ Duplicate Suppression
If a report from the same device and same coordinates (<300 m within 30 min) is attempted again, show:
> “It looks like you already reported this location recently.”

### ✅ Local Accountability Notice
Show a short disclaimer:
> “False reports may delay real help. Your device and time are logged locally.”

---

## 🧩 Tier 1 — Light Verification (Still No Server Dependency)

Add low-friction checks to deter casual spammers.

### 🔐 One-Time OTP Verification
Verify a phone number or email before first report (via SMS OTP). Cache a verified flag locally.

### 🤖 CAPTCHA on Bursts
Show an invisible or checkbox CAPTCHA **only** if the device exceeds X reports/hour.

### 🧬 Anonymous Device ID
Generate a hash of installation + date → stored locally and rotated daily.  
Use it to track cooldowns and duplicates across sessions.

---

## 🌐 Tier 2 — Server Relay (Recommended for Authorities)

Add a lightweight backend relay that receives reports and **forwards only legitimate ones** to authorities.

### 📡 Relay API
The frontend sends a JSON payload:
```json
{
  "lat": 12.9716,
  "lng": 77.5946,
  "timestamp": "2025-10-27T10:12:00Z",
  "type": "Crash",
  "accuracy": 35,
  "deviceId": "hash123",
  "message": "EMERGENCY: Highway accident reported"
}
```

The relay:
1. Applies **rate limits** (e.g., max 3 reports per 6 hours per device).
2. Clusters reports by `(lat,lng,time)` and forwards only the **first** one.
3. Requires ≥2 unique verified reporters for certain alerts.
4. Forwards via email, WhatsApp API, or SMS gateway.

### 🧠 Smart Filtering
- Ignore duplicates from same region/time.
- Detect patterns (e.g., many identical messages).
- Add minimal auditing (hashed device IDs, coarse location).

---

## 🏛️ Tier 3 — Authority Integration

For official deployments:
- Direct webhook/API integration with emergency dispatch systems (e.g., 112, 911).
- Region-based whitelisting.
- Operator console for clustering and verification.
- Optional integration with CAD (Computer-Aided Dispatch) systems.

---

## ⚙️ Suggested Configuration Keys

Add these to `appsettings.json` for easy tuning:

```json
"Safety": {
  "EnableCooldown": true,
  "CooldownSecondsAfterSend": 600,
  "MaxReportsPerHourPerDevice": 2,
  "RequireOtpOnFirstSend": true,
  "EnableCaptchaOnBurst": true,
  "DedupeRadiusMeters": 300,
  "DedupeWindowMinutes": 30
},
"Relay": {
  "UseServerRelay": true,
  "ForwardMinUniqueReporters": 2
},
"Geo": {
  "RequireOnRoad": true,
  "MaxDistanceMeters": 150,
  "MinAccuracyMeters": 60
}
```

---

## 🧭 UX Friction that Reduces Abuse

- **Tap-and-hold** to send (prevents accidental taps)
- **Severity selection** (Crash / Fire / Blockage / Injuries)
- **“Already reported here” banner** for nearby duplicates
- **Disclaimer** reminding that false reports may be shared with authorities

---

## ✅ Recommended Implementation Sequence

| Step | Action | Complexity | Impact |
|------|--------|-------------|--------|
| 1 | Add local cooldown & duplicate suppression | 🟢 Easy | 🔼 High |
| 2 | Add tap-and-hold confirm | 🟢 Easy | 🔼 High |
| 3 | Add OTP verification | 🟡 Medium | 🔼 High |
| 4 | Deploy server relay with rate limiting | 🟠 Medium | 🔼 Very High |
| 5 | Add clustering & operator console | 🔴 Advanced | 🔼 Critical for production |

---

**Goal:**  
Keep emergency access instant for real users — but *make spam inconvenient and traceable*.

