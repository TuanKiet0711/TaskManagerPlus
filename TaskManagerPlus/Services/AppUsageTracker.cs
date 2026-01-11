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
        private Dictionary<string, ProcessTrackingInfo> trackedProcesses;
        private Timer trackingTimer;
        private bool isTracking;

        public AppUsageTracker(ProcessMonitor monitor)
        {
            database = new AppUsageDatabase();
            processMonitor = monitor;
            trackedProcesses = new Dictionary<string, ProcessTrackingInfo>();
        }

        public void StartTracking()
        {
            if (isTracking) return;

            isTracking = true;
            trackingTimer = new Timer(TrackingCallback, null, 0, 5000); // Track every 5 seconds
        }

        public void StopTracking()
        {
            isTracking = false;
            trackingTimer?.Dispose();
            
            // End all active sessions
            foreach (var proc in trackedProcesses.Values.Where(p => p.IsTracking))
            {
                database.EndAppSession(proc.ProcessName);
            }
            trackedProcesses.Clear();
        }

        private void TrackingCallback(object state)
        {
            if (!isTracking) return;

            try
            {
                var currentProcesses = processMonitor.GetAllProcesses();
                var currentProcessNames = new HashSet<string>();

                foreach (var process in currentProcesses)
                {
                    string processName = process.ProcessName;
                    currentProcessNames.Add(processName);

                    if (!trackedProcesses.ContainsKey(processName))
                    {
                        // New process detected
                        trackedProcesses[processName] = new ProcessTrackingInfo
                        {
                            ProcessName = processName,
                            ExecutablePath = process.FilePath,
                            IsTracking = true,
                            LastSeen = DateTime.Now
                        };

                        database.StartAppSession(processName, process.FilePath);
                    }
                    else
                    {
                        trackedProcesses[processName].LastSeen = DateTime.Now;
                    }

                    // Record stats
                    database.RecordAppStats(
                        processName,
                        process.CpuUsage,
                        process.MemoryBytes,
                        process.DiskUsage,
                        process.NetworkUsage
                    );
                }

                // End sessions for processes that are no longer running
                var endedProcesses = trackedProcesses.Keys
                    .Where(name => !currentProcessNames.Contains(name))
                    .ToList();

                foreach (var processName in endedProcesses)
                {
                    if (trackedProcesses[processName].IsTracking)
                    {
                        database.EndAppSession(processName);
                        trackedProcesses[processName].IsTracking = false;
                    }
                }

                // Clean up old entries
                var toRemove = trackedProcesses
                    .Where(kvp => !kvp.Value.IsTracking && (DateTime.Now - kvp.Value.LastSeen).TotalMinutes > 5)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in toRemove)
                {
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
            public string ProcessName { get; set; }
            public string ExecutablePath { get; set; }
            public bool IsTracking { get; set; }
            public DateTime LastSeen { get; set; }
        }
    }
}
