// PinTheHighwayCrash/Models/Options.cs
#nullable enable
using System.ComponentModel.DataAnnotations;

namespace PinTheHighwayCrash.Models
{
    /// <summary>
    /// Feature flags to toggle optional pages / panels.
    /// </summary>
    public sealed class FeatureFlags
    {
        public bool ShowHealthPage { get; set; } = true;
        public bool ShowDebugPanel { get; set; } = false;
    }

    /// <summary>
    /// Emergency contact / channel configuration.
    /// </summary>
    public sealed class EmergencyOptions
    {
        [Required] public string Country { get; set; } = "IN";
        [Required] public string Phone { get; set; } = "112";
        public string? WhatsAppNumber { get; set; }
        public string? Email { get; set; }
        public string EmailSubject { get; set; } = "EMERGENCY: Highway accident";
        public string SmsIntro { get; set; } = "EMERGENCY: Highway accident reported";

        public bool EnableCall { get; set; } = true;
        public bool EnableSms { get; set; } = true;
        public bool EnableWhatsApp { get; set; } = true;
        public bool EnableEmail { get; set; } = true;
    }

    /// <summary>
    /// Map rendering options (Leaflet).
    /// </summary>
    public sealed class MapOptions
    {
        public double FallbackLat { get; set; } = 20.5937;
        public double FallbackLng { get; set; } = 78.9629;
        public int InitialZoom { get; set; } = 16;

        [Required]
        public string TileUrl { get; set; } = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
    }

    /// <summary>
    /// Geolocation, distance gating, and verification options.
    /// </summary>
    public sealed class GeoOptions
    {
        public int MaxDistanceMeters { get; set; } = 150;
        public bool RequireGpsFix { get; set; } = true;
        public int GeolocationTimeoutMs { get; set; } = 15000;
        public int MinAccuracyMeters { get; set; } = 60;

        [Required] public OnRoadVerificationOptions OnRoadVerification { get; set; } = new();
    }

    public sealed class OnRoadVerificationOptions
    {
        public bool Enabled { get; set; } = true;
        public string Provider { get; set; } = "Nominatim";
        public int MaxRoadDistanceMeters { get; set; } = 60;

        [Required] public string NominatimReverseUrl { get; set; } = "https://nominatim.openstreetmap.org/reverse";
        [Required] public string NominatimSearchUrl { get; set; } = "https://nominatim.openstreetmap.org/search";
        public string? NominatimEmail { get; set; } = "your-contact-email@example.com";
        public string? PreferredCountryCodes { get; set; } = "IN";
        public string? AcceptLanguage { get; set; } = "en";
    }

    /// <summary>
    /// Offline/PWA and map tile caching options.
    /// </summary>
    public sealed class OfflineOptions
    {
        public bool EnablePwa { get; set; } = true;
        public bool CacheTiles { get; set; } = true;

        /// <summary>
        /// Hosts allowed for tile caching (e.g., tile.openstreetmap.org).
        /// </summary>
        [Required] public string[] TileHosts { get; set; } = new[] { "tile.openstreetmap.org" };

        /// <summary>
        /// Max number of tiles to keep in cache.
        /// </summary>
        public int MaxCachedTiles { get; set; } = 800;

        /// <summary>
        /// Remove cached tiles older than this many days.
        /// </summary>
        public int MaxTileAgeDays { get; set; } = 21;
    }

    /// <summary>
    /// Cooldown configuration for throttling user actions (call/SMS/WhatsApp/email).
    /// </summary>
    public sealed class CooldownOptions
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Default duration in seconds if action-specific values are not provided.
        /// </summary>
        [Range(0, int.MaxValue)]
        public int DefaultDurationSeconds { get; set; } = 120;

        [Required] public PerActionCooldowns PerAction { get; set; } = new();

        /// <summary>
        /// Small window to allow rapid double-taps without starting a full cooldown.
        /// </summary>
        [Range(0, 30)]
        public int GracePeriodSeconds { get; set; } = 3;

        [Required] public PersistOptions Persist { get; set; } = new();
        [Required] public CooldownUiOptions Ui { get; set; } = new();
        [Required] public TestModeOptions TestMode { get; set; } = new();
        [Required] public DebugOptions Debug { get; set; } = new();
        [Required] public ClockOptions Clock { get; set; } = new();

        public sealed class PerActionCooldowns
        {
            [Range(0, int.MaxValue)]
            public int CallSeconds { get; set; } = 120;
            [Range(0, int.MaxValue)]
            public int SmsSeconds { get; set; } = 90;
            [Range(0, int.MaxValue)]
            public int WhatsAppSeconds { get; set; } = 90;
            [Range(0, int.MaxValue)]
            public int EmailSeconds { get; set; } = 60;
        }

        public sealed class PersistOptions
        {
            /// <summary>Use localStorage (true) or sessionStorage (false).</summary>
            public bool UseLocalStorage { get; set; } = true;

            /// <summary>Key prefix for storage entries.</summary>
            [Required] public string KeyPrefix { get; set; } = "pthc_cd_";

