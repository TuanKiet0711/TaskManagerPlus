using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Services
{
    public class SmartAlertService
    {
        private readonly ProcessMonitor _processMonitor;
        private readonly HardwareMonitor _hardwareMonitor;
        private bool _isRunning;
        private Thread _workerThread;
        
        public event EventHandler<AlertInfo> OnAlertGenerated;
        
        // Track high CPU usage over time: PID -> consecutive high CPU ticks
        private readonly ConcurrentDictionary<int, int> _highCpuTracker = new ConcurrentDictionary<int, int>();
        private const int MAX_HIGH_CPU_TICKS = 5; // e.g. 5 ticks * 5 seconds = 25 seconds of high CPU
        private const double CPU_USAGE_THRESHOLD = 75.0; // 75%
        
        // Auto-suspend / Eco user rules
        public List<AutoSuspendRule> AutoRules { get; set; } = new List<AutoSuspendRule>();

        public SmartAlertService(ProcessMonitor processMonitor, HardwareMonitor hardwareMonitor)
        {
            _processMonitor = processMonitor;
            _hardwareMonitor = hardwareMonitor;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _workerThread = new Thread(WorkerLoop) { IsBackground = true, Name = "SmartAlertWorker" };
            _workerThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void WorkerLoop()
        {
            while (_isRunning)
            {
                try
                {
                    CheckSystemHealth();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in SmartAlert worker: " + ex.Message);
                }
                Thread.Sleep(5000); // Check every 5 seconds
            }
        }

        private void CheckSystemHealth()
        {
            // 1. Check for processes with excessive CPU usage
            var allProcesses = _processMonitor.GetAllProcesses(true);
            
            // Clean up dead processes from tracker
            var activePids = new HashSet<int>(allProcesses.Select(p => p.ProcessId));
            foreach (var pid in _highCpuTracker.Keys.ToList())
            {
                if (!activePids.Contains(pid))
                {
                    _highCpuTracker.TryRemove(pid, out _);
                }
            }

            foreach (var p in allProcesses)
            {
                if (p.ProcessId == 0 || p.ProcessName == "Idle") continue;

                if (p.CpuUsage >= CPU_USAGE_THRESHOLD)
                {
                    int ticks = _highCpuTracker.AddOrUpdate(p.ProcessId, 1, (key, old) => old + 1);
                    if (ticks == MAX_HIGH_CPU_TICKS)
                    {
                        // Apply AutoRules if it matches
                        var rule = AutoRules.Find(r => r.ProcessName.Equals(p.ProcessName, StringComparison.OrdinalIgnoreCase));
                        if (rule != null && ((p.CpuUsage > rule.CpuThreshold && rule.CpuThreshold > 0) || (p.MemoryBytes / 1024 / 1024 > rule.RamThresholdMB && rule.RamThresholdMB > 0)))
                        {
                            ApplyAutoRuleAction(rule, p.ProcessId);
                            _highCpuTracker.TryRemove(p.ProcessId, out _);
                        }
                        else
                        {
                            GenerateAlert(new AlertInfo
                            {
                                Severity = AlertSeverity.Warning,
                                Title = "High CPU Usage Detected",
                                Message = $"The app '{p.ProcessName}' is consistently consuming high CPU ({p.CpuUsage:F1}%). This may drain your battery and cause heating.",
                                RelatedProcessId = p.ProcessId,
                                RelatedProcessName = p.ProcessName,
                                Action = SuggestedAction.SetEcoMode
                            });
                        }
                    }
                    else if (ticks == MAX_HIGH_CPU_TICKS * 2) 
                    {
                        // Escalate if it continues
                        GenerateAlert(new AlertInfo
                        {
                            Severity = AlertSeverity.Critical,
                            Title = "Critical Resource Drain",
                            Message = $"'{p.ProcessName}' is still consuming unusual amounts of CPU. We strongly suggest suspending it.",
                            RelatedProcessId = p.ProcessId,
                            RelatedProcessName = p.ProcessName,
                            Action = SuggestedAction.SuspendProcess
                        });
                    }
                }
                else
                {
                    // Reset if it drops below threshold
                    _highCpuTracker.TryRemove(p.ProcessId, out _);
                }
            }

            // 2. Check System Temperature
            var systemInfo = _hardwareMonitor.GetCpuInfo();
            if (systemInfo.Temperature > 85.0) // 85C is quite hot
            {
                // Find top CPU consumer to blame
                var topApp = allProcesses.OrderByDescending(p => p.CpuUsage).FirstOrDefault();
                if (topApp != null && topApp.CpuUsage > 30.0)
                {
                    GenerateAlert(new AlertInfo
                    {
                        Severity = AlertSeverity.Critical,
                        Title = "System Overheating Warning",
                        Message = $"System temperature is high ({systemInfo.Temperature:F1}°C). '{topApp.ProcessName}' seems to be the main cause. Suspend or kill it to cool down.",
                        RelatedProcessId = topApp.ProcessId,
                        RelatedProcessName = topApp.ProcessName,
                        Action = SuggestedAction.SuspendProcess
                    });
                }
                else
                {
                     GenerateAlert(new AlertInfo
                    {
                        Severity = AlertSeverity.Warning,
                        Title = "System Overheating",
                        Message = $"System temperature is high ({systemInfo.Temperature:F1}°C). Consider checking your cooling or closing background apps.",
                        Action = SuggestedAction.None
                    });
                }
            }
        }

        private void GenerateAlert(AlertInfo alert)
        {
            OnAlertGenerated?.Invoke(this, alert);
        }

        private void ApplyAutoRuleAction(AutoSuspendRule rule, int processId)
        {
            switch (rule.Action)
            {
                case SuggestedAction.KillProcess:
                    _processMonitor.KillProcess(processId);
                    break;
                case SuggestedAction.SuspendProcess:
                    _processMonitor.SuspendProcess(processId);
                    break;
                case SuggestedAction.SetEcoMode:
                    _processMonitor.ChangePriority(processId, System.Diagnostics.ProcessPriorityClass.Idle);
                    break;
            }
        }
    }
}

