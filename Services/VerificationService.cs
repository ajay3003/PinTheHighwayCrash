using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PinTheHighwayCrash.Models;

namespace PinTheHighwayCrash.Services;

public class VerificationService
{
    private readonly HttpClient _http;
    private readonly GeoOptions _geo;

    public VerificationService(HttpClient http, IOptions<GeoOptions> geo)
    {
        _http = http;
        _geo = geo.Value;

        // Identify app per OSM usage policy
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PinTheHighwayCrash/1.0 (+https://example.org)");

        if (!string.IsNullOrWhiteSpace(_geo.OnRoadVerification.NominatimEmail))
            _http.DefaultRequestHeaders.From = _geo.OnRoadVerification.NominatimEmail;
    }

    /// <summary>
    /// Checks via Nominatim if a coordinate appears to be on a road.
    /// </summary>
    public async Task<OnRoadResult> VerifyIfOnRoadAsync(double lat, double lng, CancellationToken ct = default)
    {
        if (!_geo.OnRoadVerification.Enabled || _geo.OnRoadVerification.Provider != "Nominatim")
            return new OnRoadResult(true, "On-road check disabled.");

        var url =
            $"{_geo.OnRoadVerification.NominatimReverseUrl}?format=jsonv2&lat={lat}&lon={lng}&zoom=18&addressdetails=1";

        // Append email (optional but encouraged by OSM)
        if (!string.IsNullOrWhiteSpace(_geo.OnRoadVerification.NominatimEmail))
            url += $"&email={Uri.EscapeDataString(_geo.OnRoadVerification.NominatimEmail)}";

        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
            return new OnRoadResult(true, "Reverse geocode unavailable (network/rate limit).");

        var json = await resp.Content.ReadFromJsonAsync<NominatimReverseResponse>(cancellationToken: ct);

        var cls = json?.Category ?? string.Empty;
        var typ = json?.Type ?? string.Empty;
        var addr = json?.Address ?? new Dictionary<string, string>();

        // Simple heuristic — consider it "on road" if Nominatim classifies as a road/highway type.
        var isLikelyRoad =
            cls.Equals("highway", StringComparison.OrdinalIgnoreCase) ||
            typ.Contains("road", StringComparison.OrdinalIgnoreCase) ||
            typ.Contains("primary", StringComparison.OrdinalIgnoreCase) ||
            typ.Contains("secondary", StringComparison.OrdinalIgnoreCase) ||
            typ.Contains("trunk", StringComparison.OrdinalIgnoreCase) ||
            typ.Contains("motorway", StringComparison.OrdinalIgnoreCase);

        string? locationHint = addr.TryGetValue("road", out var roadName)
            ? roadName
            : addr.TryGetValue("neighbourhood", out var hood)
                ? hood
                : addr.TryGetValue("city", out var city)
                    ? city
                    : null;

        var note = $"Nominatim: {cls}/{typ}" + (locationHint is not null ? $" near {locationHint}" : string.Empty);

        return new OnRoadResult(isLikelyRoad, note);
    }

    // Model for result
    public record OnRoadResult(bool IsOnRoad, string? Note);

    // Internal DTO for Nominatim JSON response
    private sealed class NominatimReverseResponse
    {
        public string? Category { get; set; }
        public string? Type { get; set; }
        public Dictionary<string, string>? Address { get; set; }
    }
}
