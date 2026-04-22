using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Services
{
    public static class ReportExporter
    {
        public static void ExportToCsv(ProcessMonitor processMonitor, HardwareMonitor hardwareMonitor)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV File (*.csv)|*.csv";
                sfd.FileName = $"SystemReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var processes = processMonitor.GetAllProcesses(true).OrderByDescending(p => p.CpuUsage).ToList();
                        var tempStats = hardwareMonitor.GetTemperatureStats();
                        SystemInfo hwInfo = processMonitor.GetSystemInfo();

                        StringBuilder sb = new StringBuilder();
                        
                        // Header
                        sb.AppendLine($"Task Manager+ System Report, {DateTime.Now}");
                        sb.AppendLine($"OS: {hwInfo.OSName}, Total Memory: {hwInfo.TotalRAM} MB");
                        sb.AppendLine($"Max Temp: {tempStats.MaxTemp:F1} C, Avg Temp: {tempStats.AvgTemp:F1} C");
                        sb.AppendLine();
                        
                        // Columns
                        sb.AppendLine("Process ID,Process Name,Status,CPU Usage (%),Memory (MB),Disk Usage (B/s),Is Background");

                        // Data
                        foreach (var p in processes)
                        {
                            sb.AppendLine($"{p.ProcessId},\"{p.ProcessName}\",\"{p.Status}\",{p.CpuUsage:F2},{p.MemoryBytes / 1024.0 / 1024.0:F2},{0},{(!p.HasWindow)}");
                        }

                        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        MessageBox.Show("Report exported successfully!", "Export Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export report:\n{ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}


