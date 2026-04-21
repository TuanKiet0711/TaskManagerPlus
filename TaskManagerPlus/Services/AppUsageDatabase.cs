using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TaskManagerPlus.Services
{
    public class AppSessionData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AppName { get; set; }
        public string ExePath { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double CpuSum { get; set; }
        public long RamSum { get; set; }
        public int StatCount { get; set; }
        public DateTime? LastSeen { get; set; }
    }

    public class AppUsageDatabase
    {
        private readonly string dataFilePath;
        private List<AppSessionData> sessions;
        private readonly Dictionary<int, AppSessionData> activeSessions;

        public AppUsageDatabase()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDir = Path.Combine(appData, "TaskManagerPlus");
            if (!Directory.Exists(appDir))
                Directory.CreateDirectory(appDir);
            
            dataFilePath = Path.Combine(appDir, "app_usage.json");
            activeSessions = new Dictionary<int, AppSessionData>();
            LoadData();
        }

        private void LoadData()
        {
            if (File.Exists(dataFilePath))
            {
                try
                {
                    string json = File.ReadAllText(dataFilePath);
                    sessions = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AppSessionData>>(json) ?? new List<AppSessionData>();
                }
                catch
                {
                    sessions = new List<AppSessionData>();
                }
            }
            else
            {
                sessions = new List<AppSessionData>();
            }
        }

        private void SaveData()
        {
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(sessions, Newtonsoft.Json.Formatting.None);
                File.WriteAllText(dataFilePath, json);
            }
            catch { }
        }

        public void StartAppSession(int processId, string processName, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            var session = new AppSessionData
            {
                AppName = processName,
                ExePath = executablePath,
                StartTime = DateTime.Now
            };

            sessions.Add(session);
            activeSessions[processId] = session;
            SaveData();
        }

        public void EndAppSession(int processId)
        {
            if (activeSessions.TryGetValue(processId, out var session))
            {
                session.EndTime = DateTime.Now;
                activeSessions.Remove(processId);
                SaveData();
            }
        }

        public void RecordAppStats(int processId, string processName, double cpuUsage, long memoryUsage, double diskUsage, double networkUsage)
        {
            if (activeSessions.TryGetValue(processId, out var session))
            {
                // App tracked
            }
            else
            {
                StartAppSession(processId, processName, "");
                activeSessions.TryGetValue(processId, out session);
            }

            if (session != null)
            {
                session.CpuSum += cpuUsage;
                session.RamSum += memoryUsage;
                session.StatCount++;
                session.LastSeen = DateTime.Now;
            }
        }

        public List<AppSessionData> GetSessionsForToday()
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);
            return sessions.Where(s => 
                s.StartTime < tomorrow && (s.EndTime ?? DateTime.Now) >= today
            ).ToList();
        }

        public List<AppHistoryItem> GetAppHistory(DateTime? startDate = null, DateTime? endDate = null)
        {
            var items = new List<AppHistoryItem>();

            DateTime? start = startDate?.Date;
            DateTime? endExclusive = endDate?.Date.AddDays(1);

            var filtered = sessions.Where(s => 
                (!start.HasValue || (s.EndTime ?? DateTime.Now) >= start.Value) &&
                (!endExclusive.HasValue || s.StartTime < endExclusive.Value)
            ).ToList();

            var groups = filtered.GroupBy(s => s.AppName);

            foreach (var g in groups)
            {
                string appName = g.Key;
                int launchCount = g.Count();

                double totalDuration = 0;
                double totalCpuSum = 0;
                long totalRamSum = 0;
                int totalStatCount = 0;
                DateTime maxLastSeen = DateTime.MinValue;
                bool hasRunning = false;

                foreach (var s in g)
                {
                    DateTime sStart = s.StartTime;
                    DateTime sEnd = s.EndTime ?? DateTime.Now;

                    if (start.HasValue && sStart < start.Value) sStart = start.Value;
                    if (endExclusive.HasValue && sEnd > endExclusive.Value) sEnd = endExclusive.Value;

                    if (sEnd > sStart)
                    {
                        totalDuration += (sEnd - sStart).TotalSeconds;
                    }

                    totalCpuSum += s.CpuSum;
                    totalRamSum += s.RamSum;
                    totalStatCount += s.StatCount;

                    if (s.LastSeen.HasValue && s.LastSeen.Value > maxLastSeen)
                    {
                        maxLastSeen = s.LastSeen.Value;
                    }

                    // A session is running if EndTime is null OR if it was seen very recently
                    bool isSessionRunning = s.EndTime == null || (s.LastSeen.HasValue && (DateTime.Now - s.LastSeen.Value).TotalSeconds <= 15);
                    if (isSessionRunning)
                        hasRunning = true;
                }

                double avgCpu = totalStatCount > 0 ? totalCpuSum / totalStatCount : 0;
                long avgRam = totalStatCount > 0 ? totalRamSum / totalStatCount : 0;

                string exePath = g.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.ExePath))?.ExePath ?? "";

                items.Add(new AppHistoryItem
                {
                    ProcessName = appName,
                    ExePath = exePath,
                    TotalDuration = (int)totalDuration,
                    LaunchCount = launchCount,
                    IsRunning = hasRunning,
                    AverageCpu = avgCpu,
                    AverageMemory = avgRam
                });
            }

            return items.ToList();
        }

        public void UpdateDailySummary()
        {
        }

        /// <summary>
        /// Returns raw session records for a specific app (for detail view).
        /// </summary>
        public List<AppSessionData> GetSessionsForApp(string appName, DateTime? startDate = null, DateTime? endDate = null)
        {
            DateTime? start = startDate?.Date;
            DateTime? endExclusive = endDate?.Date.AddDays(1);

            return sessions
                .Where(s =>
                    s.AppName.Equals(appName, StringComparison.OrdinalIgnoreCase) &&
                    (!start.HasValue || (s.EndTime ?? DateTime.Now) >= start.Value) &&
                    (!endExclusive.HasValue || s.StartTime < endExclusive.Value))
                .OrderByDescending(s => s.StartTime)
                .ToList();
        }

        public void CleanOldData(int daysToKeep = 30)
        {
            if (daysToKeep == 0)
            {
                sessions.Clear();
            }
            else
            {
                DateTime cutoff = DateTime.Now.AddDays(-daysToKeep);
                sessions.RemoveAll(s => s.StartTime < cutoff);
            }
            SaveData();
        }

        public int DeleteTodaySessions()
        {
            DateTime today = DateTime.Today;
            int count = sessions.RemoveAll(s => s.StartTime.Date == today);
            SaveData();
            return count;
        }
    }

    public class AppHistoryItem
    {
        public System.Drawing.Bitmap Icon { get; set; }
        public string ProcessName { get; set; }
        public string ExePath { get; set; }
        public int TotalDuration { get; set; }
        public double AverageCpu { get; set; }
        public long AverageMemory { get; set; }
        public int LaunchCount { get; set; }
        public bool IsRunning { get; set; }

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
