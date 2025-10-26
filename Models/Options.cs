namespace PinTheHighwayCrash.Models;

public class EmergencyOptions
{
    public string Country { get; set; } = "IN";
    public string Phone { get; set; } = "112";
    public string? WhatsAppNumber { get; set; }
    public string? Email { get; set; }
    public string EmailSubject { get; set; } = "EMERGENCY: Highway accident";
    public string SmsIntro { get; set; } = "EMERGENCY: Highway accident reported";
    public bool EnableCall { get; set; } = true;
    public bool EnableSms { get; set; } = true;
    public bool EnableWhatsApp { get; set; } = true;
    public bool EnableEmail { get; set; } = true;
}

public class MapOptions
{
    public double FallbackLat { get; set; } = 20.5937;
    public double FallbackLng { get; set; } = 78.9629;
    public int InitialZoom { get; set; } = 16;
    public string TileUrl { get; set; } = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
}

public class GeoOptions
{
    public int MaxDistanceMeters { get; set; } = 150;
    public bool RequireGpsFix { get; set; } = true;
    public int GeolocationTimeoutMs { get; set; } = 15000;
    public int MinAccuracyMeters { get; set; } = 60;
    public OnRoadVerificationOptions OnRoadVerification { get; set; } = new();
}

public class OnRoadVerificationOptions
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "Nominatim";
    public int MaxRoadDistanceMeters { get; set; } = 60;
    public string NominatimReverseUrl { get; set; } = "https://nominatim.openstreetmap.org/reverse";
    public string? NominatimEmail { get; set; } = null; // OSM requires a contact email in User-Agent/query
}

public class LoggingOptions
{
    public string MinimumLevel { get; set; } = "Information";
    public ConsoleLoggingOptions Console { get; set; } = new();
}

public class ConsoleLoggingOptions
{
    public bool Enable { get; set; } = true;
    public string Level { get; set; } = "Debug";
}

public class FeatureFlags
{
    public bool ShowHealthPage { get; set; } = true;
    public bool ShowDebugPanel { get; set; } = false;
}
