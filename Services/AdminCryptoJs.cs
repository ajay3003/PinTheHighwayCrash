using Microsoft.JSInterop;
using System.Text.Json;
using System.Threading.Tasks;

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// Provides async interop with adminCrypto.js (AES-GCM + PBKDF2).
    /// 
    /// Handles secure passphrase-derived key generation, key wrapping/unwrapping,
    /// and JSON encryption/decryption using the Web Crypto API.
    /// </summary>
    public sealed class AdminCryptoJs : IAsyncDisposable
    {
        private readonly IJSRuntime _js;
        private IJSObjectReference? _module;

        public AdminCryptoJs(IJSRuntime js) => _js = js;

        /// <summary>
        /// Lazy-loads the adminCrypto.js ES module.
        /// </summary>
        private async ValueTask<IJSObjectReference> Module()
        {
            if (_module is null)
            {
                _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/adminCrypto.js");
            }
            return _module;
        }

        // ---------------------------------------------------------------------
        //  Proxy methods to module exports
        // ---------------------------------------------------------------------

        /// <summary>
        /// Generates a cryptographically random Base64 string (default 16 bytes).
        /// </summary>
        public async ValueTask<string> RandomB64(int len = 16)
            => await (await Module()).InvokeAsync<string>("randomB64", len);

        /// <summary>
        /// Derives a Key Encryption Key (KEK) from a passphrase and salt.
        /// </summary>
        public async ValueTask<IJSObjectReference> DeriveKek(string pass, string saltB64, int iterations)
            => await (await Module()).InvokeAsync<IJSObjectReference>("deriveKEK", pass, saltB64, iterations);

        /// <summary>
        /// Generates a new AES-GCM data key.
        /// </summary>
        public async ValueTask<IJSObjectReference> GenDataKey()
            => await (await Module()).InvokeAsync<IJSObjectReference>("genDataKey");

        /// <summary>
        /// Wraps (encrypts) a data key using the KEK.
        /// Returns a JSON object with iv + ciphertext fields.
        /// </summary>
        public async ValueTask<JsonElement> WrapKey(IJSObjectReference kek, IJSObjectReference dataKey)
            => await (await Module()).InvokeAsync<JsonElement>("wrapKey", kek, dataKey);

        /// <summary>
        /// Unwraps (decrypts) a wrapped key object back to a JS key reference.
        /// </summary>
        public async ValueTask<IJSObjectReference> UnwrapKey(IJSObjectReference kek, object wrapped)
            => await (await Module()).InvokeAsync<IJSObjectReference>("unwrapKey", kek, wrapped);

        /// <summary>
        /// Encrypts a .NET object into a JSON AES-GCM payload.
        /// </summary>
        public async ValueTask<JsonElement> EncryptJson(IJSObjectReference dataKey, object obj)
            => await (await Module()).InvokeAsync<JsonElement>("encryptJson", dataKey, obj);

        /// <summary>
        /// Decrypts a JSON AES-GCM payload back into an object.
        /// </summary>
        public async ValueTask<JsonElement> DecryptJson(IJSObjectReference dataKey, object payload)
            => await (await Module()).InvokeAsync<JsonElement>("decryptJson", dataKey, payload);

        // ---------------------------------------------------------------------
        //  Cleanup
        // ---------------------------------------------------------------------

        public async ValueTask DisposeAsync()
        {
            if (_module is not null)
            {
                try
                {
                    await _module.DisposeAsync();
                }
                catch
                {
                    // safely ignore disposal errors (browser reload, etc.)
                }
                _module = null;
            }
        }
    }
}
