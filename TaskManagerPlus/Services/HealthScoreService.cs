using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskManagerPlus.Services
{
    public class HealthScoreData
    {
        public double TotalScore { get; set; }
        public double CpuFactor { get; set; }
        public double MemoryFactor { get; set; }
        public double TempFactor { get; set; }
    }

    public class HealthScoreService
    {
        private ProcessMonitor _processMonitor;
        private HardwareMonitor _hardwareMonitor;
        private readonly Queue<int> _recentScores = new Queue<int>();
        private const int ScoreWindow = 5;
        private HealthScoreData _lastComputedData;

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
            double cpuPenalty = 0;
            double ramPenalty = 0;
            double tempPenalty = 0;
            
            // 1. CPU Penalty
            double totalCpu = _processMonitor.GetTotalCpuUsage();
            if (totalCpu > 80) { score -= 30; cpuPenalty = 30; }
            else if (totalCpu > 60) { score -= 15; cpuPenalty = 15; }
            else if (totalCpu > 40) { score -= 5; cpuPenalty = 5; }
            
            // 2. RAM Penalty
            double totalRamBytes = _processMonitor.GetTotalMemoryUsage();
            // Estimate out of 16GB limit, or better get from HardwareInfo
            SystemInfo hwInfo = _processMonitor.GetSystemInfo();
            if (hwInfo.TotalRAM > 0)
            {
                double ramPercent = (totalRamBytes / 1024.0 / 1024.0 / hwInfo.TotalRAM) * 100.0;
                if (ramPercent > 90) { score -= 30; ramPenalty = 30; }
                else if (ramPercent > 75) { score -= 15; ramPenalty = 15; }
                else if (ramPercent > 60) { score -= 5; ramPenalty = 5; }
            }

            // 3. Temperature Penalty (Max CPU Temp)
            var stats = _hardwareMonitor.GetTemperatureStats();
            if (stats.MaxTemp > 85) { score -= 40; tempPenalty = 40; }
            else if (stats.MaxTemp > 75) { score -= 20; tempPenalty = 20; }
            else if (stats.MaxTemp > 65) { score -= 10; tempPenalty = 10; }

            score = Math.Max(0, Math.Min(100, score));

            _recentScores.Enqueue(score);
            while (_recentScores.Count > ScoreWindow)
                _recentScores.Dequeue();

            int smoothedScore = (int)Math.Round(_recentScores.Average());
            _lastComputedData = new HealthScoreData
            {
                TotalScore = smoothedScore,
                CpuFactor = cpuPenalty,
                MemoryFactor = ramPenalty,
                TempFactor = tempPenalty
            };

            return smoothedScore;
        }

        public HealthScoreData GetLastComputedData()
        {
            if (_lastComputedData == null)
                CalculateHealthScore();
            return _lastComputedData;
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

