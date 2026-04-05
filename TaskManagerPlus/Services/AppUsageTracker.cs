using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Services
{
    public class AppUsageTracker
    {
        private AppUsageDatabase database;
        private ProcessMonitor processMonitor;
        private Dictionary<int, ProcessTrackingInfo> trackedProcesses;
        private Timer trackingTimer;
        private bool isTracking;
        private static readonly TimeSpan EndSessionGrace = TimeSpan.FromSeconds(30);

        public AppUsageTracker(ProcessMonitor monitor)
        {
            database = new AppUsageDatabase();
            processMonitor = monitor;
            trackedProcesses = new Dictionary<int, ProcessTrackingInfo>();
        }

        public void StartTracking()
        {
            if (isTracking) return;

            isTracking = true;
            trackingTimer = new Timer(TrackingCallback, null, 0, 2000); // Track every 2 seconds
        }

        public void StopTracking()
        {
            isTracking = false;
            trackingTimer?.Dispose();
            
            // End all active sessions
            foreach (var proc in trackedProcesses.Values.Where(p => p.IsTracking))
            {
                database.EndAppSession(proc.ProcessId);
            }
            trackedProcesses.Clear();
        }

        private void TrackingCallback(object state)
        {
            if (!isTracking) return;

            try
            {
                var currentProcesses = processMonitor
                    .GetAllProcesses(true)
                    .Where(p => p.HasWindow)
                    .ToList();
                var currentProcessIds = new HashSet<int>();

                foreach (var process in currentProcesses)
                {
                    int pid = process.ProcessId;
                    string processName = process.ProcessName;
                    currentProcessIds.Add(pid);

                    if (!trackedProcesses.ContainsKey(pid))
                    {
                        // New process detected
                        trackedProcesses[pid] = new ProcessTrackingInfo
                        {
                            ProcessId = pid,
                            ProcessName = processName,
                            ExecutablePath = process.FilePath,
                            IsTracking = true,
                            LastSeen = DateTime.Now
                        };

                        database.StartAppSession(pid, processName, process.FilePath);
                    }
                    else
                    {
                        trackedProcesses[pid].LastSeen = DateTime.Now;
                    }

                    // Record stats
                    database.RecordAppStats(
                        pid,
                        processName,
                        process.CpuUsage,
                        process.MemoryBytes,
                        process.DiskUsage,
                        process.NetworkUsage
                    );
                }

                // End sessions for processes that are no longer running
                var endedProcesses = trackedProcesses.Keys
                    .Where(id => !currentProcessIds.Contains(id))
                    .ToList();

                foreach (var processId in endedProcesses)
                {
                    if (trackedProcesses[processId].IsTracking)
                    {
                        // Avoid ending sessions on transient misses (window detection can be flaky)
                        trackedProcesses[processId].IsTracking = false;
                    }
                }

                // Clean up old entries
                var toRemove = trackedProcesses
                    .Where(kvp => !kvp.Value.IsTracking && (DateTime.Now - kvp.Value.LastSeen) > EndSessionGrace)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in toRemove)
                {
                    database.EndAppSession(key);
                    trackedProcesses.Remove(key);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tracking error: {ex.Message}");
            }
        }

        public void UpdateDailySummary()
        {
            database.UpdateDailySummary();
        }

        public void CleanOldData(int daysToKeep = 30)
        {
            database.CleanOldData(daysToKeep);
        }

        private class ProcessTrackingInfo
        {
            public int ProcessId { get; set; }
            public string ProcessName { get; set; }
            public string ExecutablePath { get; set; }
            public bool IsTracking { get; set; }
            public DateTime LastSeen { get; set; }
        }
    }
}
