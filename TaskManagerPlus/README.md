# Task Manager Plus - Qu?n lý ti?n trình nâng cao

## Mô t?
Task Manager Plus là ?ng d?ng qu?n lý ti?n trình nâng cao ???c xây d?ng b?ng C# WinForms v?i giao di?n hi?n ??i gi?ng Windows Task Manager, cho phép theo dõi chi ti?t và qu?n lý h? th?ng.

## ? Tính n?ng m?i (v3.0) - MAJOR UPDATE!

### ?? **Tab Processes - Nâng cao**
- ? **Process Icons**: Hi?n th? icon c?a t?ng ?ng d?ng
- ? **Smart Sorting**: Click vào b?t k? column nào ?? s?p x?p
  - Sort indicators (??)
  - Sort by: Name, PID, CPU, Memory, Threads
- ? **Color-coded CPU**: 
  - ?? < 25%: Normal
  - ?? 25-50%: Moderate
  - ?? > 50%: High
- ? **Icon caching**: T?i ?u hi?u n?ng
- ? **Row height**: 24px v?i icons ??p

### ?? **Tab Performance - Chi ti?t ??y ??**
- **CPU Information**:
  - ? CPU Name (AMD Ryzen, Intel Core, etc.)
  - ? Base speed (GHz)
  - ? Current speed (GHz)
  - ? Cores count
  - ? Logical processors (threads)
  - ? Temperature (°C) n?u h? tr?
  
- **Memory Information**:
  - ? Total RAM
  - ? In use / Available
  - ? Usage percentage
  - ? Committed memory

- **GPU Information** (Multi-GPU support):
  - ? GPU Name
  - ? Dedicated memory
  - ? GPU usage %
  - ? Driver version
  - ? Temperature
  
- **Disk Information** (All drives):
  - ? Disk type (SSD/HDD detection)
  - ? Active time %
  - ? Read speed (MB/s)
  - ? Write speed (MB/s)
  - ? Capacity / Used space
  - ? Temperature

- **Network Information** (All adapters):
  - ? Adapter name
  - ? Connection type (Ethernet, WiFi, etc.)
  - ? Send speed (KB/s, MB/s)
  - ? Receive speed
  - ? Link speed (Mbps/Gbps)
  - ? IP Address

### ??? **Tab Temperature - M?I!**
- ?? **Real-time Temperature Monitoring**
- ? CPU Temperature with history chart
- ? GPU Temperature (multi-GPU)
- ? Disk Temperature (SMART data)
- ? Color-coded status:
  - ?? Cool: < 50°C
  - ?? Normal: 50-70°C
  - ?? High: 70-80°C
  - ?? Critical: > 80°C
- ? 60-second history graphs
- ? Auto-scroll panel

### ?? **Tab Startup**
- ? Manage startup applications
- ? Enable/Disable apps
- ? Publisher information
- ? Startup impact indicators
- ? Registry-based detection

### ?? **Tab App History** (Placeholder)
- Coming soon!

## ?? Giao di?n

### Layout
```
???????????????????????????????????????????????????????
? [Logo TM+]  Task Manager+                           ?
????????????????????????????????????????????????????
?Processes?Perf?Temp?Startup?App history          ? ? 5 Tabs
??????????????????????????????????????????????????????
?                                                     ?
?  [Icon] Process Name    PID   CPU    Memory  ...   ? ? Processes
?                                                     ?
?  ??????? CPU         ??????????????????????       ?
?  ? 5%  ? RAM         ?  Bi?u ?? chi ti?t  ?       ? ? Performance
?  ????? ? GPU 0       ?  + Detail panel    ?       ?
?  ??????? Disk        ??????????????????????       ?
?                                                     ?
?  ??? CPU: 45°C ?????????????????????????         ? ? Temperature
?  ??? GPU: 55°C ?????????????????????????         ?
?                                                     ?
???????????????????????????????????????????????????????
? [?? Refresh] [Auto refresh] [3 sec] | Status      ?
???????????????????????????????????????????????????????
```

## ?? C?i ti?n hi?u n?ng

### Processes Tab
- Icon extraction v?i caching
- Efficient sorting algorithms
- Double buffering
- Lazy loading

### Performance Tab
- Async data fetching
- Separated hardware monitoring
- Optimized WMI queries
- Performance counter pooling

