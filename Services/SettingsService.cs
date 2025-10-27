// Services/SettingsService.cs
using System;
using System.Threading.Tasks;
using PinTheHighwayCrash.Models;

namespace PinTheHighwayCrash.Services
{
    public sealed class SettingsService
    {
        private readonly ISettingsStore _store;

        public AdminSettings Current { get; private set; } = new();
        public event Action? Changed;

        public SettingsService(ISettingsStore store) => _store = store;

        /// <summary>
        /// Safe to call before unlock; if decryption isn't possible yet, leaves defaults.
        /// </summary>
        public async Task InitializeAsync()
        {
            var loaded = await _store.LoadAsync();
            if (loaded != null)
                Current = loaded;

            Changed?.Invoke();
        }

        /// <summary>
        /// Loads from encrypted store (use this after AdminLock unlocks).
        /// Returns the loaded settings or null if none exist.
        /// </summary>
        public async Task<AdminSettings?> LoadAsync()
        {
            var loaded = await _store.LoadAsync();
            if (loaded != null)
                Current = loaded;

            Changed?.Invoke();
            return loaded;
        }

        /// <summary>
        /// Updates and persists settings.
        /// </summary>
        public async Task UpdateAsync(AdminSettings updated)
        {
            Current = updated;
            await _store.SaveAsync(Current);
            Changed?.Invoke();
        }
    }
}
