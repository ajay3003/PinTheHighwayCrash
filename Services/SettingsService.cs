using PinTheHighwayCrash.Services;
using System;
using System.Threading.Tasks;

public sealed class SettingsService
{
    private readonly ISettingsStore _store;
    public AdminSettings Current { get; private set; } = new();
    public event Action? Changed;

    public SettingsService(ISettingsStore store) => _store = store;

    public async Task InitializeAsync()
    {
        var loaded = await _store.LoadAsync();
        if (loaded != null)
            Current = loaded;
        Changed?.Invoke();
    }

    public async Task UpdateAsync(AdminSettings updated)
    {
        Current = updated;
        await _store.SaveAsync(Current);
        Changed?.Invoke();
    }
}