### Temperature Tab
- Custom GDI+ rendering
- Gradient fills
- Anti-aliased charts
- Minimal resource usage

## ?? Yêu c?u h? th?ng
- .NET Framework 4.7.2+
- Windows 7/8/10/11
- Admin rights (khuy?n ngh? cho full features)
- WMI support
- SMART-capable drives (for temperatures)

## ?? Chi ti?t k? thu?t

### Models
```csharp
- ProcessInfo: Process data + Icon
- HardwareInfo: Base hardware class
- CpuInfo: CPU cores, threads, speeds
- GpuInfo: GPU memory, drivers
- DiskInfo: SSD/HDD, speeds, temps
- NetworkInfo: Adapters, IP, speeds
- StartupApp: Registry startup items
```

### Services
```csharp
- ProcessMonitor:
  - GetAllProcesses() with icons
  - CPU usage tracking
  - Icon caching
  
- HardwareMonitor:
  - GetCpuInfo() - detailed CPU data
  - GetGpuInfo() - multi-GPU support
  - GetDiskInfo() - SSD detection
  - GetNetworkInfo() - real-time speeds
  - Temperature readings (WMI/SMART)
```

### Controls (UserControls)
```csharp
- ProcessesTab: Enhanced with icons + sorting
- PerformanceTab: Sidebar + detail views
- PerformanceSidebar: Mini charts
- TemperatureTab: Temperature monitoring
- StartupTab: Startup management
```

## ?? So sánh Windows Task Manager

| Tính n?ng | Windows TM | TM Plus v3.0 |
|-----------|------------|--------------|
| Process Icons | ? | ? |
| CPU Cores/Threads | ? | ? |
| GPU Monitoring | ? | ? (Multi-GPU) |
| Disk Speed | ? | ? (Read+Write) |
| Network Speed | ? | ? (Per adapter) |
| Temperature | ? | ? (**NEW!**) |
| Startup Manager | ? | ? |
| Custom Icons | ? | ? |
| Sorting | ? | ? (All columns) |
| Color Coding | Partial | ? (Full) |

## ?? Color Scheme

```csharp
// Hardware colors
CPU:     #0D6EFD (Blue)
RAM:     #198754 (Green)  
GPU:     #6C757D (Gray)
Disk:    #20C997 (Teal)
Network: #FFC107 (Yellow)

// Status colors
Cool:     #198754 (Green)  < 50°C
Normal:   #0D6EFD (Blue)   50-70°C
High:     #FFC107 (Orange) 70-80°C
Critical: #DC3545 (Red)    > 80°C

// Text
Dark:  #343A40
Gray:  #6C757D
Light: #F8F9FA
```

## ??? Troubleshooting

### Icons không hi?n th?
- Ch?y v?i quy?n Admin
- M?t s? system processes không có icons

### Temperature hi?n th? "N/A"
- C?n driver h? tr? WMI
- M?t s? hardware không expose temperature
- Có th? c?n OpenHardwareMonitor

### Disk type shows "Unknown"
- WMI query limitations
- Fallback to generic detection

### Network speed = 0
- C?n th?i gian ?? tính delta
- Ch? 1-2 refresh cycles

## ?? Changelog

### v3.0 (Current)
- ? NEW: Temperature monitoring tab
- ? NEW: Process icons with caching
- ? NEW: Full hardware details (cores, threads, speeds)
- ? NEW: Disk type detection (SSD/HDD)
- ? NEW: Network link speed
- ? IMPROVED: Sorting on all columns
- ? IMPROVED: Color-coded CPU usage
- ? IMPROVED: Performance tab details
- ?? FIXED: Lag issues with async operations
- ?? FIXED: Memory leaks

### v2.0
- Giao di?n tabs
- Performance sidebar
- Multi-hardware support
- Async operations

### v1.0
- Basic process listing
- Simple CPU/RAM monitoring

## ?? Roadmap

- [ ] Export data to CSV/Excel
- [ ] Services tab
- [ ] Users tab
- [ ] Details tab (handles, modules)
- [ ] Dark mode
- [ ] OpenHardwareMonitor integration
- [ ] Notification system
- [ ] Process affinity management
- [ ] Custom refresh rates per tab

## ?? Gi?y phép
MIT License

## ????? Contributors
TaskManagerPlus Team - 2024
