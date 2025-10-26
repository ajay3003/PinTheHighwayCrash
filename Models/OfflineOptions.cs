#nullable enable
namespace PinTheHighwayCrash.Models;

public sealed class OfflineOptions
{
    public bool EnablePwa { get; set; } = false;
    public bool CacheTiles { get; set; } = true;
    public string[] TileHosts { get; set; } = new[] { "tile.openstreetmap.org" };
    public int MaxCachedTiles { get; set; } = 800;
    public int MaxTileAgeDays { get; set; } = 21;
}
