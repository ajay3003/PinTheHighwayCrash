// PinTheHighwayCrash/Services/ICooldownService.cs
using System;
using System.Threading.Tasks;

namespace PinTheHighwayCrash.Services
{
    public interface ICooldownService
    {
        /// <summary>
        /// Attempts to start a cooldown for the given action key.
        /// Respects grace period and existing active cooldown.
        /// Returns true if the action is allowed to proceed now.
        /// </summary>
        Task<bool> TryBeginAsync(string actionKey, int? overrideSeconds = null);

        /// <summary>
        /// Returns remaining cooldown time. Zero means no active cooldown.
        /// </summary>
        Task<TimeSpan> GetRemainingAsync(string actionKey);

        /// <summary>
        /// True if there is an active cooldown for the action key.
        /// </summary>
        Task<bool> IsCoolingDownAsync(string actionKey);

        /// <summary>
        /// Clears any stored cooldown state for the action key.
        /// </summary>
        Task ClearAsync(string actionKey);
    }
}
