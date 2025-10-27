using System.Threading.Tasks;
using PinTheHighwayCrash.Models;

namespace PinTheHighwayCrash.Services
{
    public interface ISettingsStore
    {
        // Settings
        Task<AdminSettings?> LoadAsync();
        Task SaveAsync(AdminSettings settings);

        // Admin lock (legacy-compatible)
        Task<bool> HasAdminLockAsync();

        /// <summary>
        /// For the new encrypted model, pass the wrapped-key JSON in 'hashBase64' ({"iv","wrapped"}).
        /// </summary>
        Task SetAdminLockAsync(string saltBase64, string hashBase64, int iterations);

        Task<(string saltBase64, string hashBase64, int iterations)?> GetAdminLockAsync();

        // Admin lock (full, used by AdminLock.razor)
        Task<AdminLockFull?> GetAdminLockFullAsync();
        Task UpdateFailsAsync(int fails, string? lastFailUtc);
    }
}
