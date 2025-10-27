// PinTheHighwayCrash/Services/AntiSpamService.cs
#nullable enable
using System.Text.Json;
using Microsoft.Extensions.Options;
using PinTheHighwayCrash.Models;

namespace PinTheHighwayCrash.Services
{
    public sealed class AntiSpamService : IAntiSpamService
    {
        private readonly IOptionsMonitor<AntiSpamOptions> _opt;
        private readonly CooldownJs _js;

        public AntiSpamService(IOptionsMonitor<AntiSpamOptions> opt, CooldownJs js)
        {
            _opt = opt;
            _js = js;
        }

        private AntiSpamOptions O => _opt.CurrentValue;
        private string Pfx => O.Storage.KeyPrefix;
        private bool UseLocal => O.Storage.UseLocalStorage;

        private string BuildKey(string suffix) => $"{Pfx}{suffix}";

        public async Task<AntiSpamDecision> GuardAsync(string actionKey, double pinLat, double pinLng)
        {
            if (!O.Enabled) return AntiSpamDecision.Allow();

            var now = await _js.NowMs();

            // Global lock
            var lockRaw = await _js.GetRaw(UseLocal, BuildKey("global_lock"));
            if (!string.IsNullOrEmpty(lockRaw))
            {
                using var doc = JsonDocument.Parse(lockRaw);
                if (doc.RootElement.TryGetProperty("until", out var u))
                {
                    var until = u.GetInt64();
                    if (now < until)
                    {
                        var sec = Math.Max(1, (int)Math.Ceiling((until - now) / 1000.0));
                        return AntiSpamDecision.Deny($"Please wait {sec}s before using another channel.");
                    }
                }
            }

            // Duplicate (same cell)
            var cellKey = BuildCellKey(pinLat, pinLng);
            var dupeRaw = await _js.GetRaw(UseLocal, cellKey);
            if (!string.IsNullOrEmpty(dupeRaw))
            {
                using var doc = JsonDocument.Parse(dupeRaw);
                if (doc.RootElement.TryGetProperty("until", out var u))
                {
                    var until = u.GetInt64();
                    if (now < until)
                    {
                        var minsLeft = Math.Max(1, (int)Math.Ceiling((until - now) / 60000.0));
                        return AntiSpamDecision.Deny($"A report was recently sent for this location. Try again in ~{minsLeft} min.");
                    }
                }
            }

            // Daily cap
            var used = await GetDailyCountAsync(actionKey);
            var limit = O.DailyCaps.ForAction(actionKey);
            if (limit > 0 && used >= limit)
                return AntiSpamDecision.Deny($"Daily limit reached for {actionKey}.");

            return AntiSpamDecision.Allow();
        }

        public async Task RecordAsync(string actionKey, double pinLat, double pinLng)
        {
            if (!O.Enabled) return;

            var now = await _js.NowMs();

            // Global lock
            if (O.PostActionLockoutSeconds > 0)
            {
                var until = now + (long)O.PostActionLockoutSeconds * 1000L;
                await _js.Set(UseLocal, BuildKey("global_lock"), new { until });
            }

            // Duplicate window
            if (O.DuplicateWindowMinutes > 0)
            {
                var until = now + (long)O.DuplicateWindowMinutes * 60_000L;
                await _js.Set(UseLocal, BuildCellKey(pinLat, pinLng), new { until });
            }

            // Daily count
            var dayKey = BuildKey($"{actionKey}_daily");
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            int count = 0;

            var raw = await _js.GetRaw(UseLocal, dayKey);
            if (!string.IsNullOrEmpty(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                var date = root.TryGetProperty("date", out var d) ? d.GetString() : null;
                if (date == today && root.TryGetProperty("count", out var c)) count = c.GetInt32();
            }

            await _js.Set(UseLocal, dayKey, new { date = today, count = count + 1 });
        }

        public async Task<int> GetDailyCountAsync(string actionKey)
        {
            var dayKey = BuildKey($"{actionKey}_daily");
            var raw = await _js.GetRaw(UseLocal, dayKey);
            if (string.IsNullOrEmpty(raw)) return 0;

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var date = root.TryGetProperty("date", out var d) ? d.GetString() : null;
            if (date == DateTime.UtcNow.ToString("yyyy-MM-dd") &&
                root.TryGetProperty("count", out var c))
            {
                return c.GetInt32();
            }
            return 0;
        }

        public async Task ClearAsync()
        {
            await _js.RemoveAllWithPrefix(UseLocal, Pfx);
        }

        private string BuildCellKey(double lat, double lng)
        {
            // latitude-aware meters→degrees
            var metersPerDegLat = 111_320.0;
            var metersPerDegLng = 111_320.0 * Math.Max(0.1, Math.Cos(lat * Math.PI / 180.0));
            var cellLatDeg = O.CellSizeMeters / metersPerDegLat;
            var cellLngDeg = O.CellSizeMeters / metersPerDegLng;

            var qlat = Math.Round(lat / cellLatDeg) * cellLatDeg;
            var qlng = Math.Round(lng / cellLngDeg) * cellLngDeg;

            return BuildKey($"cell_{qlat:F5}_{qlng:F5}");
        }
    }
}
