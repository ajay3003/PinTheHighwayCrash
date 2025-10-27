using System.Threading.Tasks;

public interface ISettingsStore
{
    Task<AdminSettings?> LoadAsync();
    Task SaveAsync(AdminSettings settings);

    // Admin lock
    Task<bool> HasAdminLockAsync();
    Task SetAdminLockAsync(string saltBase64, string hashBase64, int iterations);
    Task<(string saltBase64, string hashBase64, int iterations)?> GetAdminLockAsync();
}
