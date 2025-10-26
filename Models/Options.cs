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
        [Required] public string TileUrl { get; set; } = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
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
    /// Logging configuration (appsettings:Logging).
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
