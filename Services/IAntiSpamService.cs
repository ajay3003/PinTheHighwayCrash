// PinTheHighwayCrash/Services/IAntiSpamService.cs
#nullable enable

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// Defines a lightweight client-side anti-spam guard interface.
    /// Prevents users from repeatedly sending or triggering the same actions
    /// (Call, SMS, WhatsApp, Email) within a configured cooldown, cell radius,
    /// or daily cap — all enforced via browser storage (no backend dependency).
    /// </summary>
    public interface IAntiSpamService
    {
        /// <summary>
        /// Runs all anti-spam checks before executing an action.
        /// </summary>
        /// <param name="actionKey">Action key such as "call", "sms", "whatsapp", or "email".</param>
        /// <param name="pinLat">Pinned latitude for duplicate-location detection.</param>
        /// <param name="pinLng">Pinned longitude for duplicate-location detection.</param>
        /// <returns>
        /// A decision object indicating whether the action is allowed,
        /// and (if denied) a human-readable reason.
        /// </returns>
        Task<AntiSpamDecision> GuardAsync(string actionKey, double pinLat, double pinLng);

        /// <summary>
        /// Records a successful action (updates duplicate lock, global lock, and counters).
        /// </summary>
        Task RecordAsync(string actionKey, double pinLat, double pinLng);

        /// <summary>
        /// Returns the number of actions used today for the specified key.
        /// </summary>
        Task<int> GetDailyCountAsync(string actionKey);

        /// <summary>
        /// Clears all stored anti-spam data (for debugging or reset).
        /// </summary>
        Task ClearAsync();
    }

    /// <summary>
    /// Result returned by <see cref="IAntiSpamService.GuardAsync"/>.
    /// </summary>
    public sealed class AntiSpamDecision
    {
        /// <summary>
        /// True if allowed to proceed; false if denied.
        /// </summary>
        public bool Allowed { get; init; }

        /// <summary>
        /// Human-readable reason if denied; null when allowed.
        /// </summary>
        public string? Reason { get; init; }

        public static AntiSpamDecision Allow() => new() { Allowed = true };
        public static AntiSpamDecision Deny(string reason) => new() { Allowed = false, Reason = reason };
    }
}
