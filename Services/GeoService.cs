using Microsoft.JSInterop;
using System.Text.Json;

namespace PinTheHighwayCrash.Services;

public class GeoService
{
    private readonly IJSRuntime _js;
    public GeoService(IJSRuntime js) => _js = js;

    public record Position(double Latitude, double Longitude, double AccuracyMeters);

    public async Task<Position?> GetCurrentPositionAsync(int timeoutMs = 10000, bool highAccuracy = true)
    {
        try
        {
            var pos = await _js.InvokeAsync<JsonElement>("navigator.geolocation.getCurrentPositionPromise", new {
                enableHighAccuracy = highAccuracy, timeout = timeoutMs, maximumAge = 0
            });
            var coords = pos.GetProperty("coords");
            return new Position(
                coords.GetProperty("latitude").GetDouble(),
                coords.GetProperty("longitude").GetDouble(),
                coords.GetProperty("accuracy").GetDouble()
            );
        }
        catch { return null; }
    }

    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat/2)*Math.Sin(dLat/2) +
                   Math.Cos(lat1 * Math.PI/180.0) * Math.Cos(lat2 * Math.PI/180.0) *
                   Math.Sin(dLon/2)*Math.Sin(dLon/2);
        return 2 * R * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
    }
}
