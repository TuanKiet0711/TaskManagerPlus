using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Services
{
    public class SummaryService
    {
        private readonly string _connectionString;

        public SummaryService()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaskManagerPlus"]?.ConnectionString
                ?? "Server=localhost;Database=app_usage_tracker;Uid=root;Pwd=;SslMode=None;AllowPublicKeyRetrieval=true;";
        }

        public async Task<DailySummaryData> GetDailySummaryAsync()
        {
            var summary = new DailySummaryData { IsLoading = true };

            return await Task.Run(() =>
            {
                try
                {
                    summary.SystemAwakeSeconds = GetSystemAwakeSeconds();

                    var userId = 0;
                    try
                    {
                        userId = GetCurrentUserId();
                    }
                    catch
                    {
                        userId = 0;
                    }

                    if (userId > 0)
                    {
                        var startOfDay = DateTime.Today;
                        var endExclusive = startOfDay.AddDays(1);
                        var now = DateTime.Now;
                        var currentAppName = Process.GetCurrentProcess().ProcessName;

                        using (var conn = new MySqlConnection(_connectionString))
                        {
                            conn.Open();

                            var slices = LoadTodaySessionSlices(conn, userId, startOfDay, endExclusive, now, currentAppName);
                            BuildUsageSummary(summary, slices);
                            LoadPeakHour(conn, summary, userId, startOfDay, endExclusive);
                            LoadAverageResources(conn, summary, userId, startOfDay, endExclusive);
                        }
                    }

                    GenerateInsights(summary);
                    summary.IsEmpty = summary.SystemAwakeSeconds <= 0
                        && summary.TotalUsageSeconds <= 0
                        && summary.TopApps.Count == 0;
                }
                catch
                {
                    summary.IsEmpty = summary.SystemAwakeSeconds <= 0
                        && summary.TotalUsageSeconds <= 0
                        && summary.TopApps.Count == 0;
                }
                finally
                {
                    summary.IsLoading = false;
                }

                return summary;
            });
        }

        private static List<SessionSlice> LoadTodaySessionSlices(
            MySqlConnection conn,
            int userId,
            DateTime startOfDay,
            DateTime endExclusive,
            DateTime now,
            string currentAppName)
        {
            var result = new List<SessionSlice>();

            using (var cmd = new MySqlCommand(@"
SELECT a.app_name, s.start_time, IFNULL(s.end_time, @now) AS end_time
FROM sessions s
INNER JOIN applications a ON s.app_id = a.app_id
WHERE s.user_id = @userId
  AND s.start_time < @endExclusive
  AND IFNULL(s.end_time, @now) > @startOfDay
ORDER BY s.start_time ASC;", conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@startOfDay", startOfDay);
                cmd.Parameters.AddWithValue("@endExclusive", endExclusive);
                cmd.Parameters.AddWithValue("@now", now);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var appName = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
                        var start = reader.GetDateTime(1);
                        var end = reader.GetDateTime(2);

                        var clampedStart = start < startOfDay ? startOfDay : start;
                        var clampedEnd = end > endExclusive ? endExclusive : end;

                        if (clampedEnd <= clampedStart)
                            continue;

                        result.Add(new SessionSlice
                        {
                            AppName = appName,
                            Start = clampedStart,
                            End = clampedEnd,
                            IsSelfApp = !string.IsNullOrWhiteSpace(currentAppName)
                                && appName.Equals(currentAppName, StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }

            return result;
        }

        private static void BuildUsageSummary(DailySummaryData summary, List<SessionSlice> slices)
        {
            summary.TopApps.Clear();
            if (slices == null || slices.Count == 0)
            {
                summary.TotalUsageSeconds = 0;
                summary.MostUsedApp = null;
                summary.MostUsedAppSeconds = 0;
                return;
            }

            var events = new List<UsageEvent>(slices.Count * 2);
            foreach (var slice in slices)
            {
                events.Add(new UsageEvent(slice.Start, true, slice));
                events.Add(new UsageEvent(slice.End, false, slice));
            }

            events.Sort((a, b) =>
            {
                var timeCmp = a.Time.CompareTo(b.Time);
                if (timeCmp != 0)
                    return timeCmp;

                // End events first, then starts at the same timestamp.
                if (a.IsStart == b.IsStart)
                    return 0;
                return a.IsStart ? 1 : -1;
            });

            var active = new List<SessionSlice>();
            var appTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            DateTime? lastTime = null;
            long totalSeconds = 0;

            foreach (var ev in events)
            {
                if (lastTime.HasValue && ev.Time > lastTime.Value && active.Count > 0)
                {
                    var delta = (long)(ev.Time - lastTime.Value).TotalSeconds;
                    if (delta > 0)
                    {
                        totalSeconds += delta;

                        if (active.Count == 1)
                        {
                            var only = active[0];
                            if (!only.IsSelfApp)
                            {
                                double current;
                                if (!appTotals.TryGetValue(only.AppName, out current))
                                    current = 0;
                                appTotals[only.AppName] = current + delta;
                            }
                        }
                        else
                        {
                            var share = (double)delta / active.Count;
                            foreach (var slice in active)
                            {
                                if (slice.IsSelfApp)
                                    continue;

                                double current;
                                if (!appTotals.TryGetValue(slice.AppName, out current))
                                    current = 0;
                                appTotals[slice.AppName] = current + share;
                            }
                        }
                    }
                }

                if (ev.IsStart)
                {
                    active.Add(ev.Slice);
                }
                else
                {
                    active.Remove(ev.Slice);
                }

                lastTime = ev.Time;
            }

            var awakeCapSeconds = Math.Max(0, summary.SystemAwakeSeconds);
            if (awakeCapSeconds > 0 && totalSeconds > awakeCapSeconds)
                totalSeconds = awakeCapSeconds;

            summary.TotalUsageSeconds = Math.Min(Math.Max(0, totalSeconds), 24L * 3600L);

            var appTotalsOrdered = appTotals
                .OrderByDescending(x => x.Value)
                .ToList();

            if (appTotalsOrdered.Count > 0)
            {
                var top = appTotalsOrdered[0];
                summary.MostUsedApp = top.Key;
                summary.MostUsedAppSeconds = (long)Math.Round(top.Value, MidpointRounding.AwayFromZero);
            }

            foreach (var item in appTotalsOrdered.Take(5))
            {
                summary.TopApps.Add(new AppTimeUsage
                {
                    AppName = item.Key,
                    DurationSeconds = (long)Math.Round(item.Value, MidpointRounding.AwayFromZero)
                });
            }

            var total = Math.Max(1.0, appTotalsOrdered.Sum(x => x.Value));
            foreach (var app in summary.TopApps)
                app.PercentageOfTotal = Math.Max(0, (double)app.DurationSeconds / total * 100.0);
        }

        private static SessionSlice ChoosePrimarySlice(List<SessionSlice> active)
        {
            if (active == null || active.Count == 0)
                return null;

            return active
                .OrderByDescending(x => x.Start)
                .ThenByDescending(x => x.End)
                .FirstOrDefault();
        }

        private static void LoadPeakHour(
            MySqlConnection conn,
            DailySummaryData summary,
            int userId,
            DateTime startOfDay,
            DateTime endExclusive)
        {
            using (var cmd = new MySqlCommand(@"
SELECT HOUR(s.start_time) AS peak_hour, COUNT(*) AS session_count
FROM sessions s
WHERE s.user_id = @userId
  AND s.start_time >= @startOfDay
  AND s.start_time < @endExclusive
GROUP BY HOUR(s.start_time)
ORDER BY session_count DESC, peak_hour ASC
LIMIT 1;", conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@startOfDay", startOfDay);
                cmd.Parameters.AddWithValue("@endExclusive", endExclusive);

                using (var reader = cmd.ExecuteReader())
                {
                    summary.PeakHour = reader.Read() ? reader.GetInt32(0) : -1;
                }
            }
        }

        private static void LoadAverageResources(
            MySqlConnection conn,
            DailySummaryData summary,
            int userId,
            DateTime startOfDay,
            DateTime endExclusive)
        {
            using (var cmd = new MySqlCommand(@"
SELECT
    COALESCE(AVG(r.cpu_usage), 0.0) AS avg_cpu,
    COALESCE(AVG(r.ram_usage), 0.0) AS avg_ram_bytes
FROM app_resource_usage r
INNER JOIN sessions s ON s.session_id = r.session_id
WHERE s.user_id = @userId
  AND r.recorded_at >= @startOfDay
  AND r.recorded_at < @endExclusive
  AND s.start_time < @endExclusive
  AND IFNULL(s.end_time, @endExclusive) > @startOfDay;", conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@startOfDay", startOfDay);
                cmd.Parameters.AddWithValue("@endExclusive", endExclusive);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        summary.AverageCpu = ClampPercent(reader.IsDBNull(0) ? 0 : reader.GetDouble(0));

                        var avgRamBytes = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1));
                        var totalRamBytes = GetTotalPhysicalMemoryBytes();
                        summary.AverageRam = totalRamBytes > 0
                            ? ClampPercent((double)avgRamBytes / totalRamBytes * 100.0)
                            : 0;
                    }
                }
            }
        }

        private static void GenerateInsights(DailySummaryData summary)
        {
            summary.Insights.Clear();

            if (summary.TotalUsageSeconds <= 0)
            {
                if (summary.Insights.Count == 0)
                    summary.Insights.Add("Không có dữ liệu sử dụng hôm nay.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(summary.MostUsedApp))
            {
                summary.Insights.Add(string.Format("Ứng dụng dùng nhiều nhất: {0}", summary.MostUsedApp));
            }

            if (summary.PeakHour >= 0)
            {
                summary.Insights.Add(string.Format("Khung giờ hoạt động mạnh nhất: {0:00}:00 - {1:00}:00", summary.PeakHour, summary.PeakHour + 1));
            }

            var hours = summary.TotalUsageSeconds / 3600;
            var minutes = (summary.TotalUsageSeconds % 3600) / 60;
            summary.Insights.Add(string.Format("Tổng thời gian dùng ứng dụng: {0}h {1}m", hours, minutes));

            if (summary.AverageCpu > 0 || summary.AverageRam > 0)
            {
                summary.Insights.Add(string.Format("CPU trung bình: {0:F1}% | RAM trung bình: {1:F1}%", summary.AverageCpu, summary.AverageRam));
            }
        }

        private int GetCurrentUserId()
        {
            var userName = Environment.UserName ?? "unknown";
            var computerName = Environment.MachineName ?? "unknown";

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                using (var cmd = new MySqlCommand(@"
SELECT user_id
FROM users
WHERE username = @username
  AND computer_name = @computerName
LIMIT 1;", conn))
                {
                    cmd.Parameters.AddWithValue("@username", userName);
                    cmd.Parameters.AddWithValue("@computerName", computerName);

                    object result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                        return 0;

                    return Convert.ToInt32(result);
                }
            }
        }

        private static double ClampPercent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;
            if (value < 0)
                return 0;
            if (value > 100)
                return 100;
            return value;
        }

        private static long GetSystemAwakeSeconds()
        {
            try
            {
                ulong unbiasedInterruptTime;
                if (!QueryUnbiasedInterruptTime(out unbiasedInterruptTime))
                    return 0;

                return (long)(unbiasedInterruptTime / 10000000UL);
            }
            catch
            {
                return 0;
            }
        }

        private static string FormatDuration(long seconds)
        {
            var safeSeconds = Math.Max(0, seconds);
            var hours = safeSeconds / 3600;
            var minutes = (safeSeconds % 3600) / 60;

            if (hours > 0)
                return string.Format("{0}h {1}m", hours, minutes);

            return string.Format("{0}m", minutes);
        }

        private static long GetTotalPhysicalMemoryBytes()
        {
            try
            {
                long totalBytes = 0;
                var query = new ObjectQuery("SELECT Capacity FROM Win32_PhysicalMemory");
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object cap = obj["Capacity"];
                        if (cap != null)
                            totalBytes += Convert.ToInt64(cap);
                    }
                }
                return totalBytes;
            }
            catch
            {
                return 0;
            }
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryUnbiasedInterruptTime(out ulong unbiasedInterruptTime);

        private sealed class SessionSlice
        {
            public string AppName { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public bool IsSelfApp { get; set; }
        }

        private sealed class UsageEvent
        {
            public UsageEvent(DateTime time, bool isStart, SessionSlice slice)
            {
                Time = time;
                IsStart = isStart;
                Slice = slice;
            }

            public DateTime Time { get; private set; }
            public bool IsStart { get; private set; }
            public SessionSlice Slice { get; private set; }
        }
    }
}
