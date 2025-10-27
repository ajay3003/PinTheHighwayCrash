using System.Text.Json;
using Microsoft.JSInterop;

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// JS interop bridge for cooldown and anti-spam persistence.
    /// Wraps calls to js/cooldown.js.
    /// </summary>
    public sealed class CooldownJs
    {
        private readonly IJSRuntime _js;

        public CooldownJs(IJSRuntime js)
        {
            _js = js;
        }

        /// <summary>
        /// Returns current timestamp in milliseconds (Date.now()).
        /// </summary>
        public ValueTask<long> NowMs()
            => _js.InvokeAsync<long>("pthc.cooldown.nowMs");

        /// <summary>
        /// Retrieves JSON value from storage (local or session).
        /// </summary>
        public ValueTask<JsonElement?> Get(bool useLocal, string key)
            => _js.InvokeAsync<JsonElement?>("pthc.cooldown.get", useLocal, key);

        /// <summary>
        /// Stores an object in storage.
        /// </summary>
        public ValueTask Set(bool useLocal, string key, object obj)
            => _js.InvokeVoidAsync("pthc.cooldown.set", useLocal, key, obj);

        /// <summary>
        /// Removes an entry from storage.
        /// </summary>
        public ValueTask Remove(bool useLocal, string key)
            => _js.InvokeVoidAsync("pthc.cooldown.remove", useLocal, key);

        /// <summary>
        /// Removes all entries that start with the given prefix.
        /// Requires a helper function in js/cooldown.js.
        /// </summary>
        public ValueTask RemoveAllWithPrefix(bool useLocal, string prefix)
            => _js.InvokeVoidAsync("pthc.cooldown.removeAllWithPrefix", useLocal, prefix);
    }
}
