using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Cloudless.Diagnostics
{
    internal class LeakInfo
    {
        public WeakReference<object> WeakRef { get; set; }
        public string Tag { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public DateTime? ClosedAt { get; set; }
    }

    public static class LeakTracker
    {
        private static readonly ConcurrentDictionary<int, LeakInfo> _tracked = new();

        public static void Register(object obj, string tag)
        {
            if (obj == null) return;
            int id = RuntimeHelpers.GetHashCode(obj);
            var info = new LeakInfo
            {
                WeakRef = new WeakReference<object>(obj),
                Tag = tag,
                Created = DateTime.UtcNow,
                ClosedAt = null
            };
            _tracked[id] = info;
            Debug.WriteLine($"LeakTracker: Registered id={id}, tag={tag}");
        }

        public static void MarkClosed(object obj)
        {
            if (obj == null) return;
            int id = RuntimeHelpers.GetHashCode(obj);
            if (_tracked.TryGetValue(id, out var info))
            {
                info.ClosedAt = DateTime.UtcNow;
                Debug.WriteLine($"LeakTracker: Marked closed id={id}, tag={info.Tag}");
            }
        }

        public static string GenerateReport(bool forceGc = true)
        {
            if (forceGc)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"LeakTracker Report: {DateTime.UtcNow:O}");
            sb.AppendLine($"Tracked count: {_tracked.Count}");
            sb.AppendLine("ID | Alive | Tag | CreatedUtc | ClosedUtc | AgeSeconds");
            foreach (var kv in _tracked)
            {
                int id = kv.Key;
                var info = kv.Value;
                bool alive = info.WeakRef.TryGetTarget(out var target) && target != null;
                var created = info.Created;
                var closed = info.ClosedAt?.ToString("O") ?? "(null)";
                double age = (DateTime.UtcNow - created).TotalSeconds;
                sb.AppendLine($"{id} | {alive} | {info.Tag} | {created:O} | {closed} | {age:F1}");
            }
            return sb.ToString();
        }

        public static string WriteReportToTempFile(string prefix = "CloudlessLeakReport")
        {
            string report = GenerateReport();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.txt");
            try
            {
                System.IO.File.WriteAllText(path, report);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LeakTracker: Failed to write report to temp file: {ex.Message}");
            }
            return path;
        }

        // helper to remove entries that are dead and closed long ago
        public static void PruneCollected(TimeSpan minClosedAge)
        {
            foreach (var kv in _tracked)
            {
                var id = kv.Key;
                var info = kv.Value;
                bool alive = info.WeakRef.TryGetTarget(out var target) && target != null;
                if (!alive && info.ClosedAt.HasValue && (DateTime.UtcNow - info.ClosedAt.Value) > minClosedAge)
                {
                    _tracked.TryRemove(id, out _);
                }
            }
        }
    }
}