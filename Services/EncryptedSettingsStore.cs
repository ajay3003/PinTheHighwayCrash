// Services/EncryptedSettingsStore.cs
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using PinTheHighwayCrash.Models;

namespace PinTheHighwayCrash.Services;

/// <summary>
/// Encrypted store for AdminSettings using AES-GCM with a wrapped data key.
/// Assumes AdminLock handles enrollment/unlock and sets DataKeyHolder.Ref to the unwrapped AES key (JS handle).
/// Persists to localStorage for simplicity; swap to IndexedDB by replacing the localStorage calls.
/// </summary>
public sealed class EncryptedSettingsStore : ISettingsStore
{
    private readonly IJSRuntime _js;
    private readonly AdminCryptoJs _crypto;

    // Keys in web storage
    private const string SettingsKey = "pthc_admin_settings_v2"; // { iv, data }
    private const string LockKey = "pthc_admin_lock_v2";     // { saltBase64, iterations, wrapIv, wrappedKey, fails, lastFailUtc }

    public EncryptedSettingsStore(IJSRuntime js, AdminCryptoJs crypto)
    {
        _js = js;
        _crypto = crypto;
    }

    // ---------------- ISettingsStore ----------------

    /// <summary>Decrypts and returns AdminSettings if unlocked; otherwise returns null.</summary>
    public async Task<AdminSettings?> LoadAsync()
    {
        var settingsJson = await _js.InvokeAsync<string>("localStorage.getItem", SettingsKey);
        if (string.IsNullOrWhiteSpace(settingsJson))
            return null;

        // Must be unlocked: AdminLock should have set DataKeyHolder.Ref
        var dataKeyRef = DataKeyHolder.Ref;
        if (dataKeyRef is null)
            return null;

        using var doc = JsonDocument.Parse(settingsJson);
        var root = doc.RootElement;

        var payload = new
        {
            iv = root.GetProperty("iv").GetString(),
            data = root.GetProperty("data").GetString()
        };

        var decrypted = await _crypto.DecryptJson(dataKeyRef, payload);
        var jsonText = decrypted.GetRawText();
        return JsonSerializer.Deserialize<AdminSettings>(jsonText);
    }

    /// <summary>Encrypts and saves AdminSettings. Requires unlocked data key.</summary>
    public async Task SaveAsync(AdminSettings settings)
    {
        var dataKeyRef = DataKeyHolder.Ref ?? throw new InvalidOperationException("Admin settings are locked.");
        var enc = await _crypto.EncryptJson(dataKeyRef, settings);
        await _js.InvokeVoidAsync("localStorage.setItem", SettingsKey, JsonSerializer.Serialize(enc));
    }

    public async Task<bool> HasAdminLockAsync()
    {
        var lockJson = await _js.InvokeAsync<string>("localStorage.getItem", LockKey);
        return !string.IsNullOrWhiteSpace(lockJson);
    }

    /// <summary>
    /// Legacy signature kept for ISettingsStore compatibility. Here, we interpret the second argument as a JSON
    /// blob produced by AdminLock containing { iv, wrapped } of the data key. We persist expanded fields.
    /// </summary>
    public async Task SetAdminLockAsync(string saltBase64, string wrappedJson, int iterations)
    {
        // wrappedJson should be {"iv":"..","wrapped":".."} (produced by adminCrypto.wrapKey)
        string wrapIv = "", wrappedKey = "";
        try
        {
            using var wdoc = JsonDocument.Parse(wrappedJson);
            var wroot = wdoc.RootElement;
            wrapIv = wroot.GetProperty("iv").GetString() ?? "";
            wrappedKey = wroot.GetProperty("wrapped").GetString() ?? "";
        }
        catch
        {
            // If caller passed a hashBase64 by accident, keep it to avoid breaking; store as wrappedKey.
            wrapIv = "";
            wrappedKey = wrappedJson;
        }

        var lockObj = new AdminLockFull
        {
            SaltBase64 = saltBase64,
            Iterations = iterations,
            WrapIv = wrapIv,
            WrappedKey = wrappedKey,
            Fails = 0,
            LastFailUtc = null
        };

        await _js.InvokeVoidAsync("localStorage.setItem", LockKey, JsonSerializer.Serialize(lockObj));
    }

    /// <summary>
    /// Legacy getter for compatibility. If present, returns (salt, hash, iterations); not used by the secure flow.
    /// </summary>
    public async Task<(string saltBase64, string hashBase64, int iterations)?> GetAdminLockAsync()
    {
        var json = await _js.InvokeAsync<string>("localStorage.getItem", LockKey);
        if (string.IsNullOrWhiteSpace(json)) return null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Try legacy field names, fall back to new
        var salt = root.GetProperty("saltBase64").GetString() ?? "";
        var iterations = root.TryGetProperty("iterations", out var it) ? it.GetInt32() : 300_000;
        var hashOrWrapped = root.TryGetProperty("hashBase64", out var h)
            ? h.GetString() ?? ""
            : root.TryGetProperty("wrappedKey", out var wk) ? wk.GetString() ?? "" : "";

        return (salt, hashOrWrapped, iterations);
    }

    // ---------------- New helpers used by AdminLock ----------------

    /// <summary>Full lock record for AdminLock (salt, iterations, wrapped key, and backoff data).</summary>
    public async Task<AdminLockFull?> GetAdminLockFullAsync()
    {
        var json = await _js.InvokeAsync<string>("localStorage.getItem", LockKey);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var obj = JsonSerializer.Deserialize<AdminLockFull>(json);
            return obj;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Update fail counters/backoff timestamps.</summary>
    public async Task UpdateFailsAsync(int fails, string? lastFailUtc)
    {
        var json = await _js.InvokeAsync<string>("localStorage.getItem", LockKey);
        if (string.IsNullOrWhiteSpace(json)) return;

        var cur = JsonSerializer.Deserialize<AdminLockFull>(json);
        if (cur is null) return;

        cur.Fails = fails;
        cur.LastFailUtc = lastFailUtc;

        await _js.InvokeVoidAsync("localStorage.setItem", LockKey, JsonSerializer.Serialize(cur));
    }
}


