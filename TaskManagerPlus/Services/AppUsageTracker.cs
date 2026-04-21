using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace TaskManagerPlus.Services
{
    public class AppUsageTracker
    {
        // Ignore short pauses so the summary still reflects real usage, not every tiny break.
        private const ulong IdleThresholdMilliseconds = 5UL * 60UL * 1000UL;

        private AppUsageDatabase database;
        private Timer trackingTimer;
        private bool isTracking;
        
        private HashSet<int> trackedProcessIds = new HashSet<int>();

        public AppUsageTracker()
        {
            database = new AppUsageDatabase();
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

            EndAllSessions();
        }

        private void TrackingCallback(object state)
        {
            if (!isTracking) return;

            try
            {
                if (IsUserIdle())
                {
                    EndAllSessions();
                    return;
                }

                var processes = Process.GetProcesses()
                    .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                    .ToList();

                var currentProcesses = processes.ToDictionary(p => p.Id);

                var pidsToEnd = trackedProcessIds.Where(pid => !currentProcesses.ContainsKey(pid)).ToList();
                foreach (var pid in pidsToEnd)
                {
                    database.EndAppSession(pid);
                    trackedProcessIds.Remove(pid);
                }

                foreach (var kvp in currentProcesses)
                {
                    var proc = kvp.Value;
                    int pid = proc.Id;

                    if (!trackedProcessIds.Contains(pid))
                    {
                        trackedProcessIds.Add(pid);
                        string pName = proc.ProcessName ?? "Unknown";
                        string exe = string.Empty;
                        DateTime procStartTime = DateTime.Now;
                        try { exe = proc.MainModule?.FileName ?? string.Empty; } catch { }
                        try { procStartTime = proc.StartTime; } catch { }
                        database.StartAppSession(pid, pName, exe, procStartTime);
                    }

                    long mem = SafeGetLong(() => proc.WorkingSet64, 0L);
                    database.RecordAppStats(pid, proc.ProcessName, 0, mem, 0, 0);

                    proc.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tracking error: {ex.Message}");
            }
        }

        private void EndAllSessions()
        {
            foreach (var pid in trackedProcessIds)
            {
                database.EndAppSession(pid);
            }
            trackedProcessIds.Clear();
        }

        private ForegroundProcessInfo GetForegroundProcessInfo()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
                return null;

            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == 0)
                return null;

            try
            {
                using (var proc = Process.GetProcessById((int)pid))
                {
                    string processName = proc.ProcessName ?? "Unknown";
                    string executablePath = string.Empty;

                    try
                    {
                        executablePath = proc.MainModule != null ? proc.MainModule.FileName : string.Empty;
                    }
                    catch
                    {
                        executablePath = string.Empty;
                    }

                    return new ForegroundProcessInfo
                    {
                        ProcessId = (int)pid,
                        ProcessName = processName,
                        ExecutablePath = executablePath,
                        CpuUsage = 0,
                        MemoryBytes = SafeGetLong(() => proc.WorkingSet64, 0L),
                        DiskUsage = 0,
                        NetworkUsage = 0
                    };
                }
            }
            catch
            {
                return null;
            }
        }

        private static long SafeGetLong(Func<long> getter, long fallback)
        {
            try { return getter(); } catch { return fallback; }
        }

        private static bool IsUserIdle()
        {
            try
            {
                var lastInputInfo = new LASTINPUTINFO
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO))
                };

                if (!GetLastInputInfo(ref lastInputInfo))
                    return false;

                ulong tickCount = GetTickCount64();
                ulong lastInputTick = lastInputInfo.dwTime;
                ulong idleMilliseconds = tickCount >= lastInputTick ? tickCount - lastInputTick : 0;
                return idleMilliseconds >= IdleThresholdMilliseconds;
            }
            catch
            {
                return false;
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

        private sealed class ForegroundProcessInfo
        {
            public int ProcessId { get; set; }
            public string ProcessName { get; set; }
            public string ExecutablePath { get; set; }
            public double CpuUsage { get; set; }
            public long MemoryBytes { get; set; }
            public double DiskUsage { get; set; }
            public double NetworkUsage { get; set; }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }
    }
}
