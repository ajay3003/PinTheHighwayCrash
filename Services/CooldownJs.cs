// PinTheHighwayCrash/Services/CooldownJs.cs
using Microsoft.JSInterop;
using System.Text.Json;

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// JS interop bridge for cooldown/anti-spam persistence (local/session storage).
    /// </summary>
    public sealed class CooldownJs
    {
        private readonly IJSRuntime _js;

        public CooldownJs(IJSRuntime js) => _js = js;

        public ValueTask<long> NowMs()
            => _js.InvokeAsync<long>("pthc.cooldown.nowMs");

        /// <summary>
        /// Prefer this: gets raw JSON string or null. Avoids Nullable&lt;JsonElement&gt; casting issues.
        /// </summary>
        public ValueTask<string?> GetRaw(bool useLocal, string key)
            => _js.InvokeAsync<string?>("pthc.cooldown.getRaw", useLocal, key);

        // Kept for compatibility (no longer used by our services)
        public ValueTask<JsonElement?> Get(bool useLocal, string key)
            => _js.InvokeAsync<JsonElement?>("pthc.cooldown.get", useLocal, key);

        public ValueTask Set(bool useLocal, string key, object obj)
            => _js.InvokeVoidAsync("pthc.cooldown.set", useLocal, key, obj);

        public ValueTask Remove(bool useLocal, string key)
            => _js.InvokeVoidAsync("pthc.cooldown.remove", useLocal, key);

        public ValueTask RemoveAllWithPrefix(bool useLocal, string prefix)
            => _js.InvokeVoidAsync("pthc.cooldown.removeAllWithPrefix", useLocal, prefix);
    }
}
