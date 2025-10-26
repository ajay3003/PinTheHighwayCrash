using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using PinTheHighwayCrash;
using PinTheHighwayCrash.Models;
using PinTheHighwayCrash.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ---------------- Root Components ----------------
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ---------------- HttpClient (WASM-safe) ----------------
// NOTE: Browser HttpClient doesn’t support Timeout — use per-call CTS if needed.
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// ---------------- Strongly Typed Options ----------------
builder.Services.Configure<EmergencyOptions>(cfg => builder.Configuration.GetSection("Emergency").Bind(cfg));
builder.Services.Configure<MapOptions>(cfg => builder.Configuration.GetSection("Map").Bind(cfg));
builder.Services.Configure<GeoOptions>(cfg => builder.Configuration.GetSection("Geo").Bind(cfg));
builder.Services.Configure<LoggingOptions>(cfg => builder.Configuration.GetSection("Logging").Bind(cfg));
builder.Services.Configure<FeatureFlags>(cfg => builder.Configuration.GetSection("FeatureFlags").Bind(cfg));

// ---------------- App Services ----------------
builder.Services.AddScoped<GeoService>();
builder.Services.AddScoped<HealthService>();
builder.Services.AddScoped<VerificationService>(); // on-road + forward geocode

// ---------------- Logging ----------------
var min = builder.Configuration["Logging:LogLevel:Default"];
if (Enum.TryParse<LogLevel>(min, out var configuredLevel))
{
    builder.Logging.SetMinimumLevel(configuredLevel);
}
else
{
#if DEBUG
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
    builder.Logging.SetMinimumLevel(LogLevel.Information);
#endif
}

// Optional: disable console logs via Logging:Console:Enable=false
var consoleEnable = builder.Configuration["Logging:Console:Enable"];
if (string.Equals(consoleEnable, "false", StringComparison.OrdinalIgnoreCase))
{
    builder.Logging.ClearProviders();
}

// ---------------- Run ----------------
await builder.Build().RunAsync();
