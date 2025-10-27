namespace PinTheHighwayCrash.Models
{
    /// <summary>
    /// Represents the encrypted admin lock metadata stored locally.
    /// </summary>
    public sealed class AdminLockFull
    {
        /// <summary>
        /// Base64-encoded salt used in PBKDF2 key derivation.
        /// </summary>
        public string SaltBase64 { get; set; } = string.Empty;

        /// <summary>
        /// Number of PBKDF2 iterations (e.g., 300,000).
        /// </summary>
        public int Iterations { get; set; } = 300_000;

        /// <summary>
        /// AES-GCM IV used when wrapping the data key.
        /// </summary>
        public string WrapIv { get; set; } = string.Empty;

        /// <summary>
        /// Base64-encoded AES-GCM ciphertext of the wrapped data key.
        /// </summary>
        public string WrappedKey { get; set; } = string.Empty;

        /// <summary>
        /// Consecutive failed unlock attempts.
        /// </summary>
        public int Fails { get; set; } = 0;

        /// <summary>
        /// Last failed unlock attempt timestamp (UTC ISO string).
        /// </summary>
        public string? LastFailUtc { get; set; }
    }
}
