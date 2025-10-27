using Microsoft.JSInterop;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class AdminStoreJs
{
    private readonly IJSRuntime _js;
    public AdminStoreJs(IJSRuntime js) => _js = js;

    public async Task<AdminSettings?> LoadAsync()
    {
        var json = await _js.InvokeAsync<string?>("loadSettings");
        return json is null ? null : JsonSerializer.Deserialize<AdminSettings>(json);
    }

    public Task SaveAsync(AdminSettings settings)
    {
        var json = JsonSerializer.Serialize(settings);
        return _js.InvokeVoidAsync("saveSettings", json).AsTask();
    }

    public Task<string?> GetLockAsync() => _js.InvokeAsync<string?>("getLock").AsTask();
    public Task SetLockAsync(string json) => _js.InvokeVoidAsync("setLock", json).AsTask();

    public Task<string> RandomSaltBase64Async() => _js.InvokeAsync<string>("randomSaltBase64").AsTask();

    public Task<string> Pbkdf2Async(string password, string saltBase64, int iterations) =>
        _js.InvokeAsync<string>("pbkdf2Hash", password, saltBase64, iterations).AsTask();
}
