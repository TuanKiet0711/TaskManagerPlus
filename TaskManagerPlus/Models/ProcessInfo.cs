using System;
using System.Collections.Generic;
using System.Drawing;

namespace TaskManagerPlus.Models
{
    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string MemoryUsage { get; set; }
        public long MemoryBytes { get; set; }
        public double CpuUsage { get; set; }
        public int ThreadCount { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }
        public Icon ProcessIcon { get; set; }
        public string FilePath { get; set; }

        // Thuộc tính mới để hỗ trợ grouping giống Task Manager
        public bool IsApp { get; set; }
        public bool HasWindow { get; set; }
        public bool IsGroup { get; set; }
        public List<ProcessInfo> ChildProcesses { get; set; }
        
        // Thuộc tính mới cho Disk và Network
        public double DiskUsage { get; set; }
        public double NetworkUsage { get; set; }
        public string DiskUsageFormatted { get; set; }
        public string NetworkUsageFormatted { get; set; }

        public ProcessInfo()
        {
            Status = "Running";
            ChildProcesses = new List<ProcessInfo>();
            DiskUsageFormatted = "0 MB/s";
            NetworkUsageFormatted = "0 Mbps";
        }

        public string GetMemoryFormatted()
        {
            if (MemoryBytes < 1024)
                return $"{MemoryBytes} B";
            else if (MemoryBytes < 1024 * 1024)
                return $"{MemoryBytes / 1024.0:F2} KB";
            else if (MemoryBytes < 1024 * 1024 * 1024)
                return $"{MemoryBytes / (1024.0 * 1024.0):F1} MB";
            else
                return $"{MemoryBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}