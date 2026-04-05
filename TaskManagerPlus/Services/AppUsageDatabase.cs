using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using MySql.Data.MySqlClient;

namespace TaskManagerPlus.Services
{
    public class AppUsageDatabase
    {
        private const string ConnectionName = "TaskManagerPlus";
        private readonly string connectionString;
        private readonly int userId;
        private readonly string userName;
        private readonly string computerName;
        private readonly Dictionary<int, DateTime> activeSessionsstartTimes;
        private readonly Dictionary<int, string> activeSessionsPaths;
        private readonly Dictionary<int, int> activeSessionIds;

        public AppUsageDatabase()
        {
            connectionString = GetConnectionString();
            userName = Environment.UserName ?? "unknown";
            computerName = Environment.MachineName ?? "unknown";
            activeSessionsstartTimes = new Dictionary<int, DateTime>();
            activeSessionsPaths = new Dictionary<int, string>();
            activeSessionIds = new Dictionary<int, int>();

            userId = EnsureUser();
        }

        public void StartAppSession(int processId, string processName, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            int appId = EnsureApplication(processName, executablePath);
            DateTime startTime = DateTime.Now;

            int sessionId;
            using (MySqlConnection conn = OpenConnection())
            {
                using (MySqlCommand cmd = new MySqlCommand(
                    "INSERT INTO sessions (user_id, app_id, process_id, start_time) VALUES (@userId, @appId, @processId, @startTime);",
                    conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@appId", appId);
                    cmd.Parameters.AddWithValue("@processId", processId);
                    cmd.Parameters.AddWithValue("@startTime", startTime);
                    cmd.ExecuteNonQuery();
                }

                using (MySqlCommand cmd = new MySqlCommand("SELECT LAST_INSERT_ID();", conn))
                {
                    sessionId = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }

            activeSessionsstartTimes[processId] = startTime;
            activeSessionsPaths[processId] = executablePath ?? "";
            activeSessionIds[processId] = sessionId;
        }

        public void EndAppSession(int processId)
        {
            if (!activeSessionsstartTimes.ContainsKey(processId))
                return;

            DateTime startTime = activeSessionsstartTimes[processId];
            DateTime endTime = DateTime.Now;
            int duration = (int)(endTime - startTime).TotalSeconds;

            int sessionId = 0;
            if (activeSessionIds.TryGetValue(processId, out int storedSessionId))
                sessionId = storedSessionId;

            using (MySqlConnection conn = OpenConnection())
            {
                if (sessionId == 0)
                    sessionId = FindOpenSessionId(conn, processId);

                if (sessionId != 0)
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE sessions SET end_time = @endTime, duration_seconds = @duration WHERE session_id = @sessionId;",
                        conn))
                    {
                        cmd.Parameters.AddWithValue("@endTime", endTime);
                        cmd.Parameters.AddWithValue("@duration", duration);
                        cmd.Parameters.AddWithValue("@sessionId", sessionId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            activeSessionsstartTimes.Remove(processId);
            activeSessionsPaths.Remove(processId);
            activeSessionIds.Remove(processId);
        }

        public void RecordAppStats(int processId, string processName, double cpuUsage, long memoryUsage, double diskUsage, double networkUsage)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            int sessionId = 0;
            if (activeSessionIds.TryGetValue(processId, out int storedSessionId))
                sessionId = storedSessionId;

            using (MySqlConnection conn = OpenConnection())
            {
                if (sessionId == 0)
                    sessionId = FindOpenSessionId(conn, processId);

                if (sessionId == 0)
                    return;

                using (MySqlCommand cmd = new MySqlCommand(
                    "INSERT INTO app_resource_usage (session_id, cpu_usage, ram_usage, gpu_usage, recorded_at) VALUES (@sessionId, @cpu, @ram, @gpu, @recordedAt);",
                    conn))
                {
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    cmd.Parameters.AddWithValue("@cpu", cpuUsage);
                    cmd.Parameters.AddWithValue("@ram", memoryUsage);
                    cmd.Parameters.AddWithValue("@gpu", DBNull.Value);
                    cmd.Parameters.AddWithValue("@recordedAt", DateTime.Now);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<AppHistoryItem> GetAppHistory(DateTime? startDate = null, DateTime? endDate = null)
        {
            var items = new List<AppHistoryItem>();

            DateTime? start = startDate?.Date;
            DateTime? endExclusive = endDate?.Date.AddDays(1);

            using (MySqlConnection conn = OpenConnection())
            using (MySqlCommand cmd = new MySqlCommand(@"
SELECT 
    a.app_name,
    SUM(sx.session_duration) AS total_duration,
    COUNT(DISTINCT sx.session_id) AS launch_count,
    MAX(CASE WHEN sx.end_time IS NULL THEN 1 ELSE 0 END) AS has_running,
    AVG(rx.avg_cpu) AS avg_cpu,
    AVG(rx.avg_ram) AS avg_ram,
    MAX(rx.last_seen) AS last_seen
FROM (
    SELECT 
        s.session_id,
        s.app_id,
        s.end_time,
        CASE
            WHEN (@start IS NULL AND @end IS NULL) THEN
                CASE 
                    WHEN s.end_time IS NULL THEN TIMESTAMPDIFF(SECOND, s.start_time, NOW())
                    ELSE IFNULL(s.duration_seconds, 0)
                END
            ELSE
                CASE
                    WHEN (@start IS NULL OR IFNULL(s.end_time, NOW()) >= @start)
                         AND (@end IS NULL OR s.start_time < @end)
                    THEN
                        GREATEST(
                            0,
                            TIMESTAMPDIFF(
                                SECOND,
                                GREATEST(s.start_time, IFNULL(@start, s.start_time)),
                                LEAST(IFNULL(s.end_time, NOW()), IFNULL(@end, IFNULL(s.end_time, NOW())))
                            )
                        )
                    ELSE 0
                END
        END AS session_duration
    FROM sessions s
    WHERE s.user_id = @userId
      AND (@start IS NULL OR IFNULL(s.end_time, NOW()) >= @start)
      AND (@end IS NULL OR s.start_time < @end)
) sx
INNER JOIN applications a ON sx.app_id = a.app_id
LEFT JOIN (
    SELECT 
        r.session_id,
        AVG(r.cpu_usage) AS avg_cpu,
        AVG(r.ram_usage) AS avg_ram,
        MAX(r.recorded_at) AS last_seen
    FROM app_resource_usage r
    WHERE (@start IS NULL OR r.recorded_at >= @start)
      AND (@end IS NULL OR r.recorded_at < @end)
    GROUP BY r.session_id
) rx ON rx.session_id = sx.session_id
GROUP BY a.app_name
ORDER BY total_duration DESC;", conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.Add("@start", MySqlDbType.DateTime).Value = (object)start ?? DBNull.Value;
                cmd.Parameters.Add("@end", MySqlDbType.DateTime).Value = (object)endExclusive ?? DBNull.Value;

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string appName = reader.GetString(0);
                        int totalDuration = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        int launchCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        bool hasRunning = !reader.IsDBNull(3) && Convert.ToInt32(reader.GetValue(3)) > 0;
                        double avgCpu = reader.IsDBNull(4) ? 0 : Convert.ToDouble(reader.GetValue(4));
                        long avgMemory = reader.IsDBNull(5) ? 0 : Convert.ToInt64(Convert.ToDouble(reader.GetValue(5)));
                        DateTime? lastSeen = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
                        bool isRunning = hasRunning;
                        if (lastSeen.HasValue)
                        {
                            isRunning = (DateTime.Now - lastSeen.Value) <= TimeSpan.FromSeconds(6);
                        }

                        items.Add(new AppHistoryItem
                        {
                            ProcessName = appName,
                            TotalDuration = totalDuration,
                            LaunchCount = launchCount,
                            IsRunning = isRunning,
                            AverageCpu = avgCpu,
                            AverageMemory = avgMemory
                        });
                    }
                }
            }

            return items;
        }

        public void UpdateDailySummary()
        {
            // Not implemented for SQL version
        }

        public void CleanOldData(int daysToKeep = 30)
        {
            using (MySqlConnection conn = OpenConnection())
            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                try
                {
                    if (daysToKeep == 0)
                    {
                        using (MySqlCommand cmd = new MySqlCommand(
                            "DELETE FROM sessions WHERE user_id = @userId;", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        DateTime cutoff = DateTime.Now.AddDays(-daysToKeep);
                        using (MySqlCommand cmd = new MySqlCommand(
                            "DELETE FROM sessions WHERE user_id = @userId AND start_time < @cutoff;",
                            conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@cutoff", cutoff);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        private string GetConnectionString()
        {
            string cs = ConfigurationManager.ConnectionStrings[ConnectionName]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("Missing connection string 'TaskManagerPlus' in App.config.");
            return cs;
        }

        private MySqlConnection OpenConnection()
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            return conn;
        }

        private int EnsureUser()
        {
            using (MySqlConnection conn = OpenConnection())
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT user_id FROM users WHERE username = @username AND computer_name = @computerName LIMIT 1;",
                conn))
            {
                cmd.Parameters.AddWithValue("@username", userName);
                cmd.Parameters.AddWithValue("@computerName", computerName);

                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
            }

            using (MySqlConnection conn = OpenConnection())
            {
                using (MySqlCommand cmd = new MySqlCommand(
                    "INSERT INTO users (username, computer_name) VALUES (@username, @computerName);",
                    conn))
                {
                    cmd.Parameters.AddWithValue("@username", userName);
                    cmd.Parameters.AddWithValue("@computerName", computerName);
                    cmd.ExecuteNonQuery();
                }

                using (MySqlCommand cmd = new MySqlCommand("SELECT LAST_INSERT_ID();", conn))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        private int EnsureApplication(string processName, string executablePath)
        {
            string exeName = "";
            if (!string.IsNullOrWhiteSpace(executablePath))
                exeName = Path.GetFileName(executablePath);

            using (MySqlConnection conn = OpenConnection())
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT app_id FROM applications WHERE app_name = @appName AND IFNULL(exe_name, '') = @exeName LIMIT 1;",
                conn))
            {
                cmd.Parameters.AddWithValue("@appName", processName);
                cmd.Parameters.AddWithValue("@exeName", exeName ?? "");

                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
            }

            using (MySqlConnection conn = OpenConnection())
            {
                using (MySqlCommand cmd = new MySqlCommand(
                    "INSERT INTO applications (app_name, exe_name) VALUES (@appName, @exeName);",
                    conn))
                {
                    cmd.Parameters.AddWithValue("@appName", processName);
                    cmd.Parameters.AddWithValue("@exeName", exeName ?? "");
                    cmd.ExecuteNonQuery();
                }

                using (MySqlCommand cmd = new MySqlCommand("SELECT LAST_INSERT_ID();", conn))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        private int FindOpenSessionId(MySqlConnection conn, int processId)
        {
            using (MySqlCommand cmd = new MySqlCommand(
                @"SELECT session_id 
                  FROM sessions 
                  WHERE user_id = @userId AND process_id = @processId AND end_time IS NULL
                  ORDER BY start_time DESC
                  LIMIT 1;",
                conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@processId", processId);
                object result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return 0;
                return Convert.ToInt32(result);
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
