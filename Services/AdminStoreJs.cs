using Microsoft.JSInterop;
using PinTheHighwayCrash.Models;
using System.Text.Json;
using System.Threading.Tasks;

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// Provides JavaScript interop access to local IndexedDB / localStorage admin settings.
    /// 
    /// This acts as the bridge between Blazor (.NET) and the JS side:
    /// - loadSettings / saveSettings → read/write settings in browser storage
    /// - getLock / setLock → manage encrypted admin lock metadata
    /// - pbkdf2Hash / randomSaltBase64 → derive passphrase hashes securely in JS
    /// </summary>
    public sealed class AdminStoreJs
    {
        private readonly IJSRuntime _js;

        public AdminStoreJs(IJSRuntime js) => _js = js;

        // -------------------------------------------------------
        // Settings CRUD
        // -------------------------------------------------------

        /// <summary>
        /// Loads the current admin settings from browser storage.
        /// </summary>
        public async Task<AdminSettings?> LoadAsync()
        {
            var json = await _js.InvokeAsync<string?>("loadSettings");
            return json is null ? null : JsonSerializer.Deserialize<AdminSettings>(json);
        }

        /// <summary>
        /// Saves the given settings to browser storage.
        /// </summary>
        public Task SaveAsync(AdminSettings settings)
        {
            var json = JsonSerializer.Serialize(settings);
            return _js.InvokeVoidAsync("saveSettings", json).AsTask();
        }

        // -------------------------------------------------------
        // Admin Lock (encrypted local key)
        // -------------------------------------------------------

        /// <summary>
        /// Gets the raw JSON representing the stored admin lock, if any.
        /// </summary>
        public Task<string?> GetLockAsync() =>
            _js.InvokeAsync<string?>("getLock").AsTask();

        /// <summary>
        /// Persists a new or updated admin lock JSON payload.
        /// </summary>
        public Task SetLockAsync(string json) =>
            _js.InvokeVoidAsync("setLock", json).AsTask();

        // -------------------------------------------------------
        // Crypto helpers (delegated to JS)
        // -------------------------------------------------------

        /// <summary>
        /// Requests a random Base64-encoded salt from JS (crypto-safe RNG).
        /// </summary>
        public Task<string> RandomSaltBase64Async() =>
            _js.InvokeAsync<string>("randomSaltBase64").AsTask();

        /// <summary>
        /// Performs PBKDF2 hashing in JS using the Web Crypto API.
        /// </summary>
        public Task<string> Pbkdf2Async(string password, string saltBase64, int iterations) =>
            _js.InvokeAsync<string>("pbkdf2Hash", password, saltBase64, iterations).AsTask();
    }
}