            /// <summary>Keep cooldown state across reloads for this many minutes.</summary>
            [Range(0, int.MaxValue)]
            public int PersistAcrossReloadMinutes { get; set; } = 120;

            /// <summary>Cleanup keys immediately when cooldown expires.</summary>
            public bool CleanupOnExpire { get; set; } = true;
        }

        public sealed class CooldownUiOptions
        {
            public bool ShowCountdown { get; set; } = true;

            /// <summary>Allowed values: "mm:ss" or "ss".</summary>
            [Required] public string CountdownFormat { get; set; } = "mm:ss";

            /// <summary>When remaining time &lt;= threshold, UI may highlight.</summary>
            [Range(0, int.MaxValue)]
            public int WarnThresholdSeconds { get; set; } = 10;

            public bool ShowBadge { get; set; } = true;

            /// <summary>Template can contain {remaining}.</summary>
            [Required] public string BadgeText { get; set; } = "Cooldown: {remaining}";

            public bool DisableButtonsDuringCooldown { get; set; } = true;

            /// <summary>Template can contain {remaining}.</summary>
            [Required] public string DisabledButtonTextTemplate { get; set; } = "Wait {remaining}";

            /// <summary>Tooltip shown on disabled controls; can contain {remaining}.</summary>
            [Required] public string TooltipTemplate { get; set; } = "Please wait {remaining} before trying again";

            public bool ToastOnAttemptDuringCooldown { get; set; } = true;

            /// <summary>Toast content when user attempts action during cooldown; can contain {remaining}.</summary>
            [Required] public string ToastMessageTemplate { get; set; } = "Action on cooldown. Try again in {remaining}.";
        }

        public sealed class TestModeOptions
        {
            /// <summary>If enabled, all actions use OverrideAllActionsSeconds for faster testing.</summary>
            public bool Enabled { get; set; } = false;

            [Range(0, int.MaxValue)]
            public int OverrideAllActionsSeconds { get; set; } = 5;
        }

        public sealed class DebugOptions
        {
            /// <summary>If the Debug panel is visible, bypass cooldowns (useful in dev).</summary>
            public bool BypassWhenShowDebugPanel { get; set; } = false;

            /// <summary>Emit detailed state transition logs to console.</summary>
            public bool LogTransitions { get; set; } = true;
        }

        public sealed class ClockOptions
        {
            /// <summary>"Browser" for Date.now(); "Server" if you provide a server time source.</summary>
            [Required] public string NowSource { get; set; } = "Browser";
        }
    }

    /// <summary>
    /// Lightweight client-side anti-spam options (pairs with AntiSpamService).
    /// </summary>
    public sealed class AntiSpamOptions
    {
        public bool Enabled { get; set; } = true;

        /// <summary>Minutes to block repeat reports from the same ~cell.</summary>
        [Range(0, int.MaxValue)]
        public int DuplicateWindowMinutes { get; set; } = 10;

        /// <summary>Approximate cell size in meters used for duplicate detection.</summary>
        [Range(1, 500)]
        public int CellSizeMeters { get; set; } = 30;

        /// <summary>Lock out other channels for N seconds after any action fires.</summary>
        [Range(0, int.MaxValue)]
        public int PostActionLockoutSeconds { get; set; } = 60;

        /// <summary>Daily caps per action. 0 = unlimited.</summary>
        [Required] public Caps DailyCaps { get; set; } = new();

        /// <summary>Storage behavior.</summary>
        [Required] public StorageCfg Storage { get; set; } = new();

        public sealed class Caps
        {
            [Range(0, int.MaxValue)] public int Call { get; set; } = 3;
            [Range(0, int.MaxValue)] public int Sms { get; set; } = 3;
            [Range(0, int.MaxValue)] public int WhatsApp { get; set; } = 3;
            [Range(0, int.MaxValue)] public int Email { get; set; } = 3;

            public int ForAction(string action) => action.ToLowerInvariant() switch
            {
                "call" => Call,
                "sms" => Sms,
                "whatsapp" => WhatsApp,
                "email" => Email,
                _ => 0
            };
        }

        public sealed class StorageCfg
        {
            /// <summary>Use localStorage (true) or sessionStorage (false).</summary>
            public bool UseLocalStorage { get; set; } = true;

            /// <summary>Key prefix for storage entries.</summary>
            [Required] public string KeyPrefix { get; set; } = "pthc_as_";
        }
    }

    /// <summary>
    /// Logging configuration (appsettings:Logging).
    /// Note: "Microsoft.Hosting.Lifetime" uses a dotted key in configuration;
    /// keep using this POCO if you're already binding it successfully.
    /// </summary>
    public sealed class LoggingOptions
    {
        public LogLevelSection? LogLevel { get; set; }
        public ConsoleLogging? Console { get; set; }

        public sealed class LogLevelSection
        {
            public string? Default { get; set; }
            public string? Microsoft { get; set; }
            public string? MicrosoftHostingLifetime { get; set; }
        }

        public sealed class ConsoleLogging
        {
            public bool Enable { get; set; } = true;
            public string? Level { get; set; }
        }
    }
}
