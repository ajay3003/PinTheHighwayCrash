using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// Geolocation wrapper for Blazor WASM with strong typing and graceful fallbacks.
    /// Works with either:
    ///  - window.geoHelpers.getPosition(options)  // rich wrapper
    ///  - navigator.geolocation.getCurrentPositionPromise(options) // legacy promise
    /// </summary>
    public sealed class GeoService
    {
        private readonly IJSRuntime _js;
        public GeoService(IJSRuntime js) => _js = js;

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

        /// <summary>
        /// Backward-compatible helper returning only Position or null.
        /// Internally uses TryGetCurrentPositionAsync.
        /// </summary>
        public async Task<Position?> GetCurrentPositionAsync(int timeoutMs = 10000, bool highAccuracy = true, CancellationToken ct = default)
        {
            var r = await TryGetCurrentPositionAsync(timeoutMs, highAccuracy, ct);
            return r.Position;
        }

        /// <summary>
        /// Preferred API: returns a typed GeoResult (Position + error info).
        /// Tries geoHelpers.getPosition first, then falls back to navigator.geolocation.getCurrentPositionPromise.
        /// </summary>
        public async Task<GeoResult> TryGetCurrentPositionAsync(
            int timeoutMs = 30000,
            bool highAccuracy = true,
            CancellationToken ct = default)
        {
            try
            {
                JsonElement result;

                // 1) Try the rich wrapper if present
                try
                {
                    result = await _js.InvokeAsync<JsonElement>(
                        "geoHelpers.getPosition",
                        ct,
                        new { enableHighAccuracy = highAccuracy, timeout = timeoutMs, maximumAge = 0 });
                }
                catch (JSException)
                {
                    // 2) Fallback to the legacy promise wrapper
                    result = await _js.InvokeAsync<JsonElement>(
                        "navigator.geolocation.getCurrentPositionPromise",
                        ct,
                        new { enableHighAccuracy = highAccuracy, timeout = timeoutMs, maximumAge = 0 });
                }

                // Shape A (rich): { ok: true, coords: { latitude, longitude, accuracy } }
                if (result.ValueKind == JsonValueKind.Object &&
                    result.TryGetProperty("ok", out var okProp) && okProp.ValueKind == JsonValueKind.True)
                {
                    var c = result.GetProperty("coords");
                    return GeoResult.Success(ReadCoords(c));
                }

                // Shape B (legacy): { coords: { latitude, longitude, accuracy } }
                if (result.ValueKind == JsonValueKind.Object &&
                    result.TryGetProperty("coords", out var coordsEl) &&
                    coordsEl.ValueKind == JsonValueKind.Object)
                {
                    return GeoResult.Success(ReadCoords(coordsEl));
                }

                return GeoResult.Fail(GeoErrorCode.NoCoords, "No coordinates returned.");
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

        private static Position ReadCoords(JsonElement coords)
        {
            var lat = coords.TryGetProperty("latitude", out var latEl) ? latEl.GetDouble() : 0d;
            var lng = coords.TryGetProperty("longitude", out var lngEl) ? lngEl.GetDouble() : 0d;
            var acc = coords.TryGetProperty("accuracy", out var accEl) ? accEl.GetDouble() : double.NaN;
            return new Position(lat, lng, acc);
        }

        private static (GeoErrorCode code, string message) ParseError(string raw)
        {
            // If the JS side rejected with a structured { code, message }, parse it.
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                var codeText = root.TryGetProperty("code", out var cEl) && cEl.ValueKind == JsonValueKind.String
                    ? cEl.GetString() : null;
                var msgText = root.TryGetProperty("message", out var mEl) && mEl.ValueKind == JsonValueKind.String
                    ? mEl.GetString() : raw;

                return (MapCode(codeText, raw), msgText ?? raw);
            }
            catch
            {
                // Fallback: infer from message text
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
            switch (code?.Trim().ToUpperInvariant())
            {
                case "PERMISSION_DENIED": return GeoErrorCode.PermissionDenied;
                case "POSITION_UNAVAILABLE": return GeoErrorCode.PositionUnavailable;
                case "TIMEOUT": return GeoErrorCode.Timeout;
                case "UNSUPPORTED": return GeoErrorCode.Unsupported;
                case "NO_COORDS": return GeoErrorCode.NoCoords;
                case "JS_EXCEPTION": return GeoErrorCode.JsException;
                case "EXCEPTION": return GeoErrorCode.Exception;
                case null: return GeoErrorCode.JsException;
                default:
                    // unknown string; try to infer
                    var s = code.ToLowerInvariant();
                    if (s.Contains("permission")) return GeoErrorCode.PermissionDenied;
                    if (s.Contains("unavailable")) return GeoErrorCode.PositionUnavailable;
                    if (s.Contains("timeout")) return GeoErrorCode.Timeout;
                    if (s.Contains("unsupported")) return GeoErrorCode.Unsupported;
                    return GeoErrorCode.JsException;
            }
        }

        /// <summary>
        /// Great-circle distance in meters using Haversine formula.
        /// </summary>
        public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * R * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
