using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PinTheHighwayCrash.Models;

namespace PinTheHighwayCrash.Services;

public class VerificationService
{
    private readonly HttpClient _http;
    private readonly GeoOptions _geo;

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public VerificationService(HttpClient http, IOptions<GeoOptions> geo)
    {
        _http = http;
        _geo = geo.Value;

        // Identify app per OSM usage policy
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PinTheHighwayCrash/1.0 (+https://example.org)");

        if (!string.IsNullOrWhiteSpace(_geo.OnRoadVerification.NominatimEmail))
            _http.DefaultRequestHeaders.From = _geo.OnRoadVerification.NominatimEmail;
    }

    // ------------------------------------------------------------
    // 1) Reverse: “Is this point on a road?”
    // ------------------------------------------------------------
    public async Task<OnRoadResult> VerifyIfOnRoadAsync(double lat, double lng, CancellationToken ct = default)
    {
        if (!_geo.OnRoadVerification.Enabled || _geo.OnRoadVerification.Provider != "Nominatim")
            return new OnRoadResult(true, "On-road check disabled.");

        var baseUrl = _geo.OnRoadVerification.NominatimReverseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new OnRoadResult(true, "Reverse URL not configured.");

        var url = $"{baseUrl}?format=jsonv2" +
                  $"&lat={lat.ToString(CultureInfo.InvariantCulture)}" +
                  $"&lon={lng.ToString(CultureInfo.InvariantCulture)}" +
                  $"&zoom=18&addressdetails=1";

        if (!string.IsNullOrWhiteSpace(_geo.OnRoadVerification.NominatimEmail))
            url += $"&email={Uri.EscapeDataString(_geo.OnRoadVerification.NominatimEmail)}";
        if (!string.IsNullOrWhiteSpace(_geo.OnRoadVerification.AcceptLanguage))
            url += $"&accept-language={Uri.EscapeDataString(_geo.OnRoadVerification.AcceptLanguage!)}";
        if (!string.IsNullOrWhiteSpace(_geo.OnRoadVerification.PreferredCountryCodes))
            url += $"&countrycodes={Uri.EscapeDataString(_geo.OnRoadVerification.PreferredCountryCodes!)}";

        using var resp = await _http.GetAsync(url, ct);

        if ((int)resp.StatusCode == 429)
            return new OnRoadResult(true, "Reverse geocode rate-limited by Nominatim (HTTP 429). Try again shortly.");
        if (!resp.IsSuccessStatusCode)
            return new OnRoadResult(true, "Reverse geocode unavailable (network/rate limit).");

        var json = await resp.Content.ReadFromJsonAsync<NominatimReverseResponse>(_jsonOpts, ct);

        var cls = json?.Category ?? string.Empty;
        var typ = json?.Type ?? string.Empty;
        var addr = json?.Address ?? new Dictionary<string, string>();

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

    public record OnRoadResult(bool IsOnRoad, string? Note);

    private sealed class NominatimReverseResponse
    {
        public string? Category { get; set; }
        public string? Type { get; set; }
        public Dictionary<string, string>? Address { get; set; }
    }

    // ------------------------------------------------------------
    // 2) Forward: text / Plus Code / place → coordinates (fallback)
    // ------------------------------------------------------------
    public record GeoPoint(double Lat, double Lng, string? Label);

    /// <summary>
    /// Forward-geocode free text / Plus Codes / place names to a point using Nominatim.
    /// Returns null if nothing found or if forward search isn’t enabled.
    /// </summary>
    public async Task<GeoPoint?> ForwardGeocodeAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        if (!_geo.OnRoadVerification.Enabled || _geo.OnRoadVerification.Provider != "Nominatim")
            return null;

        // Prefer explicit search endpoint from config (best practice).
        var baseUrl = _geo.OnRoadVerification.NominatimSearchUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            // Fallback: derive from reverse url if provided
            baseUrl = BuildSearchUrlFromReverse(_geo.OnRoadVerification.NominatimReverseUrl);
        }
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;

        var url = $"{baseUrl}?format=jsonv2" +
                  $"&q={Uri.EscapeDataString(query)}" +
                  $"&limit=1&addressdetails=1";

        if (!string.IsNullOrWhiteSpace(_geo.OnRoadVerification.NominatimEmail))
            url += $"&email={Uri.EscapeDataString(_geo.OnRoadVerification.NominatimEmail)}";
        if (!string.IsNullOrWhiteSpace(_geo.OnRoadVerification.AcceptLanguage))
            url += $"&accept-language={Uri.EscapeDataString(_geo.OnRoadVerification.AcceptLanguage!)}";
        if (!string.IsNullOrWhiteSpace(_geo.OnRoadVerification.PreferredCountryCodes))
            url += $"&countrycodes={Uri.EscapeDataString(_geo.OnRoadVerification.PreferredCountryCodes!)}";

        using var resp = await _http.GetAsync(url, ct);

        if ((int)resp.StatusCode == 429) return null; // rate-limited
        if (!resp.IsSuccessStatusCode) return null;

        var list = await resp.Content.ReadFromJsonAsync<List<NominatimSearchItem>>(_jsonOpts, ct);
        var first = (list != null && list.Count > 0) ? list[0] : null;
        if (first is null) return null;

        if (!double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return null;
        if (!double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)) return null;

        return new GeoPoint(lat, lng, first.DisplayName);
    }

    private static string? BuildSearchUrlFromReverse(string? reverseUrl)
    {
        if (string.IsNullOrWhiteSpace(reverseUrl)) return null;
        var trimmed = reverseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/reverse", StringComparison.OrdinalIgnoreCase))
            return trimmed[..^("/reverse".Length)] + "/search";
        return trimmed + "/search";
    }

    private sealed class NominatimSearchItem
    {
        [JsonPropertyName("lat")] public string? Lat { get; set; }
        [JsonPropertyName("lon")] public string? Lon { get; set; }
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
    }
}
