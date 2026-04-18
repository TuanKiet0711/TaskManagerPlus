using System;
using System.Diagnostics;
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
        private int? currentForegroundProcessId;

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

            EndCurrentForegroundSession();
        }

        private void TrackingCallback(object state)
        {
            if (!isTracking) return;

            try
            {
                if (IsUserIdle())
                {
                    EndCurrentForegroundSession();
                    return;
                }

                var foreground = GetForegroundProcessInfo();
                if (foreground == null)
                {
                    EndCurrentForegroundSession();
                    return;
                }

                if (!currentForegroundProcessId.HasValue || currentForegroundProcessId.Value != foreground.ProcessId)
                {
                    EndCurrentForegroundSession();

                    currentForegroundProcessId = foreground.ProcessId;

                    database.StartAppSession(
                        foreground.ProcessId,
                        foreground.ProcessName,
                        foreground.ExecutablePath);
                }

                database.RecordAppStats(
                    foreground.ProcessId,
                    foreground.ProcessName,
                    foreground.CpuUsage,
                    foreground.MemoryBytes,
                    foreground.DiskUsage,
                    foreground.NetworkUsage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tracking error: {ex.Message}");
            }
        }

        private void EndCurrentForegroundSession()
        {
            if (!currentForegroundProcessId.HasValue)
                return;

            database.EndAppSession(currentForegroundProcessId.Value);
            currentForegroundProcessId = null;
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
