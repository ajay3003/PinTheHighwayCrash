// Services/EncryptedSettingsStore.cs
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using PinTheHighwayCrash.Models;

public sealed class EncryptedSettingsStore : ISettingsStore
{
    private readonly IJSRuntime _js;
    private readonly AdminCryptoJs _crypto;

    private const string StoreKey = "pthc_admin_settings_v2";
    private const string LockKey = "pthc_admin_lock_v2";

    public EncryptedSettingsStore(IJSRuntime js, AdminCryptoJs crypto)
    {
        _js = js;
        _crypto = crypto;
    }

    public async Task<AdminSettings?> LoadAsync()
    {
        var json = await _js.InvokeAsync<string>("localStorage.getItem", StoreKey);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var payload = new
            {
                iv = root.GetProperty("iv").GetString(),
                data = root.GetProperty("data").GetString()
            };

            // Reconstruct the KEK & unwrap the key from lock info
            var lockJson = await _js.InvokeAsync<string>("localStorage.getItem", LockKey);
            if (string.IsNullOrWhiteSpace(lockJson)) return null;
            using var lockDoc = JsonDocument.Parse(lockJson);
            var lockRoot = lockDoc.RootElement;
            var salt = lockRoot.GetProperty("saltBase64").GetString()!;
            var wrapped = new
            {
                iv = lockRoot.GetProperty("wrapIv").GetString()!,
                wrapped = lockRoot.GetProperty("wrappedKey").GetString()!
            };

            // Prompt user for passphrase via JS prompt (or future AdminLock field)
            var pass = await _js.InvokeAsync<string>("prompt", "Enter admin password to unlock settings:");
            if (string.IsNullOrWhiteSpace(pass)) return null;

            var kek = await _crypto.DeriveKek(pass, salt, 300_000);
            var dataKey = await _crypto.UnwrapKey(kek, wrapped);

            var result = await _crypto.DecryptJson(dataKey, payload);
            var jsonText = result.GetRawText();
            return JsonSerializer.Deserialize<AdminSettings>(jsonText);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(AdminSettings settings)
    {
        // Generate data key + derive KEK from password
        var pass = await _js.InvokeAsync<string>("prompt", "Set or confirm admin password:");
        if (string.IsNullOrWhiteSpace(pass))
            return;

        var salt = await _crypto.RandomB64(16);
        var kek = await _crypto.DeriveKek(pass, salt, 300_000);
        var dataKey = await _crypto.GenDataKey();
        var wrapped = await _crypto.WrapKey(kek, dataKey);
        var enc = await _crypto.EncryptJson(dataKey, settings);

        var settingsJson = JsonSerializer.Serialize(enc);
        await _js.InvokeVoidAsync("localStorage.setItem", StoreKey, settingsJson);

        // Store lock info (for re-deriving key)
        var lockPayload = new
        {
            saltBase64 = salt,
            wrapIv = wrapped.GetProperty("iv").GetString(),
            wrappedKey = wrapped.GetProperty("wrapped").GetString()
        };
        await _js.InvokeVoidAsync("localStorage.setItem", LockKey, JsonSerializer.Serialize(lockPayload));
    }

    public async Task<bool> HasAdminLockAsync()
    {
        var lockJson = await _js.InvokeAsync<string>("localStorage.getItem", LockKey);
        return !string.IsNullOrWhiteSpace(lockJson);
    }

    public async Task SetAdminLockAsync(string saltBase64, string hashBase64, int iterations)
    {
        var payload = JsonSerializer.Serialize(new { saltBase64, hashBase64, iterations });
        await _js.InvokeVoidAsync("localStorage.setItem", LockKey, payload);
    }

    public async Task<(string saltBase64, string hashBase64, int iterations)?> GetAdminLockAsync()
    {
        var json = await _js.InvokeAsync<string>("localStorage.getItem", LockKey);
        if (json is null) return null;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (
            root.GetProperty("saltBase64").GetString()!,
            root.TryGetProperty("hashBase64", out var h) ? h.GetString() ?? "" : "",
            root.TryGetProperty("iterations", out var it) ? it.GetInt32() : 300_000
        );
    }
}
