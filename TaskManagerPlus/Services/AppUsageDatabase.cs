using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace TaskManagerPlus.Services
{
    public class AppUsageDatabase
    {
        private const string ConnectionName = "TaskManagerPlus";
        private readonly string connectionString;
        private readonly int userId;
        private readonly string userName;
        private readonly string computerName;
        private readonly Dictionary<string, DateTime> activeSessionsstartTimes;
        private readonly Dictionary<string, string> activeSessionsPaths;
        private readonly Dictionary<string, int> activeSessionIds;

        public AppUsageDatabase()
        {
            connectionString = GetConnectionString();
            userName = Environment.UserName ?? "unknown";
            computerName = Environment.MachineName ?? "unknown";
            activeSessionsstartTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            activeSessionsPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            activeSessionIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            userId = EnsureUser();
        }

        public void StartAppSession(string processName, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            int appId = EnsureApplication(processName, executablePath);
            DateTime startTime = DateTime.Now;

            int sessionId;
            using (SqlConnection conn = OpenConnection())
            using (SqlCommand cmd = new SqlCommand(
                "INSERT INTO sessions (user_id, app_id, start_time) VALUES (@userId, @appId, @startTime); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@appId", appId);
                cmd.Parameters.AddWithValue("@startTime", startTime);
                sessionId = (int)cmd.ExecuteScalar();
            }

            activeSessionsstartTimes[processName] = startTime;
            activeSessionsPaths[processName] = executablePath ?? "";
            activeSessionIds[processName] = sessionId;
        }

        public void EndAppSession(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;
            if (!activeSessionsstartTimes.ContainsKey(processName))
                return;

            DateTime startTime = activeSessionsstartTimes[processName];
            DateTime endTime = DateTime.Now;
            int duration = (int)(endTime - startTime).TotalSeconds;

            int sessionId = 0;
            if (activeSessionIds.TryGetValue(processName, out int storedSessionId))
                sessionId = storedSessionId;

            using (SqlConnection conn = OpenConnection())
            using (SqlCommand cmd = new SqlCommand(
                "UPDATE sessions SET end_time = @endTime, duration_seconds = @duration WHERE session_id = @sessionId;",
                conn))
            {
                if (sessionId == 0)
                    sessionId = FindOpenSessionId(conn, processName);

                if (sessionId != 0)
                {
                    cmd.Parameters.AddWithValue("@endTime", endTime);
                    cmd.Parameters.AddWithValue("@duration", duration);
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    cmd.ExecuteNonQuery();
                }
            }

            activeSessionsstartTimes.Remove(processName);
            activeSessionsPaths.Remove(processName);
            activeSessionIds.Remove(processName);
        }

        public void RecordAppStats(string processName, double cpuUsage, long memoryUsage, double diskUsage, double networkUsage)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            int sessionId = 0;
            if (activeSessionIds.TryGetValue(processName, out int storedSessionId))
                sessionId = storedSessionId;

            using (SqlConnection conn = OpenConnection())
            {
                if (sessionId == 0)
                    sessionId = FindOpenSessionId(conn, processName);

                if (sessionId == 0)
                    return;

                using (SqlCommand cmd = new SqlCommand(
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

            using (SqlConnection conn = OpenConnection())
            using (SqlCommand cmd = new SqlCommand(@"
SELECT 
    a.app_name,
    SUM(ISNULL(s.duration_seconds, 0)) AS total_duration,
    COUNT(s.session_id) AS launch_count,
    AVG(r.cpu_usage) AS avg_cpu,
    AVG(r.ram_usage) AS avg_ram
FROM sessions s
INNER JOIN applications a ON s.app_id = a.app_id
LEFT JOIN app_resource_usage r ON r.session_id = s.session_id
WHERE s.user_id = @userId
  AND (@start IS NULL OR s.start_time >= @start)
  AND (@end IS NULL OR s.start_time < @end)
GROUP BY a.app_name
ORDER BY total_duration DESC;", conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.Add("@start", SqlDbType.DateTime).Value = (object)start ?? DBNull.Value;
                cmd.Parameters.Add("@end", SqlDbType.DateTime).Value = (object)endExclusive ?? DBNull.Value;

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string appName = reader.GetString(0);
                        int totalDuration = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        int launchCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        double avgCpu = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                        long avgMemory = reader.IsDBNull(4) ? 0 : Convert.ToInt64(reader.GetDouble(4));

                        items.Add(new AppHistoryItem
                        {
                            ProcessName = appName,
                            TotalDuration = totalDuration,
                            LaunchCount = launchCount,
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
            using (SqlConnection conn = OpenConnection())
            using (SqlTransaction tx = conn.BeginTransaction())
            {
                try
                {
                    if (daysToKeep == 0)
                    {
                        using (SqlCommand cmd = new SqlCommand(
                            "DELETE FROM sessions WHERE user_id = @userId;", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        DateTime cutoff = DateTime.Now.AddDays(-daysToKeep);
                        using (SqlCommand cmd = new SqlCommand(
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

        private SqlConnection OpenConnection()
        {
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            return conn;
        }

        private int EnsureUser()
        {
            using (SqlConnection conn = OpenConnection())
            using (SqlCommand cmd = new SqlCommand(
                "SELECT TOP 1 user_id FROM users WHERE username = @username AND computer_name = @computerName;",
                conn))
            {
                cmd.Parameters.AddWithValue("@username", userName);
                cmd.Parameters.AddWithValue("@computerName", computerName);

                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
            }

            using (SqlConnection conn = OpenConnection())
            using (SqlCommand cmd = new SqlCommand(
                "INSERT INTO users (username, computer_name) VALUES (@username, @computerName); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                conn))
            {
                cmd.Parameters.AddWithValue("@username", userName);
                cmd.Parameters.AddWithValue("@computerName", computerName);
                return (int)cmd.ExecuteScalar();
            }
        }

        private int EnsureApplication(string processName, string executablePath)
        {
            string exeName = "";
            if (!string.IsNullOrWhiteSpace(executablePath))
                exeName = Path.GetFileName(executablePath);

            using (SqlConnection conn = OpenConnection())
            using (SqlCommand cmd = new SqlCommand(
                "SELECT TOP 1 app_id FROM applications WHERE app_name = @appName AND ISNULL(exe_name, '') = @exeName;",
                conn))
            {
                cmd.Parameters.AddWithValue("@appName", processName);
                cmd.Parameters.AddWithValue("@exeName", exeName ?? "");

                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
            }

            using (SqlConnection conn = OpenConnection())
            using (SqlCommand cmd = new SqlCommand(
                "INSERT INTO applications (app_name, exe_name) VALUES (@appName, @exeName); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                conn))
            {
                cmd.Parameters.AddWithValue("@appName", processName);
                cmd.Parameters.AddWithValue("@exeName", exeName ?? "");
                return (int)cmd.ExecuteScalar();
            }
        }

        private int FindOpenSessionId(SqlConnection conn, string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return 0;

            int appId = EnsureApplication(processName, activeSessionsPaths.ContainsKey(processName) ? activeSessionsPaths[processName] : "");

            using (SqlCommand cmd = new SqlCommand(
                @"SELECT TOP 1 session_id 
                  FROM sessions 
                  WHERE user_id = @userId AND app_id = @appId AND end_time IS NULL
                  ORDER BY start_time DESC;",
                conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@appId", appId);
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
