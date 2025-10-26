using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using PinTheHighwayCrash;
using PinTheHighwayCrash.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ---------- Root Components ----------
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ---------- HttpClient ----------
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    Timeout = TimeSpan.FromSeconds(15)
});

// ---------- App Services ----------
builder.Services.AddScoped<GeoService>();
builder.Services.AddScoped<HealthService>();

// ---------- Logging ----------
#if DEBUG
builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
builder.Logging.SetMinimumLevel(LogLevel.Information);
#endif
// NOTE: Blazor WebAssembly automatically logs to the browser console;
// AddConsole() is only for server apps.

// ---------- Run ----------
await builder.Build().RunAsync();
