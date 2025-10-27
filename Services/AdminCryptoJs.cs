using Microsoft.JSInterop;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class AdminCryptoJs : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public AdminCryptoJs(IJSRuntime js) => _js = js;

    private async ValueTask<IJSObjectReference> Module()
    {
        if (_module is null)
        {
            // Import the ES module
            _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/adminCrypto.js");
        }
        return _module;
    }

    // Proxy methods to the module's exports

    public async ValueTask<string> RandomB64(int len = 16)
        => await (await Module()).InvokeAsync<string>("randomB64", len);

    public async ValueTask<IJSObjectReference> DeriveKek(string pass, string saltB64, int iterations)
        => await (await Module()).InvokeAsync<IJSObjectReference>("deriveKEK", pass, saltB64, iterations);

    public async ValueTask<IJSObjectReference> GenDataKey()
        => await (await Module()).InvokeAsync<IJSObjectReference>("genDataKey");

    public async ValueTask<JsonElement> WrapKey(IJSObjectReference kek, IJSObjectReference dataKey)
        => await (await Module()).InvokeAsync<JsonElement>("wrapKey", kek, dataKey);

    public async ValueTask<IJSObjectReference> UnwrapKey(IJSObjectReference kek, object wrapped)
        => await (await Module()).InvokeAsync<IJSObjectReference>("unwrapKey", kek, wrapped);

    public async ValueTask<JsonElement> EncryptJson(IJSObjectReference dataKey, object obj)
        => await (await Module()).InvokeAsync<JsonElement>("encryptJson", dataKey, obj);

    public async ValueTask<JsonElement> DecryptJson(IJSObjectReference dataKey, object payload)
        => await (await Module()).InvokeAsync<JsonElement>("decryptJson", dataKey, payload);

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch { /* ignore */ }
            _module = null;
        }
    }
}
