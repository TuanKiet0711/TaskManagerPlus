using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Services
{
    public class SummaryService
    {
        private readonly AppUsageDatabase _database;

        public SummaryService()
        {
            _database = new AppUsageDatabase();
        }

        public async Task<DailySummaryData> GetDailySummaryAsync()
        {
            var summary = new DailySummaryData { IsLoading = true };

            return await Task.Run(() =>
            {
                try
                {
                    summary.SystemAwakeSeconds = GetSystemAwakeSeconds();

                    var startOfDay = DateTime.Today;
                    var endExclusive = startOfDay.AddDays(1);
                    var now = DateTime.Now;
                    var currentAppName = Process.GetCurrentProcess().ProcessName;

                    // Get sessions from today
                    var todayHistory = _database.GetAppHistory(startOfDay, startOfDay);
                    var allTodaySessions = _database.GetSessionsForToday();

                    if (allTodaySessions.Any())
                    {
                        var slices = LoadTodaySessionSlices(allTodaySessions, startOfDay, endExclusive, now, currentAppName);
                        BuildUsageSummary(summary, slices);
                        LoadPeakHour(allTodaySessions, summary);
                        LoadAverageResources(allTodaySessions, summary);
                    }

                    GenerateInsights(summary);
                    summary.IsEmpty = summary.SystemAwakeSeconds <= 0
                        && summary.TotalUsageSeconds <= 0
                        && summary.TopApps.Count == 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in GetDailySummaryAsync: {ex.Message}");
                    summary.IsEmpty = true;
                }
                finally
                {
                    summary.IsLoading = false;
                }

                return summary;
            });
        }

        private static List<SessionSlice> LoadTodaySessionSlices(
            List<AppSessionData> sessions,
            DateTime startOfDay,
            DateTime endExclusive,
            DateTime now,
            string currentAppName)
        {
            var result = new List<SessionSlice>();

            foreach (var s in sessions)
            {
                var start = s.StartTime;
                var end = s.EndTime ?? now;

                var clampedStart = start < startOfDay ? startOfDay : start;
                var clampedEnd = end > endExclusive ? endExclusive : end;

                if (clampedEnd <= clampedStart)
                    continue;

                result.Add(new SessionSlice
                {
                    AppName = s.AppName,
                    Start = clampedStart,
                    End = clampedEnd,
                    IsSelfApp = !string.IsNullOrWhiteSpace(currentAppName)
                        && s.AppName.Equals(currentAppName, StringComparison.OrdinalIgnoreCase)
                });
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
                    var delta = (ev.Time - lastTime.Value).TotalSeconds;
                    if (delta > 0)
                    {
                        totalSeconds += (long)delta;

                        if (active.Count == 1)
                        {
                            var only = active[0];
                            if (!only.IsSelfApp)
                            {
                                appTotals.TryGetValue(only.AppName, out double current);
                                appTotals[only.AppName] = current + delta;
                            }
                        }
                        else
                        {
                            var share = delta / active.Count;
                            foreach (var slice in active)
                            {
                                if (slice.IsSelfApp)
                                    continue;

                                appTotals.TryGetValue(slice.AppName, out double current);
                                appTotals[slice.AppName] = current + share;
                            }
                        }
                    }
                }

                if (ev.IsStart) active.Add(ev.Slice);
                else active.Remove(ev.Slice);

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

        private static void LoadPeakHour(List<AppSessionData> sessions, DailySummaryData summary)
        {
            var hourCounts = new int[24];
            foreach (var s in sessions)
            {
                int hour = s.StartTime.Hour;
                hourCounts[hour]++;
            }

            int maxHour = -1;
            int maxCount = 0;
            for (int i = 0; i < 24; i++)
            {
                if (hourCounts[i] > maxCount)
                {
                    maxCount = hourCounts[i];
                    maxHour = i;
                }
            }
            summary.PeakHour = maxHour;
        }

        private static void LoadAverageResources(List<AppSessionData> sessions, DailySummaryData summary)
        {
            double totalCpuSum = 0;
            long totalRamSum = 0;
            int totalStatCount = 0;

            foreach (var s in sessions)
            {
                totalCpuSum += s.CpuSum;
                totalRamSum += s.RamSum;
                totalStatCount += s.StatCount;
            }

            if (totalStatCount > 0)
            {
                summary.AverageCpu = ClampPercent(totalCpuSum / totalStatCount);
                
                long avgRamBytes = totalRamSum / totalStatCount;
                long totalRamBytes = GetTotalPhysicalMemoryBytes();
                summary.AverageRam = totalRamBytes > 0
                    ? ClampPercent((double)avgRamBytes / totalRamBytes * 100.0)
                    : 0;
            }
            else
            {
                summary.AverageCpu = 0;
                summary.AverageRam = 0;
            }
        }

        private static void GenerateInsights(DailySummaryData summary)
        {
            summary.Insights.Clear();

            if (summary.TotalUsageSeconds <= 0)
            {
                summary.Insights.Add(LocalizationService.T("overview_no_apps"));
                return;
            }

            if (!string.IsNullOrWhiteSpace(summary.MostUsedApp))
            {
                summary.Insights.Add(string.Format(LocalizationService.T("overview_insight_most_used"), summary.MostUsedApp));
            }

            if (summary.PeakHour >= 0)
            {
                summary.Insights.Add(string.Format(LocalizationService.T("overview_insight_peak"), summary.PeakHour, summary.PeakHour + 1));
            }

            var hours = summary.TotalUsageSeconds / 3600;
            var minutes = (summary.TotalUsageSeconds % 3600) / 60;
            summary.Insights.Add(string.Format(LocalizationService.T("overview_insight_total_time"), hours, minutes));

            if (summary.AverageCpu > 0 || summary.AverageRam > 0)
            {
                summary.Insights.Add(string.Format(LocalizationService.T("overview_insight_resources"), summary.AverageCpu, summary.AverageRam));
            }
        }

        private static double ClampPercent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            return Math.Max(0, Math.Min(100, value));
        }

        private static long GetSystemAwakeSeconds()
        {
            try
            {
                if (QueryUnbiasedInterruptTime(out ulong unbiasedInterruptTime))
                    return (long)(unbiasedInterruptTime / 10000000UL);
            }
            catch { }
            return 0;
        }

        private static long GetTotalPhysicalMemoryBytes()
        {
            try
            {
                long totalBytes = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["Capacity"] != null)
                            totalBytes += Convert.ToInt64(obj["Capacity"]);
                    }
                }
                return totalBytes;
            }
            catch { return 16L * 1024 * 1024 * 1024; } // Fallback 16GB
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
            public DateTime Time { get; }
            public bool IsStart { get; }
            public SessionSlice Slice { get; }
        }
    }
}
