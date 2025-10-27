using System.ComponentModel.DataAnnotations;

public sealed class AdminSettings
{
    public int Schema { get; set; } = 1;

    [Range(0.1, 50)]
    public double GeofenceKm { get; set; } = 3;

    public CooldownSettings Cooldowns { get; set; } = new();

    public Channels Channels { get; set; } = new();

    public Templates Templates { get; set; } = new();

    public bool TelemetryEnabled { get; set; } = false;

    public bool TestMode { get; set; } = false;
}

public sealed class CooldownSettings
{
    [Range(10, 3600)]
    public int ReportSeconds { get; set; } = 120;

    [Range(10, 3600)]
    public int NotifySeconds { get; set; } = 300;
}

public sealed class Channels
{
    public bool Call { get; set; } = true;
    public bool Sms { get; set; } = true;
    public bool Email { get; set; } = true;
    public bool WhatsApp { get; set; } = false;
}

public sealed class Templates
{
    [Required, StringLength(280)]
    public string Sms { get; set; } = "Accident at {coords}. Need help. {timestamp}";
    [StringLength(2000)]
    public string Email { get; set; } =
        "Accident reported at {location} ({coords}) at {timestamp}. Details: {details}";
}
