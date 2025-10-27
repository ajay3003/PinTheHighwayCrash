// PinTheHighwayCrash/Services/AntiSpamService.cs
#nullable enable
using System.Text.Json;
using Microsoft.Extensions.Options;
using PinTheHighwayCrash.Models;

namespace PinTheHighwayCrash.Services
{
    /// <summary>
    /// Client-side anti-spam guard that prevents repeated actions (call/SMS/etc.)
    /// within time or location constraints, using browser storage.
    /// </summary>
    public sealed class AntiSpamService : IAntiSpamService
    {
        private readonly IOptionsMonitor<AntiSpamOptions> _opt;
        private readonly CooldownJs _js;

        public AntiSpamService(IOptionsMonitor<AntiSpamOptions> opt, CooldownJs js)
        {
            _opt = opt;
            _js = js;
        }

        private string BuildKey(string suffix) => $"{_opt.CurrentValue.Storage.KeyPrefix}{suffix}";

        /// <inheritdoc />
        public async Task<AntiSpamDecision> GuardAsync(string actionKey, double pinLat, double pinLng)
        {
            var cfg = _opt.CurrentValue;
            if (!cfg.Enabled)
                return AntiSpamDecision.Allow();

            var now = await _js.NowMs();
            var useLocal = cfg.Storage.UseLocalStorage;

            // ---- Global post-action lockout ----
            var lockKey = BuildKey("global_lock");
            var lockJson = await _js.Get(useLocal, lockKey);
            if (lockJson.HasValue &&
                lockJson.Value.TryGetProperty("until", out var untilProp) &&
                untilProp.GetInt64() > now)
            {
                var remain = untilProp.GetInt64() - now;
                var sec = (int)Math.Ceiling(remain / 1000.0);
                return AntiSpamDecision.Deny($"Please wait {sec}s before sending another report.");
            }

            // ---- Duplicate (same cell) check ----
            var cellKey = BuildCellKey(pinLat, pinLng);
            var cellJson = await _js.Get(useLocal, cellKey);
            if (cellJson.HasValue &&
                cellJson.Value.TryGetProperty("until", out var duUntil) &&
                duUntil.GetInt64() > now)
            {
                var remain = duUntil.GetInt64() - now;
                var min = Math.Ceiling(remain / 60000.0);
                return AntiSpamDecision.Deny($"Duplicate report blocked for {min:0} more min in this area.");
            }

            // ---- Daily cap check ----
            var dailyCount = await GetDailyCountAsync(actionKey);
            var limit = cfg.DailyCaps.ForAction(actionKey);
            if (limit > 0 && dailyCount >= limit)
            {
                return AntiSpamDecision.Deny($"Daily limit reached for {actionKey} ({limit}).");
            }

            return AntiSpamDecision.Allow();
        }

        /// <inheritdoc />
        public async Task RecordAsync(string actionKey, double pinLat, double pinLng)
        {
            var cfg = _opt.CurrentValue;
            var now = await _js.NowMs();
            var useLocal = cfg.Storage.UseLocalStorage;

            // ---- Set post-action global lockout ----
            var lockoutMs = cfg.PostActionLockoutSeconds * 1000L;
            if (lockoutMs > 0)
            {
                var until = now + lockoutMs;
                await _js.Set(useLocal, BuildKey("global_lock"), new { until });
            }

            // ---- Set duplicate cell window ----
            var cellMs = cfg.DuplicateWindowMinutes * 60_000L;
            if (cellMs > 0)
            {
                var until = now + cellMs;
                await _js.Set(useLocal, BuildCellKey(pinLat, pinLng), new { until });
            }

            // ---- Increment daily counter ----
            var dayKey = BuildKey($"{actionKey}_daily");
            var today = DateTime.UtcNow.Date;
            var existing = await _js.Get(useLocal, dayKey);

            int count = 0;
            string? storedDate = null;

            if (existing.HasValue &&
                existing.Value.TryGetProperty("date", out var dateProp) &&
                existing.Value.TryGetProperty("count", out var countProp))
            {
                storedDate = dateProp.GetString();
                count = countProp.GetInt32();
            }

            if (storedDate != today.ToString("yyyy-MM-dd"))
                count = 0;

            count++;
            await _js.Set(useLocal, dayKey, new { date = today.ToString("yyyy-MM-dd"), count });
        }

        /// <inheritdoc />
        public async Task<int> GetDailyCountAsync(string actionKey)
        {
            var useLocal = _opt.CurrentValue.Storage.UseLocalStorage;
            var dayKey = BuildKey($"{actionKey}_daily");
            var json = await _js.Get(useLocal, dayKey);
            if (!json.HasValue) return 0;

            if (json.Value.TryGetProperty("date", out var dateProp) &&
                json.Value.TryGetProperty("count", out var countProp))
            {
                var storedDate = dateProp.GetString();
                if (storedDate == DateTime.UtcNow.Date.ToString("yyyy-MM-dd"))
                    return countProp.GetInt32();
            }
            return 0;
        }

        /// <inheritdoc />
        public async Task ClearAsync()
        {
            var useLocal = _opt.CurrentValue.Storage.UseLocalStorage;
            await _js.RemoveAllWithPrefix(useLocal, _opt.CurrentValue.Storage.KeyPrefix);
        }

        // ---- Helpers ----

        private string BuildCellKey(double lat, double lng)
        {
            var cellSize = _opt.CurrentValue.CellSizeMeters;
            const double metersPerDegree = 111_320.0; // rough at equator

            var latCell = Math.Floor(lat * metersPerDegree / cellSize);
            var lngCell = Math.Floor(lng * metersPerDegree / cellSize);
            return BuildKey($"cell_{latCell}_{lngCell}");
        }
    }
}
