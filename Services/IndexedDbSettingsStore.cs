using System.Text.Json;
using System.Threading.Tasks;


namespace PinTheHighwayCrash.Services;

public sealed class IndexedDbSettingsStore : ISettingsStore
{
    private readonly AdminStoreJs _js;
    private const int DefaultIterations = 250_000;

    public IndexedDbSettingsStore(AdminStoreJs js) => _js = js;

    public async Task<AdminSettings?> LoadAsync()
    {
        var s = await _js.LoadAsync();
        return s;
    }

    public Task SaveAsync(AdminSettings settings) => _js.SaveAsync(settings);

    public async Task<bool> HasAdminLockAsync() => (await _js.GetLockAsync()) is not null;

    public async Task SetAdminLockAsync(string saltBase64, string hashBase64, int iterations)
    {
        var payload = JsonSerializer.Serialize(new { saltBase64, hashBase64, iterations });
        await _js.SetLockAsync(payload);
    }

    public async Task<(string saltBase64, string hashBase64, int iterations)?> GetAdminLockAsync()
    {
        var json = await _js.GetLockAsync();
        if (json is null) return null;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (root.GetProperty("saltBase64").GetString()!,
                root.GetProperty("hashBase64").GetString()!,
                root.GetProperty("iterations").GetInt32());
    }
}
