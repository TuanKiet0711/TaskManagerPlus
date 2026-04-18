using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Services
{
    public sealed class LiveResourceMonitor : IDisposable
    {
        private sealed class CpuSample
        {
            public TimeSpan TotalProcessorTime;
            public DateTime TimestampUtc;
        }

        private readonly Dictionary<int, CpuSample> _cpuSamplesByPid = new Dictionary<int, CpuSample>();

        public IReadOnlyList<ProcessResourceUsage> Sample()
        {
            var nowUtc = DateTime.UtcNow;
            var cpuCount = Math.Max(1, Environment.ProcessorCount);
            var results = new List<ProcessResourceUsage>();

            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch
            {
                return Array.Empty<ProcessResourceUsage>();
            }

            var seenPids = new HashSet<int>();

            foreach (var p in processes)
            {
                int pid;
                string name;
                long workingSet;
                TimeSpan totalCpu;

                try
                {
                    pid = p.Id;
                    name = GetDisplayName(p);
                    workingSet = p.WorkingSet64;
                    totalCpu = p.TotalProcessorTime;
                }
                catch
                {
                    continue;
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }

                seenPids.Add(pid);

                var cpuPercent = 0.0;
                if (_cpuSamplesByPid.TryGetValue(pid, out var prev))
                {
                    var elapsedMs = (nowUtc - prev.TimestampUtc).TotalMilliseconds;
                    var cpuDeltaMs = (totalCpu - prev.TotalProcessorTime).TotalMilliseconds;
                    if (elapsedMs > 0 && cpuDeltaMs >= 0)
                        cpuPercent = (cpuDeltaMs / (elapsedMs * cpuCount)) * 100.0;

                    prev.TotalProcessorTime = totalCpu;
                    prev.TimestampUtc = nowUtc;
                }
                else
                {
                    _cpuSamplesByPid[pid] = new CpuSample { TotalProcessorTime = totalCpu, TimestampUtc = nowUtc };
                }

                if (double.IsNaN(cpuPercent) || double.IsInfinity(cpuPercent) || cpuPercent < 0)
                    cpuPercent = 0;

                results.Add(new ProcessResourceUsage
                {
                    ProcessId = pid,
                    DisplayName = name,
                    CpuPercent = Math.Min(100.0, cpuPercent),
                    WorkingSetBytes = workingSet
                });
            }

            // cleanup dead pids
            if (_cpuSamplesByPid.Count > 0)
            {
                var dead = _cpuSamplesByPid.Keys.Where(pid => !seenPids.Contains(pid)).ToList();
                foreach (var pid in dead)
                    _cpuSamplesByPid.Remove(pid);
            }

            return results;
        }

        private static string GetDisplayName(Process p)
        {
            var title = "";
            try { title = p.MainWindowTitle; } catch { }

            if (!string.IsNullOrWhiteSpace(title))
                return title;

            try { return p.ProcessName; } catch { return "Unknown"; }
        }

        public void Dispose()
        {
            _cpuSamplesByPid.Clear();
        }
    }
}

