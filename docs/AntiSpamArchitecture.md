# 🛡️ Anti-Spam & Cooldown Architecture — *PinTheHighwayCrash*

## Overview
The application now includes a **two-layer protection system** against rapid or repeated emergency submissions:

1. **Cooldown Service (`CooldownService` + `CooldownButton`)**
2. **Anti-Spam Service (`AntiSpamService`)**

Together, they throttle user actions like **Call, SMS, WhatsApp, and Email**, preventing accidental spam and repeated alerts from the same location.

---

## 1️⃣ Cooldown Service

### Purpose
Prevents rapid consecutive taps on the same button (e.g., “Call” or “Send SMS”).

### Key Features
- Per-action cooldown (configurable in `appsettings.json`)
- Persistent across page reloads (`localStorage` or `sessionStorage`)
- Optional countdown display on buttons
- Grace period for small double-clicks
- Supports configurable UI templates (`{remaining}` substitution)

### Example Configuration (from `appsettings.json`)
```json
"Cooldown": {
  "Enabled": true,
  "DefaultDurationSeconds": 120,
  "PerAction": {
    "CallSeconds": 120,
    "SmsSeconds": 90,
    "WhatsAppSeconds": 90,
    "EmailSeconds": 60
  },
  "Ui": {
    "ShowCountdown": true,
    "BadgeText": "Cooldown: {remaining}",
    "DisabledButtonTextTemplate": "Wait {remaining}",
    "TooltipTemplate": "Please wait {remaining} before trying again"
  }
}
```

### Example Usage
```razor
<CooldownButton ActionKey="call"
                Css="btn btn-danger"
                Text="📞 Call 112"
                OnClick="CallEmergency" />
```

---

## 2️⃣ Anti-Spam Service

### Purpose
Adds a **second layer** of protection by tracking *frequency* and *geolocation* of actions.
Prevents excessive use even after cooldowns expire.

### Behavior
| Protection Type | Description |
|------------------|-------------|
| **Global Lockout** | After any emergency action, blocks all channels briefly (e.g. 5–10 min). |
| **Duplicate Location** | Prevents repeat reports from nearly the same coordinates for a short window (e.g. 30 m radius for 10 min). |
| **Per-Action Daily Cap** | Limits number of actions (e.g. SMS, Call, Email) per day. |
| **Cool-Down Reinforcement** | Works with `CooldownService` to ensure consistent throttling. |

### Integration Example (Report.razor)
```razor
private async Task SendSms()
{
    if (!CanSend)
    {
        _submitMsg = "You must be on a verified road and near the pinned location.";
        return;
    }

    var decision = await AntiSpam.GuardAsync("sms", _pinLat, _pinLng);
    if (!decision.Allowed)
    {
        _submitMsg = decision.Reason;
        return;
    }

    var body = Uri.EscapeDataString(BuildReportText());
    var url = $"sms:{_phone}?&body={body}";
    await JS.InvokeVoidAsync("shareInterop.openUrl", url);
    _submitMsg = $"Opened SMS app prefilled to {_phone}.";

    await AntiSpam.RecordAsync("sms", _pinLat, _pinLng);
}
```

---

## 🚫 What This Protects Against
✅ Double-taps or repeated button clicks  
✅ Spamming multiple actions (Call→SMS→WA→Email) in seconds  
✅ Repeated reports from the same place  
✅ Accidental overuse during an emergency  

---

## ⚠️ What It Cannot Fully Prevent
| Limitation | Reason |
|-------------|---------|
| **Incognito / New Browser** | Local storage resets on each session |
| **Manual Storage Clearing** | User can delete `localStorage` data |
| **Clock Tampering** | Time source is the browser (`Date.now()`) |
| **Multiple Devices** | Each device maintains its own storage |

For stronger enforcement, you’ll need **server-side rate limiting** (see below).

---

## 🧩 Optional Hardening
- Add **long-press confirmation** on critical buttons.
- Display **remaining cooldowns** or **lockout status** in the UI.
- Use latitude-aware coordinate hashing for more accurate duplicate detection:
  ```csharp
  var metersPerDegLat = 111_320.0;
  var metersPerDegLng = 111_320.0 * Math.Max(0.1, Math.Cos(lat * Math.PI / 180.0));
  ```

---

## 🖥️ Future Server-Side Extensions
If you introduce a backend API:
- ✅ Implement IP-based rate limits (token bucket / sliding window)
- ✅ Enforce time-signed request nonces (HMAC)
- ✅ Maintain server-side deduplication (lat/lng buckets)
- ✅ Optionally require CAPTCHA after repeated triggers

---

## ✅ Summary
| Layer | Scope | Persistence | Purpose |
|-------|--------|-------------|----------|
| **CooldownService** | Per-action | Browser storage | Prevent rapid repeats |
| **AntiSpamService** | Global, per device | Browser storage | Prevent spam across actions and duplicate locations |
| **Server-Side (future)** | Per-account / IP | Centralized | Enforce long-term global rate limits |

Your current setup provides **robust real-world spam resistance** for a standalone PWA — more than enough for public safety use without backend infrastructure.
