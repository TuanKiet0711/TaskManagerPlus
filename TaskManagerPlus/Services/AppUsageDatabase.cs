using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TaskManagerPlus.Services
{
    public class AppUsageDatabase
    {
        private string dataPath;
        private string sessionsFile;
        private string statsFile;
        private Dictionary<string, DateTime> activeSessionsstartTimes;
        private Dictionary<string, string> activeSessionsPaths;

        public AppUsageDatabase()
        {
            dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TaskManagerPlus");
            
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            sessionsFile = Path.Combine(dataPath, "sessions.csv");
            statsFile = Path.Combine(dataPath, "stats.csv");
            activeSessionsstartTimes = new Dictionary<string, DateTime>();
            activeSessionsPaths = new Dictionary<string, string>();
            
            InitializeFiles();
        }

        private void InitializeFiles()
        {
            if (!File.Exists(sessionsFile))
            {
                File.WriteAllText(sessionsFile, "ProcessName,ExecutablePath,StartTime,EndTime,Duration\n");
            }
            if (!File.Exists(statsFile))
            {
                File.WriteAllText(statsFile, "ProcessName,RecordTime,CpuUsage,MemoryUsage,DiskUsage,NetworkUsage\n");
            }
        }

        public void StartAppSession(string processName, string executablePath)
        {
            activeSessionsstartTimes[processName] = DateTime.Now;
            activeSessionsPaths[processName] = executablePath ?? "";
        }

        public void EndAppSession(string processName)
        {
            if (!activeSessionsstartTimes.ContainsKey(processName))
                return;

            DateTime startTime = activeSessionsstartTimes[processName];
            string executablePath = activeSessionsPaths.ContainsKey(processName) ? activeSessionsPaths[processName] : "";
            DateTime endTime = DateTime.Now;
            int duration = (int)(endTime - startTime).TotalSeconds;

            string line = $"{processName},{executablePath},{startTime:yyyy-MM-dd HH:mm:ss},{endTime:yyyy-MM-dd HH:mm:ss},{duration}\n";
            File.AppendAllText(sessionsFile, line);

            activeSessionsstartTimes.Remove(processName);
            activeSessionsPaths.Remove(processName);
        }

        public void RecordAppStats(string processName, double cpuUsage, long memoryUsage, double diskUsage, double networkUsage)
        {
            string line = $"{processName},{DateTime.Now:yyyy-MM-dd HH:mm:ss},{cpuUsage:F2},{memoryUsage},{diskUsage:F2},{networkUsage:F2}\n";
            File.AppendAllText(statsFile, line);
        }

        public List<AppHistoryItem> GetAppHistory(DateTime? startDate = null, DateTime? endDate = null)
        {
            var items = new Dictionary<string, AppHistoryItem>();

            if (!File.Exists(sessionsFile))
                return new List<AppHistoryItem>();

            var lines = File.ReadAllLines(sessionsFile).Skip(1); // Skip header

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                string processName = parts[0];
                if (!DateTime.TryParse(parts[2], out DateTime sessionStart))
                    continue;
                if (!int.TryParse(parts[4], out int duration))
                    continue;

                // Filter by date range
                if (startDate.HasValue && sessionStart.Date < startDate.Value.Date)
                    continue;
                if (endDate.HasValue && sessionStart.Date > endDate.Value.Date)
                    continue;

                if (!items.ContainsKey(processName))
                {
                    items[processName] = new AppHistoryItem
                    {
                        ProcessName = processName,
                        TotalDuration = 0,
                        LaunchCount = 0,
                        AverageCpu = 0,
                        AverageMemory = 0
                    };
                }

                items[processName].TotalDuration += duration;
                items[processName].LaunchCount++;
            }

            // Get stats
            if (File.Exists(statsFile))
            {
                var statsLines = File.ReadAllLines(statsFile).Skip(1);
                var statsByProcess = new Dictionary<string, List<(double cpu, long memory)>>();

                foreach (var line in statsLines)
                {
                    var parts = line.Split(',');
                    if (parts.Length < 4) continue;

                    string processName = parts[0];
                    if (!items.ContainsKey(processName)) continue;

                    if (!double.TryParse(parts[2], out double cpu))
                        continue;
                    if (!long.TryParse(parts[3], out long memory))
                        continue;

                    if (!statsByProcess.ContainsKey(processName))
                    {
                        statsByProcess[processName] = new List<(double, long)>();
                    }
                    statsByProcess[processName].Add((cpu, memory));
                }

                foreach (var kvp in statsByProcess)
                {
                    if (items.ContainsKey(kvp.Key))
                    {
                        items[kvp.Key].AverageCpu = kvp.Value.Average(s => s.cpu);
                        items[kvp.Key].AverageMemory = (long)kvp.Value.Average(s => s.memory);
                    }
                }
            }

            return items.Values.OrderByDescending(i => i.TotalDuration).ToList();
        }

        public void UpdateDailySummary()
        {
            // Not needed for CSV implementation
        }

        public void CleanOldData(int daysToKeep = 30)
        {
            if (daysToKeep == 0)
            {
                // Clear all data
                File.WriteAllText(sessionsFile, "ProcessName,ExecutablePath,StartTime,EndTime,Duration\n");
                File.WriteAllText(statsFile, "ProcessName,RecordTime,CpuUsage,MemoryUsage,DiskUsage,NetworkUsage\n");
                return;
            }

            DateTime cutoffDate = DateTime.Now.AddDays(-daysToKeep);

            // Clean sessions
            if (File.Exists(sessionsFile))
            {
                var lines = File.ReadAllLines(sessionsFile);
                var newLines = new List<string> { lines[0] }; // Keep header

                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length >= 3 && DateTime.TryParse(parts[2], out DateTime startTime))
                    {
                        if (startTime >= cutoffDate)
                        {
                            newLines.Add(lines[i]);
                        }
                    }
                }

                File.WriteAllLines(sessionsFile, newLines);
            }

            // Clean stats
            if (File.Exists(statsFile))
            {
                var lines = File.ReadAllLines(statsFile);
                var newLines = new List<string> { lines[0] }; // Keep header

                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length >= 2 && DateTime.TryParse(parts[1], out DateTime recordTime))
                    {
                        if (recordTime >= cutoffDate)
                        {
                            newLines.Add(lines[i]);
                        }
                    }
                }

                File.WriteAllLines(statsFile, newLines);
            }
        }
    }

    public class AppHistoryItem
    {
        public string ProcessName { get; set; }
        public int TotalDuration { get; set; }
        public double AverageCpu { get; set; }
        public long AverageMemory { get; set; }
        public int LaunchCount { get; set; }

        public string FormattedDuration
        {
            get
            {
                TimeSpan ts = TimeSpan.FromSeconds(TotalDuration);
                if (ts.TotalHours >= 1)
                    return $"{(int)ts.TotalHours}h {ts.Minutes}m";
                else
                    return $"{ts.Minutes}m {ts.Seconds}s";
            }
        }

        public string FormattedMemory
        {
            get
            {
                if (AverageMemory < 1024)
                    return $"{AverageMemory} B";
                else if (AverageMemory < 1024 * 1024)
                    return $"{AverageMemory / 1024.0:F1} KB";
                else if (AverageMemory < 1024 * 1024 * 1024)
                    return $"{AverageMemory / (1024.0 * 1024.0):F1} MB";
                else
                    return $"{AverageMemory / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }
    }
}
