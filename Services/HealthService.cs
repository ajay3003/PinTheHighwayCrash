using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// Runs environment checks (via wwwroot/js/healthInterop.js) and returns a typed report.
    /// Falls back to a helpful report if the JS interop isn't available.
    /// </summary>
    public sealed class HealthService
    {
        private readonly IJSRuntime _js;
        public HealthService(IJSRuntime js) => _js = js;

        public enum HealthStatus
        {
            Pass,
            Warn,
            Fail,
            Info,
            Unknown
        }

        public record HealthItem(string Name, HealthStatus Status, string Detail, string Fix);
        public record HealthReport(HealthStatus Overall, IReadOnlyList<HealthItem> Items, string? Source = null);

        /// <summary>
        /// Runs the health checks by invoking the browser interop: healthInterop.run(timeoutMs).
        /// If the JS helper is missing or throws, a fallback report is generated with guidance.
        /// </summary>
        public async Task<HealthReport> RunAsync(int timeoutMs = 8000, CancellationToken cancellationToken = default)
        {
            try
            {
                // Call the JS runner: returns { overall: string, items: [] }
                var json = await _js.InvokeAsync<JsonElement>("healthInterop.run", cancellationToken, timeoutMs);

                var overallStr = json.TryGetProperty("overall", out var ovEl)
                    ? ovEl.GetString() ?? "unknown"
                    : "unknown";

                var overall = ParseStatus(overallStr);

                var items = new List<HealthItem>();
                if (json.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var it in itemsEl.EnumerateArray())
                    {
                        var name = GetStringOrEmpty(it, "name");
                        var status = ParseStatus(GetStringOrEmpty(it, "status"));
                        var detail = GetStringOrEmpty(it, "detail");
                        var fix = GetStringOrEmpty(it, "fix");
                        items.Add(new HealthItem(name, status, detail, fix));
                    }
                }

                // If the JS returned nothing, create a minimal "unknown" report.
                if (items.Count == 0)
                {
                    items.Add(new HealthItem(
                        "No items returned",
                        HealthStatus.Info,
                        "healthInterop.run returned no items.",
                        "Ensure wwwroot/js/healthInterop.js is included before blazor.webassembly.js."));
                }

                return new HealthReport(overall, items, Source: "js");
            }
            catch (JSException)
            {
                // Most common: the script isn't loaded yet or the symbol isn't defined.
                return BuildMissingInteropReport();
            }
            catch (OperationCanceledException)
            {
                // Caller requested cancellation.
                return new HealthReport(
                    Overall: HealthStatus.Info,
                    Items: new[]
                    {
                        new HealthItem("Cancelled", HealthStatus.Info, "Health check was cancelled.", "")
                    },
                    Source: "cancelled"
                );
            }
            catch (Exception ex)
            {
                // Any other unexpected error: provide diagnostic details.
                return new HealthReport(
                    Overall: HealthStatus.Fail,
                    Items: new[]
                    {
                        new HealthItem(
                            "Unhandled exception",
                            HealthStatus.Fail,
                            ex.Message,
                            "Open browser devtools (F12) → Console for details. Ensure healthInterop.js is loaded.")
                    },
                    Source: "exception"
                );
            }
        }

        /// <summary>
        /// Convenience wrapper that never throws; always returns a report.
        /// </summary>
        public async Task<HealthReport> RunSafeAsync(int timeoutMs = 8000, CancellationToken cancellationToken = default)
        {
            try
            {
                return await RunAsync(timeoutMs, cancellationToken);
            }
            catch (Exception ex)
            {
                return new HealthReport(
                    Overall: HealthStatus.Fail,
                    Items: new[]
                    {
                        new HealthItem(
                            "HealthService failure",
                            HealthStatus.Fail,
                            ex.Message,
                            "Check console/network. Verify healthInterop.js script order in index.html.")
                    },
                    Source: "catch-all"
                );
            }
        }

        // ---------- Helpers ----------

        private static HealthStatus ParseStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return HealthStatus.Unknown;

            switch (value.Trim().ToLowerInvariant())
            {
                case "pass": return HealthStatus.Pass;
                case "ok": return HealthStatus.Pass;
                case "warn": return HealthStatus.Warn;
                case "warning": return HealthStatus.Warn;
                case "fail": return HealthStatus.Fail;
                case "error": return HealthStatus.Fail;
                case "info": return HealthStatus.Info;
                case "unknown": return HealthStatus.Unknown;
                default: return HealthStatus.Unknown;
            }
        }

        private static string GetStringOrEmpty(JsonElement obj, string propertyName)
        {
            if (obj.TryGetProperty(propertyName, out var el) && el.ValueKind == JsonValueKind.String)
                return el.GetString() ?? string.Empty;
            return string.Empty;
        }

        private static HealthReport BuildMissingInteropReport()
        {
            var items = new List<HealthItem>
            {
                new(
                    "healthInterop.js loaded",
                    HealthStatus.Fail,
                    "The JS function 'healthInterop.run' is not available.",
                    "Include <script src=\"js/healthInterop.js\"></script> BEFORE <script src=\"_framework/blazor.webassembly.js\"></script> in wwwroot/index.html."
                ),
                new(
                    "Leaflet included",
                    HealthStatus.Info,
                    "Not checked (interop missing).",
                    "Ensure Leaflet CSS/JS are referenced in index.html before Blazor."
                ),
                new(
                    "Geolocation availability",
                    HealthStatus.Info,
                    "Not checked (interop missing).",
                    "Use a modern browser. If on Windows, OS Location Services may be disabled by policy."
                )
            };

            return new HealthReport(HealthStatus.Fail, items, Source: "fallback");
        }
    }
}
