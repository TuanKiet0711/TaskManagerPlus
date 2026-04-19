using System;
using System.Collections.Generic;

namespace TaskManagerPlus.Models
{
    /// <summary>
    /// Model chứa dữ liệu tổng quan hôm nay (đơn giản, không lặp lại Tab Lịch sử)
    /// </summary>
    public class DailySummaryData
    {
        // Stats tổng quát
        public long SystemAwakeSeconds { get; set; }
        public long TotalUsageSeconds { get; set; }
        public string MostUsedApp { get; set; }
        public long MostUsedAppSeconds { get; set; }
        public int PeakHour { get; set; }
        public double AverageCpu { get; set; }
        public double AverageRam { get; set; }

        // Top 5 apps theo thời gian
        public List<AppTimeUsage> TopApps { get; set; } = new List<AppTimeUsage>();

        // Insights
        public List<string> Insights { get; set; } = new List<string>();

        // Trạng thái
        public bool IsLoading { get; set; }
        public bool IsEmpty { get; set; }
    }

    /// <summary>
    /// Model cho mỗi app trong top list
    /// </summary>
    public class AppTimeUsage
    {
        public string AppName { get; set; }
        public long DurationSeconds { get; set; }
        public double PercentageOfTotal { get; set; }

        public string GetFormattedDuration()
        {
            var hours = DurationSeconds / 3600;
            var minutes = (DurationSeconds % 3600) / 60;
            
            if (hours > 0)
                return $"{hours}h {minutes}m";
            return $"{minutes}m";
        }
    }
}
