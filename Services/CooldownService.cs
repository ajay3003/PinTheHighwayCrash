// PinTheHighwayCrash/Services/CooldownService.cs
using System.Text.Json;
using Microsoft.Extensions.Options;
using PinTheHighwayCrash.Models;

namespace PinTheHighwayCrash.Services;

public sealed class CooldownService : ICooldownService
{
    private readonly IOptionsMonitor<CooldownOptions> _opt;
    private readonly CooldownJs _js;

    public CooldownService(IOptionsMonitor<CooldownOptions> opt, CooldownJs js)
    {
        _opt = opt;
        _js = js;
    }

    private string BuildKey(string action) => $"{_opt.CurrentValue.Persist.KeyPrefix}{action}";
    private static int ClampNonNegative(int value) => value < 0 ? 0 : value;

    private int SecondsFor(string action)
    {
        var o = _opt.CurrentValue;
        if (o.TestMode.Enabled) return ClampNonNegative(o.TestMode.OverrideAllActionsSeconds);

        return action.ToLowerInvariant() switch
        {
            "call" => ClampNonNegative(o.PerAction.CallSeconds),
            "sms" => ClampNonNegative(o.PerAction.SmsSeconds),
            "whatsapp" => ClampNonNegative(o.PerAction.WhatsAppSeconds),
            "email" => ClampNonNegative(o.PerAction.EmailSeconds),
            _ => ClampNonNegative(o.DefaultDurationSeconds)
        };
    }

    public async Task<bool> TryBeginAsync(string actionKey, int? overrideSeconds = null)
    {
        var cfg = _opt.CurrentValue;
        if (!cfg.Enabled) return true;
        if (cfg.Debug.BypassWhenShowDebugPanel) return true;

        var key = BuildKey(actionKey);
        var useLocal = cfg.Persist.UseLocalStorage;
        var now = await _js.NowMs();

        // read raw
        var raw = await _js.GetRaw(useLocal, key);
        if (!string.IsNullOrEmpty(raw))
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var until = root.TryGetProperty("until", out var untilProp) ? untilProp.GetInt64() : 0L;
            var started = root.TryGetProperty("started", out var sProp) ? sProp.GetInt64() : now;

            var persistWindowMs = (long)cfg.Persist.PersistAcrossReloadMinutes * 60_000L;
            if (persistWindowMs > 0 && now - started > persistWindowMs)
            {
                await _js.Remove(useLocal, key);
            }
            else if (until > 0 && now < until)
            {
                var sinceStartSec = (now - started) / 1000.0;
                if (sinceStartSec <= cfg.GracePeriodSeconds) return true;
                return false;
            }
        }

        var seconds = ClampNonNegative(overrideSeconds ?? SecondsFor(actionKey));
        var untilNew = now + seconds * 1000L;
        var payload = new { started = now, until = untilNew, action = actionKey };
        await _js.Set(useLocal, key, payload);
        return true;
    }

    public async Task<TimeSpan> GetRemainingAsync(string actionKey)
    {
        var cfg = _opt.CurrentValue;
        if (!cfg.Enabled) return TimeSpan.Zero;

        var key = BuildKey(actionKey);
        var useLocal = cfg.Persist.UseLocalStorage;
        var raw = await _js.GetRaw(useLocal, key);
        if (string.IsNullOrEmpty(raw)) return TimeSpan.Zero;

        var now = await _js.NowMs();

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var until = root.TryGetProperty("until", out var uProp) ? uProp.GetInt64() : now;
        var started = root.TryGetProperty("started", out var sProp) ? sProp.GetInt64() : now;

        var persistWindowMs = (long)cfg.Persist.PersistAcrossReloadMinutes * 60_000L;
        if (persistWindowMs > 0 && now - started > persistWindowMs)
        {
            if (cfg.Persist.CleanupOnExpire) await _js.Remove(useLocal, key);
            return TimeSpan.Zero;
        }

        var ms = until - now;
        if (ms <= 0)
        {
            if (cfg.Persist.CleanupOnExpire) await _js.Remove(useLocal, key);
            return TimeSpan.Zero;
        }

        return TimeSpan.FromMilliseconds(ms);
    }

    public async Task<bool> IsCoolingDownAsync(string actionKey)
        => (await GetRemainingAsync(actionKey)) > TimeSpan.Zero;

    public async Task ClearAsync(string actionKey)
    {
        var cfg = _opt.CurrentValue;
        await _js.Remove(cfg.Persist.UseLocalStorage, BuildKey(actionKey));
    }
}
