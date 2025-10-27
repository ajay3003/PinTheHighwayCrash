// PinTheHighwayCrash/Services/GeoService.cs
#nullable enable
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// Geolocation wrapper for Blazor WebAssembly.
    /// Tries geoHelpers.getPosition → legacy promise polyfill → maps errors clearly.
    /// Includes retries and optional maximumAge allowance.
    /// </summary>
    public sealed class GeoService
    {
        private readonly IJSRuntime _js;
        public GeoService(IJSRuntime js) => _js = js;

        // ---------- Models ----------
        public record Position(double Latitude, double Longitude, double AccuracyMeters);

        public enum GeoErrorCode
        {
            None,
            PermissionDenied,
            PositionUnavailable,
            Timeout,
            Unsupported,
            NoCoords,
            JsException,
            Exception
        }

        public record GeoResult(Position? Position, GeoErrorCode ErrorCode, string? ErrorMessage)
        {
            public bool IsSuccess => Position is not null && ErrorCode == GeoErrorCode.None;
            public static GeoResult Success(Position p) => new(p, GeoErrorCode.None, null);
            public static GeoResult Fail(GeoErrorCode code, string? msg) => new(null, code, msg);
        }

        // ---------- Public API (simple) ----------
        public async Task<Position?> GetCurrentPositionAsync(
            int timeoutMs = 10000,
            bool highAccuracy = true,
            int maximumAgeMs = 0,
            CancellationToken ct = default)
        {
            var r = await TryGetCurrentPositionAsync(timeoutMs, highAccuracy, maximumAgeMs, ct);
            return r.Position;
        }

        // ---------- Public API (diagnostic, single attempt) ----------
        public async Task<GeoResult> TryGetCurrentPositionAsync(
            int timeoutMs = 30000,
            bool highAccuracy = true,
            int maximumAgeMs = 0,
            CancellationToken ct = default)
        {
            using var linked = LinkWithTimeout(ct, timeoutMs);
            var token = linked.Token;

            try
            {
                JsonElement result;
                try
                {
                    result = await _js.InvokeAsync<JsonElement>(
                        "geoHelpers.getPosition",
                        token,
                        new { enableHighAccuracy = highAccuracy, timeout = timeoutMs, maximumAge = maximumAgeMs });
                }
                catch (JSException)
                {
                    result = await _js.InvokeAsync<JsonElement>(
                        "navigator.geolocation.getCurrentPositionPromise",
                        token,
                        new { enableHighAccuracy = highAccuracy, timeout = timeoutMs, maximumAge = maximumAgeMs });
                }

                if (result.ValueKind == JsonValueKind.Object &&
                    result.TryGetProperty("ok", out var okProp) &&
                    okProp.ValueKind == JsonValueKind.True &&
                    result.TryGetProperty("coords", out var c1) &&
                    c1.ValueKind == JsonValueKind.Object)
                {
                    return GeoResult.Success(ReadCoords(c1));
                }

                if (result.ValueKind == JsonValueKind.Object &&
                    result.TryGetProperty("coords", out var c2) &&
                    c2.ValueKind == JsonValueKind.Object)
                {
                    return GeoResult.Success(ReadCoords(c2));
                }

                return GeoResult.Fail(GeoErrorCode.NoCoords, "No coordinates returned from geolocation API.");
            }
            catch (OperationCanceledException oce)
            {
                return GeoResult.Fail(GeoErrorCode.Timeout, $"Cancelled or timed out: {oce.Message}");
            }
            catch (JSException jse)
            {
                var (code, msg) = ParseError(jse.Message);
                return GeoResult.Fail(code, msg);
            }
            catch (Exception ex)
            {
                return GeoResult.Fail(GeoErrorCode.Exception, ex.Message);
            }
        }

        // ---------- Multi-strategy retry ----------
        public async Task<GeoResult> TryGetCurrentPositionWithRetriesAsync(
            int timeoutMs = 10000,
            CancellationToken ct = default)
        {
            var first = await TryGetCurrentPositionAsync(timeoutMs: timeoutMs, highAccuracy: true, maximumAgeMs: 0, ct);
            if (first.IsSuccess) return first;

            var second = await TryGetCurrentPositionAsync(timeoutMs: Math.Max(15000, timeoutMs), highAccuracy: false, maximumAgeMs: 0, ct);
            if (second.IsSuccess) return second;

            var third = await TryGetCurrentPositionAsync(timeoutMs: Math.Max(15000, timeoutMs), highAccuracy: false, maximumAgeMs: 30_000, ct);
            if (third.IsSuccess) return third;

            return PickBestError(first, second, third);
        }

        // ---------- Helpers ----------
        private static Position ReadCoords(JsonElement coords)
        {
            var lat = coords.TryGetProperty("latitude", out var latEl) ? latEl.GetDouble() : 0d;
            var lng = coords.TryGetProperty("longitude", out var lngEl) ? lngEl.GetDouble() : 0d;
            var acc = coords.TryGetProperty("accuracy", out var accEl) ? accEl.GetDouble() : double.NaN;
            return new Position(lat, lng, acc);
        }

        private static (GeoErrorCode code, string message) ParseError(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                var codeText = root.TryGetProperty("code", out var cEl) && cEl.ValueKind == JsonValueKind.String
                    ? cEl.GetString()
                    : null;

                var msgText = root.TryGetProperty("message", out var mEl) && mEl.ValueKind == JsonValueKind.String
                    ? mEl.GetString()
                    : raw;

                return (MapCode(codeText), msgText ?? raw);
            }
            catch
            {
                var s = raw.ToLowerInvariant();
                if (s.Contains("permission")) return (GeoErrorCode.PermissionDenied, raw);
                if (s.Contains("unavailable")) return (GeoErrorCode.PositionUnavailable, raw);
                if (s.Contains("timeout")) return (GeoErrorCode.Timeout, raw);
                if (s.Contains("unsupported")) return (GeoErrorCode.Unsupported, raw);
                return (GeoErrorCode.JsException, raw);
            }
        }

        private static GeoErrorCode MapCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return GeoErrorCode.JsException;
            switch (code.Trim().ToUpperInvariant())
            {
                case "PERMISSION_DENIED": return GeoErrorCode.PermissionDenied;
                case "POSITION_UNAVAILABLE": return GeoErrorCode.PositionUnavailable;
                case "TIMEOUT": return GeoErrorCode.Timeout;
                case "UNSUPPORTED": return GeoErrorCode.Unsupported;
                case "NO_COORDS": return GeoErrorCode.NoCoords;
                case "JS_EXCEPTION": return GeoErrorCode.JsException;
                case "EXCEPTION": return GeoErrorCode.Exception;
                default:
                    var s = code.ToLowerInvariant();
                    if (s.Contains("permission")) return GeoErrorCode.PermissionDenied;
                    if (s.Contains("unavailable")) return GeoErrorCode.PositionUnavailable;
                    if (s.Contains("timeout")) return GeoErrorCode.Timeout;
                    if (s.Contains("unsupported")) return GeoErrorCode.Unsupported;
                    return GeoErrorCode.JsException;
            }
        }

        private static GeoResult PickBestError(params GeoResult[] results)
        {
            foreach (var r in results)
                if (r.ErrorCode == GeoErrorCode.PermissionDenied)
                    return r;

            foreach (var r in results)
                if (!r.IsSuccess && !string.IsNullOrWhiteSpace(r.ErrorMessage))
                    return r;

            return GeoResult.Fail(GeoErrorCode.Exception, "Could not acquire location.");
        }

        private static CancellationTokenSource LinkWithTimeout(CancellationToken ct, int timeoutMs)
        {
            if (timeoutMs <= 0)
                return CancellationTokenSource.CreateLinkedTokenSource(ct);

            var ctsTimeout = new CancellationTokenSource(timeoutMs);
            return CancellationTokenSource.CreateLinkedTokenSource(ct, ctsTimeout.Token);
        }

        /// <summary>Great-circle distance in meters (Haversine).</summary>
        public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Earth radius (m)
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * R * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
