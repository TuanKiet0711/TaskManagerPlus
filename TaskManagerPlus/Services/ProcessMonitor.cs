using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Services
{
    public class ProcessMonitor
    {
        private readonly Dictionary<int, DateTime> lastCpuCheckTime;
        private readonly Dictionary<int, TimeSpan> lastCpuTime;

        private readonly ConcurrentDictionary<string, System.Drawing.Icon> iconCache;
        private readonly ConcurrentDictionary<string, string> descCache;

        public ProcessMonitor()
        {
            lastCpuCheckTime = new Dictionary<int, DateTime>();
            lastCpuTime = new Dictionary<int, TimeSpan>();

            iconCache = new ConcurrentDictionary<string, System.Drawing.Icon>();
            descCache = new ConcurrentDictionary<string, string>();
        }

        // =========================
        // Processes
        // =========================
        public List<ProcessInfo> GetAllProcesses()
        {
            Process[] processes = Process.GetProcesses();
            ConcurrentBag<ProcessInfo> bag = new ConcurrentBag<ProcessInfo>();

            Parallel.ForEach(processes, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, p =>
            {
                try
                {
                    int pid = p.Id;

                    // some processes may throw
                    int sessionId = SafeGetInt(() => p.SessionId, -1);
                    bool hasWindow = SafeGetBool(() => p.MainWindowHandle != IntPtr.Zero, false);
                    long mem = SafeGetLong(() => p.WorkingSet64, 0L);

                    string filePath = GetExecutablePathWmi(pid);
                    string desc = GetDescriptionCached(p.ProcessName, filePath);

                    ProcessInfo info = new ProcessInfo();
                    info.ProcessId = pid;
                    info.ProcessName = p.ProcessName;
                    info.SessionId = sessionId;

                    info.ParentProcessId = GetParentProcessIdWmi(pid);

                    info.MemoryBytes = mem;
                    info.MemoryUsage = FormatBytes(mem);
                    info.ThreadCount = SafeGetInt(() => p.Threads.Count, 0);
                    info.CpuUsage = GetCpuUsage(p);

                    info.Description = desc;
                    info.FilePath = filePath;
                    info.ProcessIcon = GetIconCached(filePath);

                    info.HasWindow = hasWindow;

                    // Apps decision will be made in GetGroupedProcesses
                    info.IsApp = hasWindow;

                    if (string.IsNullOrWhiteSpace(info.DiskUsageFormatted)) info.DiskUsageFormatted = "0 MB/s";
                    if (string.IsNullOrWhiteSpace(info.NetworkUsageFormatted)) info.NetworkUsageFormatted = "0 Mbps";

                    bag.Add(info);
                }
                catch
                {
                    // ignore inaccessible processes
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            });

            return bag.OrderByDescending(x => x.MemoryBytes).ToList();
        }

        /// <summary>
        /// Task-Manager-like grouping:
        /// - Filter by CURRENT USER SESSION -> background count closer to Task Manager
        /// - Apps = HasWindow only
        /// - Children by ParentProcessId
        /// - Light heuristic re-home to VS/Chrome/Edge
        /// </summary>
        public Dictionary<string, List<ProcessInfo>> GetGroupedProcesses()
        {
            List<ProcessInfo> all = GetAllProcesses();

            // 1) Filter: only current session (critical for "195 vs 89" problem)
            int currentSession = Process.GetCurrentProcess().SessionId;
            all = all.Where(p => p.SessionId == currentSession).ToList();

            // 2) Remove idle/invalid
            all = all.Where(p => p.ProcessId != 0 && !string.IsNullOrWhiteSpace(p.ProcessName)).ToList();

            // Apps roots: HasWindow only
            List<ProcessInfo> roots = all
                .Where(p => p.HasWindow)
                .OrderBy(p => string.IsNullOrWhiteSpace(p.Description) ? p.ProcessName : p.Description)
                .ToList();

            // parent -> children map
            Dictionary<int, List<ProcessInfo>> childrenMap = new Dictionary<int, List<ProcessInfo>>();
            foreach (ProcessInfo p in all)
            {
                int parent = p.ParentProcessId;
                if (parent <= 0) continue;

                List<ProcessInfo> list;
                if (!childrenMap.TryGetValue(parent, out list))
                {
                    list = new List<ProcessInfo>();
                    childrenMap[parent] = list;
                }
                list.Add(p);
            }

            // rootPid -> descendants
            Dictionary<int, List<ProcessInfo>> groupChildren = new Dictionary<int, List<ProcessInfo>>();
            HashSet<int> used = new HashSet<int>();

            foreach (ProcessInfo r in roots)
            {
                List<ProcessInfo> desc = GetDescendants(r.ProcessId, childrenMap);

                used.Add(r.ProcessId);
                foreach (var c in desc) used.Add(c.ProcessId);

                groupChildren[r.ProcessId] = desc;
            }

            // Heuristic re-home pass
            ProcessInfo vsRoot = roots.FirstOrDefault(p => IsVisualStudioRoot(p));
            ProcessInfo chromeRoot = roots.FirstOrDefault(p => IsChromeRoot(p));
            ProcessInfo edgeRoot = roots.FirstOrDefault(p => IsEdgeRoot(p));
            ProcessInfo explorerRoot = roots.FirstOrDefault(p => IsExplorerRoot(p));

            foreach (ProcessInfo p in all)
            {
                if (p.HasWindow) continue;
                if (used.Contains(p.ProcessId)) continue;

                if (vsRoot != null && IsVisualStudioChild(p))
                {
                    groupChildren[vsRoot.ProcessId].Add(p);
                    used.Add(p.ProcessId);
                    continue;
                }

                if (chromeRoot != null && IsChromeChild(p))
                {
                    groupChildren[chromeRoot.ProcessId].Add(p);
                    used.Add(p.ProcessId);
                    continue;
                }

                if (edgeRoot != null && IsEdgeChild(p))
                {
                    groupChildren[edgeRoot.ProcessId].Add(p);
                    used.Add(p.ProcessId);
                    continue;
                }

                // Some Windows shell related stuff often belongs under Explorer in Task Manager
                if (explorerRoot != null && IsExplorerChild(p))
                {
                    groupChildren[explorerRoot.ProcessId].Add(p);
                    used.Add(p.ProcessId);
                    continue;
                }
            }

            // Build Apps list
            List<ProcessInfo> appsGroup = new List<ProcessInfo>();
            foreach (ProcessInfo root in roots)
            {
                List<ProcessInfo> kids;
                if (!groupChildren.TryGetValue(root.ProcessId, out kids))
                    kids = new List<ProcessInfo>();

                kids = kids.GroupBy(x => x.ProcessId).Select(g => g.First()).ToList();

                ProcessInfo groupRow = new ProcessInfo();
                groupRow.ProcessId = root.ProcessId;
                groupRow.ParentProcessId = root.ParentProcessId;
                groupRow.SessionId = root.SessionId;

                groupRow.ProcessName = root.ProcessName;
                groupRow.Description = string.IsNullOrWhiteSpace(root.Description) ? root.ProcessName : root.Description;
                groupRow.FilePath = root.FilePath;
                groupRow.ProcessIcon = root.ProcessIcon;

                groupRow.HasWindow = true;
                groupRow.IsApp = true;

                groupRow.ChildProcesses = kids.OrderByDescending(x => x.MemoryBytes).ToList();
                groupRow.IsGroup = groupRow.ChildProcesses != null && groupRow.ChildProcesses.Count > 0;

                long memSum = root.MemoryBytes;
                double cpuSum = root.CpuUsage;
                int threadSum = root.ThreadCount;

                foreach (var c in groupRow.ChildProcesses)
                {
                    memSum += c.MemoryBytes;
                    cpuSum += c.CpuUsage;
                    threadSum += c.ThreadCount;
                }

                groupRow.MemoryBytes = memSum;
                groupRow.MemoryUsage = FormatBytes(memSum);
                groupRow.CpuUsage = cpuSum;
                groupRow.ThreadCount = threadSum;

                groupRow.DiskUsageFormatted = "0 MB/s";
                groupRow.NetworkUsageFormatted = "0 Mbps";

                appsGroup.Add(groupRow);
            }

            // Background = anything not used as root or child
            HashSet<int> allUsed = new HashSet<int>();
            foreach (var r in roots) allUsed.Add(r.ProcessId);
            foreach (var kv in groupChildren)
                foreach (var c in kv.Value) allUsed.Add(c.ProcessId);

            List<ProcessInfo> background = all
                .Where(p => !allUsed.Contains(p.ProcessId))
                .OrderBy(p => p.ProcessName)
                .ToList();

            Dictionary<string, List<ProcessInfo>> grouped = new Dictionary<string, List<ProcessInfo>>();
            grouped["Apps"] = appsGroup;
            grouped["Background processes"] = background;
            return grouped;
        }

        private List<ProcessInfo> GetDescendants(int rootPid, Dictionary<int, List<ProcessInfo>> childrenMap)
        {
            List<ProcessInfo> result = new List<ProcessInfo>();
            Queue<int> q = new Queue<int>();
            HashSet<int> visited = new HashSet<int>();

            q.Enqueue(rootPid);
            visited.Add(rootPid);

            while (q.Count > 0)
            {
                int cur = q.Dequeue();

                List<ProcessInfo> kids;
                if (!childrenMap.TryGetValue(cur, out kids)) continue;

                foreach (var k in kids)
                {
                    if (visited.Contains(k.ProcessId)) continue;
                    visited.Add(k.ProcessId);

                    result.Add(k);
                    q.Enqueue(k.ProcessId);
                }
            }

            return result;
        }

        // ===== heuristics =====
        private bool IsVisualStudioRoot(ProcessInfo p)
        {
            string n = (p.ProcessName ?? "").ToLowerInvariant();
            string d = (p.Description ?? "").ToLowerInvariant();
            return n == "devenv" || d.Contains("visual studio");
        }

        private bool IsVisualStudioChild(ProcessInfo p)
        {
            string n = (p.ProcessName ?? "").ToLowerInvariant();
            string d = (p.Description ?? "").ToLowerInvariant();

            if (n.StartsWith("servicehub.")) return true;
            if (n == "vbcscompiler") return true;
            if (n.Contains("msbuild")) return true;
            if (d.Contains("servicehub")) return true;
            if (d.Contains("roslyn")) return true;
            return false;
        }

        private bool IsChromeRoot(ProcessInfo p)
        {
            string n = (p.ProcessName ?? "").ToLowerInvariant();
            string d = (p.Description ?? "").ToLowerInvariant();
            return n == "chrome" || d.Contains("google chrome");
        }

        private bool IsChromeChild(ProcessInfo p)
        {
            string n = (p.ProcessName ?? "").ToLowerInvariant();
            if (n == "chrome") return true;
            if (n == "crashpad_handler") return true;
            return false;
        }

        private bool IsEdgeRoot(ProcessInfo p)
        {
            string n = (p.ProcessName ?? "").ToLowerInvariant();
            string d = (p.Description ?? "").ToLowerInvariant();
            return n == "msedge" || d.Contains("microsoft edge");
        }

        private bool IsEdgeChild(ProcessInfo p)
        {
            string n = (p.ProcessName ?? "").ToLowerInvariant();
            if (n == "msedge") return true;
            if (n == "msedgewebview2") return true;
            if (n == "crashpad_handler") return true;
            return false;
        }

        private bool IsExplorerRoot(ProcessInfo p)
        {
            string n = (p.ProcessName ?? "").ToLowerInvariant();
            string d = (p.Description ?? "").ToLowerInvariant();
            return n == "explorer" || d.Contains("windows explorer");
        }

        private bool IsExplorerChild(ProcessInfo p)
        {
            string n = (p.ProcessName ?? "").ToLowerInvariant();
            // A few common shell-related background processes
            if (n == "shellexperiencehost") return true;
            if (n == "searchhost") return true;
            if (n == "startmenuexperiencehost") return true;
            if (n == "textinputhost") return true;
            if (n == "applicationframehost") return true;
            return false;
        }

        public void KillProcess(int processId)
        {
            try
            {
                Process proc = Process.GetProcessById(processId);
                proc.Kill();
                proc.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot end process: " + ex.Message);
            }
        }

        // =========================
        // System info
        // =========================
        public SystemInfo GetSystemInfo()
        {
            SystemInfo info = new SystemInfo();

            try
            {
                using (PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                using (PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes"))
                {
                    cpuCounter.NextValue();
                    Thread.Sleep(120);

                    info.CpuUsage = cpuCounter.NextValue();
                    info.AvailableRAM = ramCounter.NextValue();

                    info.TotalRAM = GetTotalPhysicalMemoryMB();
                    info.UsedRAM = info.TotalRAM - info.AvailableRAM;
                }

                Process[] ps = Process.GetProcesses();
                info.ProcessCount = ps.Length;

                int threads = 0;
                foreach (Process p in ps)
                {
                    try { threads += p.Threads.Count; }
                    catch { }
                    finally { try { p.Dispose(); } catch { } }
                }
                info.ThreadCount = threads;
            }
            catch { }

            return info;
        }

        private float GetTotalPhysicalMemoryMB()
        {
            try
            {
                ObjectQuery query = new ObjectQuery("SELECT Capacity FROM Win32_PhysicalMemory");
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    long totalBytes = 0;
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object cap = obj["Capacity"];
                        if (cap != null) totalBytes += Convert.ToInt64(cap);
                    }
                    return totalBytes / (1024f * 1024f);
                }
            }
            catch
            {
                return 8192f;
            }
        }

        // =========================
        // Cleanup
        // =========================
        public void Cleanup()
        {
            try
            {
                lastCpuCheckTime.Clear();
                lastCpuTime.Clear();

                foreach (var kv in iconCache)
                {
                    try { if (kv.Value != null) kv.Value.Dispose(); } catch { }
                }

                System.Drawing.Icon dummy;
                while (!iconCache.IsEmpty)
                {
                    foreach (var key in iconCache.Keys)
                        iconCache.TryRemove(key, out dummy);
                }

                string dummyS;
                while (!descCache.IsEmpty)
                {
                    foreach (var key in descCache.Keys)
                        descCache.TryRemove(key, out dummyS);
                }
            }
            catch { }
        }

        // =========================
        // WMI helpers
        // =========================
        private int GetParentProcessIdWmi(int pid)
        {
            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId=" + pid))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object v = obj["ParentProcessId"];
                        if (v != null) return Convert.ToInt32(v);
                    }
                }
            }
            catch { }
            return 0;
        }

        private string GetExecutablePathWmi(int pid)
        {
            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("SELECT ExecutablePath FROM Win32_Process WHERE ProcessId=" + pid))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object v = obj["ExecutablePath"];
                        return v == null ? "" : v.ToString();
                    }
                }
            }
            catch { }
            return "";
        }

        private string GetDescriptionCached(string processName, string filePath)
        {
            string key = string.IsNullOrWhiteSpace(filePath) ? "proc:" + processName : filePath;

            string cached;
            if (descCache.TryGetValue(key, out cached))
                return cached;

            string desc = processName;

            try
            {
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    FileVersionInfo vi = FileVersionInfo.GetVersionInfo(filePath);
                    if (!string.IsNullOrWhiteSpace(vi.FileDescription))
                        desc = vi.FileDescription.Trim();
                }
            }
            catch { }

            descCache[key] = desc;
            return desc;
        }

        private System.Drawing.Icon GetIconCached(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;

            return iconCache.GetOrAdd(filePath, fp =>
            {
                return IconHelper.GetIconFromPath(fp, true);
            });
        }

        private double GetCpuUsage(Process p)
        {
            try
            {
                int pid = p.Id;
                DateTime now = DateTime.UtcNow;
                TimeSpan cpu = p.TotalProcessorTime;

                DateTime lastT;
                TimeSpan lastC;

                if (lastCpuCheckTime.TryGetValue(pid, out lastT) &&
                    lastCpuTime.TryGetValue(pid, out lastC))
                {
                    double cpuUsedMs = (cpu - lastC).TotalMilliseconds;
                    double totalMs = (now - lastT).TotalMilliseconds;

                    if (totalMs > 0)
                    {
                        double usage = cpuUsedMs / (Environment.ProcessorCount * totalMs) * 100.0;
                        lastCpuCheckTime[pid] = now;
                        lastCpuTime[pid] = cpu;

                        if (usage < 0) usage = 0;
                        if (usage > 100) usage = 100;
                        return usage;
                    }
                }

                lastCpuCheckTime[pid] = now;
                lastCpuTime[pid] = cpu;
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private static int SafeGetInt(Func<int> getter, int fallback)
        {
            try { return getter(); } catch { return fallback; }
        }

        private static long SafeGetLong(Func<long> getter, long fallback)
        {
            try { return getter(); } catch { return fallback; }
        }

        private static bool SafeGetBool(Func<bool> getter, bool fallback)
        {
            try { return getter(); } catch { return fallback; }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024L * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
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
