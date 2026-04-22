using System;
using System.Linq;

namespace TaskManagerPlus.Services
{
    public class HealthScoreService
    {
        private ProcessMonitor _processMonitor;
        private HardwareMonitor _hardwareMonitor;

        public HealthScoreService(ProcessMonitor processMonitor, HardwareMonitor hardwareMonitor)
        {
            _processMonitor = processMonitor;
            _hardwareMonitor = hardwareMonitor;
        }

        /// <summary>
        /// Returns a score from 0 (Critical) to 100 (Optimal).
        /// </summary>
        public int CalculateHealthScore()
        {
            int score = 100;
            
            // 1. CPU Penalty
            double totalCpu = _processMonitor.GetTotalCpuUsage();
            if (totalCpu > 80) score -= 30;
            else if (totalCpu > 60) score -= 15;
            else if (totalCpu > 40) score -= 5;
            
            // 2. RAM Penalty
            double totalRamBytes = _processMonitor.GetTotalMemoryUsage();
            // Estimate out of 16GB limit, or better get from HardwareInfo
            Models.HardwareInfo hwInfo = Models.HardwareInfo.GetSystemInfo();
            if (hwInfo.TotalMemoryMB > 0)
            {
                double ramPercent = (totalRamBytes / 1024.0 / 1024.0 / hwInfo.TotalMemoryMB) * 100.0;
                if (ramPercent > 90) score -= 30;
                else if (ramPercent > 75) score -= 15;
                else if (ramPercent > 60) score -= 5;
            }

            // 3. Temperature Penalty (Max CPU Temp)
            var stats = _hardwareMonitor.GetTemperatureStats();
            if (stats.MaxTemp > 85) score -= 40;
            else if (stats.MaxTemp > 75) score -= 20;
            else if (stats.MaxTemp > 65) score -= 10;

            return Math.Max(0, Math.Min(100, score));
        }

        public string GetHealthStatus(int score)
        {
            if (score >= 80) return "Tốt" + " / Good";
            if (score >= 50) return "Cảnh báo" + " / Warning";
            return "Nguy hiểm" + " / Critical";
        }
        
        public System.Drawing.Color GetHealthColor(int score)
        {
            if (score >= 80) return ThemeService.Success;
            if (score >= 50) return ThemeService.Warning;
            return ThemeService.Danger;
        }
    }
}
