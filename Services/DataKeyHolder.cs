using Microsoft.JSInterop;

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// Holds a live reference to the decrypted AES-GCM key in memory.
    /// 
    /// The key itself never leaves the browser's Web Crypto API — only a JS object
    /// reference is stored here. This lets Blazor call encryption/decryption
    /// methods through JavaScript securely while the session is unlocked.
    /// 
    /// Call <see cref="Clear"/> on logout or reload to forget the key.
    /// </summary>
    public static class DataKeyHolder
    {
        /// <summary>
        /// Reference to the unwrapped AES key (lives in JS, referenced via .NET proxy).
        /// </summary>
        public static IJSObjectReference? Ref { get; set; }

        /// <summary>
        /// Clears the key reference (used when relocking or on logout).
        /// </summary>
        public static void Clear() => Ref = null;
    }
}
