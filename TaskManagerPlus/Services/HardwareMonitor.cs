using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Services
{
    public class HardwareMonitor
    {
        private Dictionary<string, PerformanceCounter> performanceCounters;
        private Dictionary<string, long> lastNetworkBytes;
        private DateTime lastNetworkCheck;
        private int processorCount;
        private string cpuName;
        private double baseSpeed;

        public HardwareMonitor()
        {
            performanceCounters = new Dictionary<string, PerformanceCounter>();
            lastNetworkBytes = new Dictionary<string, long>();
            lastNetworkCheck = DateTime.Now;
            processorCount = Environment.ProcessorCount;
            InitializeCpuInfo();
        }

        private void InitializeCpuInfo()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        cpuName = obj["Name"]?.ToString() ?? "Unknown CPU";
                        baseSpeed = Convert.ToDouble(obj["MaxClockSpeed"] ?? 0) / 1000.0; // Convert to GHz
                        break;
                    }
                }
            }
            catch
            {
                cpuName = "Unknown CPU";
                baseSpeed = 0;
            }
        }

        public CpuInfo GetCpuInfo()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return new CpuInfo
                        {
                            Name = obj["Name"]?.ToString() ?? "Unknown CPU",
                            Cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0),
                            LogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0),
                            BaseSpeed = Convert.ToDouble(obj["MaxClockSpeed"] ?? 0) / 1000.0,
                            CurrentSpeed = Convert.ToDouble(obj["CurrentClockSpeed"] ?? 0) / 1000.0,
                            Temperature = GetCpuTemperature(),
                            Usage = 0,
                            Type = "CPU"
                        };
                    }
                }
            }
            catch { }

            return new CpuInfo
            {
                Name = cpuName,
                Cores = processorCount / 2,
                LogicalProcessors = processorCount,
                BaseSpeed = baseSpeed,
                CurrentSpeed = baseSpeed,
                Usage = 0,
                Type = "CPU"
            };
        }

        public List<GpuInfo> GetGpuInfo()
        {
            List<GpuInfo> gpus = new List<GpuInfo>();

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
                {
                    int gpuIndex = 0;
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            long adapterRam = Convert.ToInt64(obj["AdapterRAM"] ?? 0);
                            string driverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown";
                            
                            GpuInfo gpu = new GpuInfo
                            {
                                Name = obj["Name"]?.ToString() ?? $"GPU {gpuIndex}",
                                Type = "GPU",
                                DedicatedMemory = FormatBytes(adapterRam),
                                Usage = GetGpuUsage(gpuIndex),
                                Temperature = GetGpuTemperature(gpuIndex),
                                Details = $"Driver: {driverVersion}"
                            };

                            gpu.UsageText = $"{gpu.Usage:F1}%";
                            gpus.Add(gpu);
                            gpuIndex++;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            if (gpus.Count == 0)
            {
                gpus.Add(new GpuInfo
                {
                    Name = "GPU 0",
                    Type = "GPU",
                    Usage = 0,
                    UsageText = "0%",
                    DedicatedMemory = "N/A"
                });
            }

            return gpus;
        }

        public List<DiskInfo> GetDiskInfo()
        {
            List<DiskInfo> disks = new List<DiskInfo>();

            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in drives.Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    try
                    {
                        double activeTime = GetDiskActiveTime(drive.Name[0]);
                        var diskSpeed = GetDiskSpeed(drive.Name[0]);
                        
                        DiskInfo disk = new DiskInfo
                        {
                            Name = $"Disk ({drive.Name.TrimEnd('\\')})",
                            DriveLetter = drive.Name,
                            Type = GetDiskType(drive.Name[0]),
                            TotalSize = drive.TotalSize,
                            FreeSpace = drive.TotalFreeSpace,
                            Capacity = FormatBytes(drive.TotalSize),
                            Usage = ((drive.TotalSize - drive.TotalFreeSpace) / (double)drive.TotalSize) * 100,
                            ActiveTime = activeTime,
                            ReadSpeed = diskSpeed.Item1,
                            WriteSpeed = diskSpeed.Item2,
                            Details = $"{FormatBytes(drive.TotalSize - drive.TotalFreeSpace)} / {FormatBytes(drive.TotalSize)}",
                            Temperature = GetDiskTemperature(drive.Name[0])
                        };

                        disk.UsageText = $"{disk.ActiveTime:F0}%";
                        disk.Speed = $"R: {FormatSpeed(disk.ReadSpeed)} W: {FormatSpeed(disk.WriteSpeed)}";
                        disks.Add(disk);
                    }
                    catch { }
                }
            }
            catch { }

            return disks;
        }

        public List<NetworkInfo> GetNetworkInfo()
        {
            List<NetworkInfo> networks = new List<NetworkInfo>();
            DateTime now = DateTime.Now;
            double timeSpan = (now - lastNetworkCheck).TotalSeconds;

            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface ni in interfaces.Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                {
                    try
                    {
                        IPv4InterfaceStatistics stats = ni.GetIPv4Statistics();
                        long totalBytes = stats.BytesSent + stats.BytesReceived;
                        
                        double sendSpeed = 0;
                        double receiveSpeed = 0;

                        if (lastNetworkBytes.ContainsKey(ni.Id) && timeSpan > 0)
                        {
                            long bytesDiff = totalBytes - lastNetworkBytes[ni.Id];
                            sendSpeed = (stats.BytesSent - (lastNetworkBytes.ContainsKey(ni.Id + "_sent") ? lastNetworkBytes[ni.Id + "_sent"] : 0)) / timeSpan / 1024;
                            receiveSpeed = (stats.BytesReceived - (lastNetworkBytes.ContainsKey(ni.Id + "_recv") ? lastNetworkBytes[ni.Id + "_recv"] : 0)) / timeSpan / 1024;
                        }

                        lastNetworkBytes[ni.Id] = totalBytes;
                        lastNetworkBytes[ni.Id + "_sent"] = stats.BytesSent;
                        lastNetworkBytes[ni.Id + "_recv"] = stats.BytesReceived;

                        string ipAddress = "N/A";
                        var ipProps = ni.GetIPProperties();
                        var unicastAddresses = ipProps.UnicastAddresses;
                        if (unicastAddresses.Count > 0)
                        {
                            var ipv4 = unicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                            if (ipv4 != null)
                                ipAddress = ipv4.Address.ToString();
                        }

                        long speedBits = ni.Speed;
                        string linkSpeed = speedBits >= 1000000000 ? $"{speedBits / 1000000000.0:F1} Gbps" : $"{speedBits / 1000000.0:F0} Mbps";

                        NetworkInfo network = new NetworkInfo
                        {
                            Name = ni.Name,
                            AdapterName = ni.Description,
                            Type = ni.NetworkInterfaceType.ToString(),
                            ConnectionType = ni.NetworkInterfaceType.ToString(),
                            IpAddress = ipAddress,
                            SendSpeed = sendSpeed,
                            ReceiveSpeed = receiveSpeed,
                            Usage = Math.Min((sendSpeed + receiveSpeed) * 8 / (speedBits / 1024.0) * 100, 100),
                            UsageText = $"S: {FormatSpeed(sendSpeed)} R: {FormatSpeed(receiveSpeed)}",
                            Speed = linkSpeed
                        };

                        networks.Add(network);
                    }
                    catch { }
                }
            }
            catch { }

            lastNetworkCheck = now;
            return networks;
        }

        private double GetCpuTemperature()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        double temp = Convert.ToDouble(obj["CurrentTemperature"]);
                        return (temp - 2732) / 10.0; // Convert from decikelvin to Celsius
                    }
                }
            }
            catch { }
            return 0;
        }

        private double GetGpuTemperature(int gpuIndex)
        {
            // GPU temperature requires specific drivers/software
            // This is a placeholder - would need OpenHardwareMonitor or similar
            return 0;
        }

        private double GetDiskTemperature(char driveLetter)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSStorageDriver_ATAPISmartData"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        byte[] smartData = (byte[])obj["VendorSpecific"];
                        if (smartData != null && smartData.Length > 0)
                        {
                            // Parse SMART data for temperature (attribute 194)
                            // This is simplified - real implementation needs proper SMART parsing
                            return 0;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        private double GetGpuUsage(int gpuIndex)
        {
            try
            {
                string counterKey = $"GPU_{gpuIndex}";
                if (!performanceCounters.ContainsKey(counterKey))
                {
                    performanceCounters[counterKey] = new PerformanceCounter("GPU Engine", "Utilization Percentage", "_engtype_3D");
                }
                return performanceCounters[counterKey].NextValue();
            }
            catch
            {
                return 0;
            }
        }

        private double GetDiskActiveTime(char driveLetter)
        {
            try
            {
                string counterKey = $"Disk_{driveLetter}";
                if (!performanceCounters.ContainsKey(counterKey))
                {
                    performanceCounters[counterKey] = new PerformanceCounter("PhysicalDisk", "% Disk Time", $"{(int)driveLetter - 65} {driveLetter}:");
                }
                return performanceCounters[counterKey].NextValue();
            }
            catch
            {
                return 0;
            }
        }

        private Tuple<double, double> GetDiskSpeed(char driveLetter)
        {
            try
            {
                string readKey = $"DiskRead_{driveLetter}";
                string writeKey = $"DiskWrite_{driveLetter}";
                string diskName = $"{(int)driveLetter - 65} {driveLetter}:";

                if (!performanceCounters.ContainsKey(readKey))
                {
                    performanceCounters[readKey] = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", diskName);
                }
                if (!performanceCounters.ContainsKey(writeKey))
                {
                    performanceCounters[writeKey] = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", diskName);
                }

                double readSpeed = performanceCounters[readKey].NextValue() / 1024; // KB/s
                double writeSpeed = performanceCounters[writeKey].NextValue() / 1024; // KB/s

                return Tuple.Create(readSpeed, writeSpeed);
            }
            catch
            {
                return Tuple.Create(0.0, 0.0);
            }
        }

        private string GetDiskType(char driveLetter)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_DiskDrive WHERE DeviceID LIKE '%{driveLetter}%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string mediaType = obj["MediaType"]?.ToString() ?? "";
                        if (mediaType.Contains("SSD") || mediaType.Contains("Solid State"))
                            return "SSD";
                        else if (mediaType.Contains("HDD") || mediaType.Contains("Hard Disk"))
                            return "HDD";
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }

        private string FormatSpeed(double kbps)
        {
            if (kbps < 1024)
                return $"{kbps:F0} KB/s";
            else
                return $"{kbps / 1024:F2} MB/s";
        }

        public void Cleanup()
        {
            foreach (var counter in performanceCounters.Values)
            {
                counter?.Dispose();
            }
            performanceCounters.Clear();
        }
    }
}
