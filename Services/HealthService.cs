using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace PinTheHighwayCrash.Services;

/// <summary>
/// Performs environment and browser capability checks via JS interop (`healthInterop.js`).
/// Provides a structured `HealthReport` with diagnostic information.
/// </summary>
public sealed class HealthService
{
    private readonly IJSRuntime _js;
    private readonly ILogger<HealthService>? _logger;

    public HealthService(IJSRuntime js, ILogger<HealthService>? logger = null)
    {
        _js = js;
        _logger = logger;
    }

    // ---------- Models ----------

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

    // ---------- Public API ----------

    /// <summary>
    /// Executes JS-based environment health checks by invoking <c>healthInterop.run(timeoutMs)</c>.
    /// Returns a structured <see cref="HealthReport"/>; falls back to default info if interop fails.
    /// </summary>
    public async Task<HealthReport> RunAsync(int timeoutMs = 8000, CancellationToken cancellationToken = default)
    {
        try
        {
            // JS returns { overall: string, items: [] }
            var json = await _js.InvokeAsync<JsonElement>(
                "healthInterop.run", cancellationToken, timeoutMs);

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

            if (items.Count == 0)
            {
                items.Add(new HealthItem(
                    "No items returned",
                    HealthStatus.Info,
                    "healthInterop.run returned no items.",
                    "Ensure js/healthInterop.js is included before blazor.webassembly.js."));
            }

            return new HealthReport(overall, items, Source: "js");
        }
        catch (JSException jsEx)
        {
            _logger?.LogWarning(jsEx, "healthInterop.run not available in JS.");
            return BuildMissingInteropReport();
        }
        catch (OperationCanceledException)
        {
            return new HealthReport(
                Overall: HealthStatus.Info,
                Items: new[]
                {
                    new HealthItem(
                        "Cancelled",
                        HealthStatus.Info,
                        "Health check was cancelled by caller.",
                        "")
                },
                Source: "cancelled"
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unhandled error while running HealthService.");
            return new HealthReport(
                Overall: HealthStatus.Fail,
                Items: new[]
                {
                    new HealthItem(
                        "Unhandled exception",
                        HealthStatus.Fail,
                        ex.Message,
                        "Open browser devtools (F12) → Console for details. Verify healthInterop.js is loaded.")
                },
                Source: "exception"
            );
        }
    }

    /// <summary>
    /// Convenience wrapper that never throws; always returns a <see cref="HealthReport"/>.
    /// </summary>
    public async Task<HealthReport> RunSafeAsync(int timeoutMs = 8000, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunAsync(timeoutMs, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "HealthService.RunSafeAsync encountered a failure.");

            return new HealthReport(
                Overall: HealthStatus.Fail,
                Items: new[]
                {
                    new HealthItem(
                        "HealthService failure",
                        HealthStatus.Fail,
                        ex.Message,
                        "Check console/network. Verify js/healthInterop.js script order in index.html.")
                },
                Source: "catch-all"
            );
        }
    }

    // ---------- Helpers ----------

    private static HealthStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return HealthStatus.Unknown;

        return value.Trim().ToLowerInvariant() switch
        {
            "pass" or "ok" => HealthStatus.Pass,
            "warn" or "warning" => HealthStatus.Warn,
            "fail" or "error" => HealthStatus.Fail,
            "info" => HealthStatus.Info,
            "unknown" => HealthStatus.Unknown,
            _ => HealthStatus.Unknown
        };
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
