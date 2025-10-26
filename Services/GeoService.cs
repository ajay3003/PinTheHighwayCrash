using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// Geolocation wrapper for Blazor WebAssembly.
    /// 
    /// Supports:
    ///  • window.geoHelpers.getPosition(options) — rich JS wrapper
    ///  • navigator.geolocation.getCurrentPositionPromise(options) — legacy fallback
    /// 
    /// Provides structured results and graceful error mapping.
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

        // ---------- Public API ----------

        /// <summary>
        /// Simpler helper that returns only the position or null.
        /// </summary>
        public async Task<Position?> GetCurrentPositionAsync(
            int timeoutMs = 10000,
            bool highAccuracy = true,
            CancellationToken ct = default)
        {
            var r = await TryGetCurrentPositionAsync(timeoutMs, highAccuracy, ct);
            return r.Position;
        }

        /// <summary>
        /// Gets the current location, returning full diagnostic info.
        /// </summary>
        public async Task<GeoResult> TryGetCurrentPositionAsync(
            int timeoutMs = 30000,
            bool highAccuracy = true,
            CancellationToken ct = default)
        {
            try
            {
                JsonElement result;

                // --- Try modern helper first ---
                try
                {
                    result = await _js.InvokeAsync<JsonElement>(
                        "geoHelpers.getPosition",
                        ct,
                        new { enableHighAccuracy = highAccuracy, timeout = timeoutMs, maximumAge = 0 });
                }
                catch (JSException)
                {
                    // --- Fallback to legacy promise-based API ---
                    result = await _js.InvokeAsync<JsonElement>(
                        "navigator.geolocation.getCurrentPositionPromise",
                        ct,
                        new { enableHighAccuracy = highAccuracy, timeout = timeoutMs, maximumAge = 0 });
                }

                // --- Rich format: { ok: true, coords: { latitude, longitude, accuracy } } ---
                if (result.ValueKind == JsonValueKind.Object &&
                    result.TryGetProperty("ok", out var okProp) &&
                    okProp.ValueKind == JsonValueKind.True)
                {
                    var c = result.GetProperty("coords");
                    return GeoResult.Success(ReadCoords(c));
                }

                // --- Legacy format: { coords: { latitude, longitude, accuracy } } ---
                if (result.ValueKind == JsonValueKind.Object &&
                    result.TryGetProperty("coords", out var coordsEl) &&
                    coordsEl.ValueKind == JsonValueKind.Object)
                {
                    return GeoResult.Success(ReadCoords(coordsEl));
                }

                return GeoResult.Fail(GeoErrorCode.NoCoords, "No coordinates returned from geolocation API.");
            }
            catch (JSException jse)
            {
                var (code, msg) = ParseError(jse.Message);
                return GeoResult.Fail(code, msg);
            }
            catch (OperationCanceledException oce)
            {
                return GeoResult.Fail(GeoErrorCode.Timeout, $"Cancelled or timed out: {oce.Message}");
            }
            catch (Exception ex)
            {
                return GeoResult.Fail(GeoErrorCode.Exception, ex.Message);
            }
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

                return (MapCode(codeText, raw), msgText ?? raw);
            }
            catch
            {
                // Fallback: infer from message text if JSON parse fails
                var s = raw.ToLowerInvariant();
                if (s.Contains("permission")) return (GeoErrorCode.PermissionDenied, raw);
                if (s.Contains("unavailable")) return (GeoErrorCode.PositionUnavailable, raw);
                if (s.Contains("timeout")) return (GeoErrorCode.Timeout, raw);
                if (s.Contains("unsupported")) return (GeoErrorCode.Unsupported, raw);
                return (GeoErrorCode.JsException, raw);
            }
        }

        private static GeoErrorCode MapCode(string? code, string rawFallback)
        {
            if (string.IsNullOrWhiteSpace(code))
                return GeoErrorCode.JsException;

            var c = code.Trim().ToUpperInvariant();
            return c switch
            {
                "PERMISSION_DENIED" => GeoErrorCode.PermissionDenied,
                "POSITION_UNAVAILABLE" => GeoErrorCode.PositionUnavailable,
                "TIMEOUT" => GeoErrorCode.Timeout,
                "UNSUPPORTED" => GeoErrorCode.Unsupported,
                "NO_COORDS" => GeoErrorCode.NoCoords,
                "JS_EXCEPTION" => GeoErrorCode.JsException,
                "EXCEPTION" => GeoErrorCode.Exception,
                _ => GuessCodeFromText(code)
            };
        }

        private static GeoErrorCode GuessCodeFromText(string text)
        {
            var s = text.ToLowerInvariant();
            if (s.Contains("permission")) return GeoErrorCode.PermissionDenied;
            if (s.Contains("unavailable")) return GeoErrorCode.PositionUnavailable;
            if (s.Contains("timeout")) return GeoErrorCode.Timeout;
            if (s.Contains("unsupported")) return GeoErrorCode.Unsupported;
            return GeoErrorCode.JsException;
        }

        /// <summary>
        /// Computes great-circle distance (in meters) between two coordinates using the Haversine formula.
        /// </summary>
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
