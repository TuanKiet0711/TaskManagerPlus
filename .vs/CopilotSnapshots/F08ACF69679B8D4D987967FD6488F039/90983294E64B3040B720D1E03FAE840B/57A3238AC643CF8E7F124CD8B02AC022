using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Services
{
    public class ProcessMonitor
    {
        private Dictionary<int, PerformanceCounter> cpuCounters;
        private Dictionary<int, DateTime> lastCpuCheckTime;
        private Dictionary<int, TimeSpan> lastCpuTime;
        private Dictionary<string, Icon> iconCache;

        public ProcessMonitor()
        {
            cpuCounters = new Dictionary<int, PerformanceCounter>();
            lastCpuCheckTime = new Dictionary<int, DateTime>();
            lastCpuTime = new Dictionary<int, TimeSpan>();
            iconCache = new Dictionary<string, Icon>();
        }

        public List<ProcessInfo> GetAllProcesses()
        {
            List<ProcessInfo> processList = new List<ProcessInfo>();
            Process[] processes = Process.GetProcesses();

            // Parallel processing for better performance
            var processInfos = new System.Collections.Concurrent.ConcurrentBag<ProcessInfo>();

            Parallel.ForEach(processes, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, process =>
            {
                try
                {
                    string filePath = GetProcessFilePath(process);

                    ProcessInfo info = new ProcessInfo
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        MemoryBytes = process.WorkingSet64,
                        MemoryUsage = FormatBytes(process.WorkingSet64),
                        ThreadCount = process.Threads.Count,
                        CpuUsage = GetCpuUsage(process),
                        Description = GetProcessDescription(process),
                        FilePath = filePath,
                        ProcessIcon = GetProcessIcon(filePath),
                        IsApp = IsUserApplication(process),
                        HasWindow = process.MainWindowHandle != IntPtr.Zero
                    };

                    processInfos.Add(info);
                }
                catch (Exception)
                {
                    // Bỏ qua các process không có quyền truy cập
                }
                finally
                {
                    try { process.Dispose(); } catch { }
                }
            });

            return processInfos.OrderByDescending(p => p.MemoryBytes).ToList();
        }

        public Dictionary<string, List<ProcessInfo>> GetGroupedProcesses()
        {
            var allProcesses = GetAllProcesses();
            var grouped = new Dictionary<string, List<ProcessInfo>>();

            // Nhóm Apps - processes có cửa sổ hoặc là ứng dụng người dùng
            var apps = allProcesses
                .Where(p => p.IsApp || p.HasWindow)
                .GroupBy(p => string.IsNullOrEmpty(p.Description) ? p.ProcessName : p.Description)
                .Select(g => new ProcessInfo
                {
                    ProcessName = g.Key,
                    Description = g.Key,
                    ProcessId = g.First().ProcessId,
                    MemoryBytes = g.Sum(p => p.MemoryBytes),
                    MemoryUsage = FormatBytes(g.Sum(p => p.MemoryBytes)),
                    CpuUsage = g.Sum(p => p.CpuUsage),
                    ThreadCount = g.Sum(p => p.ThreadCount),
                    ProcessIcon = g.First().ProcessIcon,
                    FilePath = g.First().FilePath,
                    IsGroup = g.Count() > 1,
                    ChildProcesses = g.ToList()
                })
                .OrderBy(p => p.ProcessName)
                .ToList();

            // Nhóm Background processes - tất cả các process còn lại
            var backgroundProcesses = allProcesses
                .Where(p => !p.IsApp && !p.HasWindow)
                .GroupBy(p => p.ProcessName)
                .Select(g => new ProcessInfo
                {
                    ProcessName = g.Key,
                    Description = g.First().Description,
                    ProcessId = g.First().ProcessId,
                    MemoryBytes = g.Sum(p => p.MemoryBytes),
                    MemoryUsage = FormatBytes(g.Sum(p => p.MemoryBytes)),
                    CpuUsage = g.Sum(p => p.CpuUsage),
                    ThreadCount = g.Sum(p => p.ThreadCount),
                    ProcessIcon = g.First().ProcessIcon,
                    FilePath = g.First().FilePath,
                    IsGroup = g.Count() > 1,
                    ChildProcesses = g.ToList()
                })
                .OrderBy(p => p.ProcessName)
                .ToList();

            grouped["Apps"] = apps;
            grouped["Background processes"] = backgroundProcesses;

            return grouped;
        }

        private bool IsUserApplication(Process process)
        {
            try
            {
                // Kiểm tra nếu process có main window
                if (process.MainWindowHandle != IntPtr.Zero)
                    return true;

                // Kiểm tra path - apps thường nằm trong Program Files hoặc AppData
                string filePath = GetProcessFilePath(process);
                if (!string.IsNullOrEmpty(filePath))
                {
                    string lowerPath = filePath.ToLower();
                    if (lowerPath.Contains("program files") ||
                        lowerPath.Contains("microsoft\\edge") ||
                        lowerPath.Contains("google\\chrome") ||
                        lowerPath.Contains("mozilla firefox") ||
                        lowerPath.Contains("\\windowsapps\\"))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private string GetProcessFilePath(Process process)
        {
            try
            {
                return process.MainModule?.FileName ?? "";
            }
            catch
            {
                return "";
            }
        }

        private Icon GetProcessIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            try
            {
                // Check cache first
                if (iconCache.ContainsKey(filePath))
                    return iconCache[filePath];

                Icon icon = null;
                
                // Try to extract icon from executable
                if (System.IO.File.Exists(filePath))
                {
                    try
                    {
                        // Try ExtractAssociatedIcon first
                        icon = Icon.ExtractAssociatedIcon(filePath);
                    }
                    catch
                    {
                        try
                        {
                            // Try using ExtractIcon from shell32
                            IntPtr[] large = new IntPtr[1];
                            IntPtr[] small = new IntPtr[1];
                            ExtractIconEx(filePath, 0, large, small, 1);
                            
                            if (small[0] != IntPtr.Zero)
                            {
                                icon = Icon.FromHandle(small[0]);
                            }
                            else if (large[0] != IntPtr.Zero)
                            {
                                icon = Icon.FromHandle(large[0]);
                            }
                        }
                        catch { }
                    }
                }
                
                if (icon != null)
                {
                    iconCache[filePath] = icon;
                    return icon;
                }
            }
            catch { }

            return null;
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        private double GetCpuUsage(Process process)
        {
            try
            {
                int processId = process.Id;
                DateTime currentTime = DateTime.Now;
                TimeSpan currentCpuTime = process.TotalProcessorTime;

                if (lastCpuCheckTime.ContainsKey(processId) && lastCpuTime.ContainsKey(processId))
                {
                    double cpuUsedMs = (currentCpuTime - lastCpuTime[processId]).TotalMilliseconds;
                    double totalMsPassed = (currentTime - lastCpuCheckTime[processId]).TotalMilliseconds;

                    if (totalMsPassed > 0)
                    {
                        double cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                        lastCpuCheckTime[processId] = currentTime;
                        lastCpuTime[processId] = currentCpuTime;
                        return Math.Min(cpuUsageTotal * 100, 100); // Cap at 100%
                    }
                }

                lastCpuCheckTime[processId] = currentTime;
                lastCpuTime[processId] = currentCpuTime;
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            else
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private string GetProcessDescription(Process process)
        {
            try
            {
                // Cache descriptions to avoid repeated file access
                string filePath = GetProcessFilePath(process);
                if (string.IsNullOrEmpty(filePath))
                    return process.ProcessName;
                    
                return process.MainModule?.FileVersionInfo.FileDescription ?? process.ProcessName;
            }
            catch
            {
                return process.ProcessName;
            }
        }

        public void KillProcess(int processId)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                process.Kill();
                process.WaitForExit(1000); // Wait max 1 second for clean exit
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot end process: {ex.Message}");
            }
        }

        public SystemInfo GetSystemInfo()
        {
            SystemInfo info = new SystemInfo();

            try
            {
                PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                cpuCounter.NextValue();
                System.Threading.Thread.Sleep(100);
                info.CpuUsage = cpuCounter.NextValue();
                info.AvailableRAM = ramCounter.NextValue();

                info.TotalRAM = GetTotalPhysicalMemory();
                info.UsedRAM = info.TotalRAM - info.AvailableRAM;
                info.ProcessCount = Process.GetProcesses().Length;
                info.ThreadCount = Process.GetProcesses().Sum(p =>
                {
                    try { return p.Threads.Count; }
                    catch { return 0; }
                });
            }
            catch (Exception)
            {
                // Sử dụng giá trị mặc định
            }

            return info;
        }

        private float GetTotalPhysicalMemory()
        {
            try
            {
                ObjectQuery query = new ObjectQuery("SELECT Capacity FROM Win32_PhysicalMemory");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);

                long totalBytes = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    totalBytes += Convert.ToInt64(obj["Capacity"]);
                }

                return totalBytes / (1024f * 1024f);
            }
            catch
            {
                return 8192;
            }
        }

        public void Cleanup()
        {
            foreach (var counter in cpuCounters.Values)
            {
                counter?.Dispose();
            }
            cpuCounters.Clear();
            lastCpuCheckTime.Clear();
            lastCpuTime.Clear();
        }
    }

    public class SystemInfo
    {
        public double CpuUsage { get; set; }
        public float TotalRAM { get; set; }
        public float UsedRAM { get; set; }
        public float AvailableRAM { get; set; }
        public int ProcessCount { get; set; }
        public int ThreadCount { get; set; }
    }
}