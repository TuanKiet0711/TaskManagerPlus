using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Services;
using TaskManagerPlus.Models;

namespace TaskManagerPlus.Controls
{
    public partial class PerformanceTab : UserControl
    {
        private ProcessMonitor processMonitor;
        private HardwareMonitor hardwareMonitor;
        private Queue<double> cpuHistory;
        private Queue<double> ramHistory;
        private List<Queue<double>> gpuHistories;
        private List<Queue<double>> diskHistories;
        private List<Queue<double>> networkHistories;
        private const int MaxHistoryPoints = 60;
        private int selectedHardwareIndex = 0;
        private CpuInfo currentCpuInfo;
        private List<GpuInfo> currentGpuInfo;
        private List<DiskInfo> currentDiskInfo;
        private List<NetworkInfo> currentNetworkInfo;

        public ProcessMonitor ProcessMonitor
        {
            get { return processMonitor; }
            set { processMonitor = value; }
        }

        public PerformanceTab()
        {
            InitializeComponent();
            hardwareMonitor = new HardwareMonitor();
            cpuHistory = new Queue<double>();
            ramHistory = new Queue<double>();
            gpuHistories = new List<Queue<double>>();
            diskHistories = new List<Queue<double>>();
            networkHistories = new List<Queue<double>>();
            SetupLocalizationTags();
        }

        public void Initialize()
        {
            pictureBoxMain.Paint += PictureBoxMain_Paint;
            sidebar.ItemSelected += Sidebar_ItemSelected;
            LoadHardwareItems();
        }

        private void Sidebar_ItemSelected(object sender, int index)
        {
            selectedHardwareIndex = index;
            pictureBoxMain.Invalidate();
            UpdateDetailView();
        }

        private void LoadHardwareItems()
        {
            sidebar.ClearItems();
            sidebar.AddItem(LocalizationService.T("perf_sidebar_cpu"), "0%", cpuHistory, Color.FromArgb(13, 110, 253));
            sidebar.AddItem(LocalizationService.T("perf_sidebar_memory"), "0%", ramHistory, Color.FromArgb(25, 135, 84));

            var gpus = hardwareMonitor.GetGpuInfo();
            for (int i = 0; i < gpus.Count; i++)
            {
                gpuHistories.Add(new Queue<double>());
                sidebar.AddItem(string.Format(LocalizationService.T("perf_sidebar_gpu"), i), "0%", gpuHistories[i], Color.FromArgb(108, 117, 125));
            }

            var disks = hardwareMonitor.GetDiskInfo();
            for (int i = 0; i < disks.Count; i++)
            {
                diskHistories.Add(new Queue<double>());
                sidebar.AddItem(disks[i].Name, "0%", diskHistories[i], Color.FromArgb(32, 201, 151));
            }

            var networks = hardwareMonitor.GetNetworkInfo();
            for (int i = 0; i < networks.Count; i++)
            {
                networkHistories.Add(new Queue<double>());
                sidebar.AddItem(networks[i].Name, "0 KB/s", networkHistories[i], Color.FromArgb(255, 193, 7));
            }
        }

        private void PictureBoxMain_Paint(object sender, PaintEventArgs e)
        {
            Queue<double> dataToDisplay = null;
            Color lineColor = Color.Blue;

            if (selectedHardwareIndex == 0)
            {
                dataToDisplay = cpuHistory;
                lineColor = Color.FromArgb(13, 110, 253);
            }
            else if (selectedHardwareIndex == 1)
            {
                dataToDisplay = ramHistory;
                lineColor = Color.FromArgb(25, 135, 84);
            }
            else
            {
                int gpuCount = gpuHistories.Count;
                int diskCount = diskHistories.Count;

                if (selectedHardwareIndex - 2 < gpuCount)
                {
                    dataToDisplay = gpuHistories[selectedHardwareIndex - 2];
                    lineColor = Color.FromArgb(108, 117, 125);
                }
                else if (selectedHardwareIndex - 2 - gpuCount < diskCount)
                {
                    dataToDisplay = diskHistories[selectedHardwareIndex - 2 - gpuCount];
                    lineColor = Color.FromArgb(32, 201, 151);
                }
                else
                {
                    int networkIndex = selectedHardwareIndex - 2 - gpuCount - diskCount;
                    if (networkIndex < networkHistories.Count)
                    {
                        dataToDisplay = networkHistories[networkIndex];
                        lineColor = Color.FromArgb(255, 193, 7);
                    }
                }
            }

            if (dataToDisplay != null)
            {
                DrawChart(e.Graphics, dataToDisplay, lineColor, pictureBoxMain.Width, pictureBoxMain.Height);
            }
        }

        private void DrawChart(Graphics g, Queue<double> data, Color lineColor, int width, int height)
        {
            g.Clear(Color.White);

            if (data.Count < 2)
                return;

            // Draw grid
            using (Pen gridPen = new Pen(Color.FromArgb(230, 230, 230)))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = (int)(height * i / 4.0);
                    g.DrawLine(gridPen, 0, y, width, y);
                }

                for (int i = 0; i <= 6; i++)
                {
                    int x = (int)(width * i / 6.0);
                    g.DrawLine(gridPen, x, 0, x, height);
                }
            }

            // Draw data line
            var points = new List<PointF>();
            var dataArray = data.ToArray();

            for (int i = 0; i < dataArray.Length; i++)
            {
                float x = (float)(width * i / (MaxHistoryPoints - 1.0));
                float y = (float)(height - (height * dataArray[i] / 100.0));
                points.Add(new PointF(x, y));
            }

            if (points.Count > 1)
            {
                // Draw gradient fill
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLines(points.ToArray());
                    path.AddLine(points[points.Count - 1].X, points[points.Count - 1].Y, points[points.Count - 1].X, height);
                    path.AddLine(points[points.Count - 1].X, height, points[0].X, height);
                    path.CloseFigure();

                    Color fillColor = Color.FromArgb(50, lineColor);
                    using (SolidBrush brush = new SolidBrush(fillColor))
                    {
                        g.FillPath(brush, path);
                    }
                }

                // Draw line
                using (Pen linePen = new Pen(lineColor, 2))
                {
                    linePen.LineJoin = LineJoin.Round;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawLines(linePen, points.ToArray());
                }
            }
        }

        public async Task UpdatePerformanceAsync()
        {
            if (processMonitor == null) return;

            try
            {
                var systemInfo = await Task.Run(() => processMonitor.GetSystemInfo());
                currentCpuInfo = await Task.Run(() => hardwareMonitor.GetCpuInfo());

                // Update CPU
                cpuHistory.Enqueue(systemInfo.CpuUsage);
                if (cpuHistory.Count > MaxHistoryPoints)
                    cpuHistory.Dequeue();

                currentCpuInfo.Usage = systemInfo.CpuUsage;

                // Update RAM
                double ramPercent = (systemInfo.UsedRAM / systemInfo.TotalRAM) * 100;
                ramHistory.Enqueue(ramPercent);
                if (ramHistory.Count > MaxHistoryPoints)
                    ramHistory.Dequeue();

                // Update GPU
                currentGpuInfo = hardwareMonitor.GetGpuInfo();
                for (int i = 0; i < currentGpuInfo.Count && i < gpuHistories.Count; i++)
                {
                    gpuHistories[i].Enqueue(currentGpuInfo[i].Usage);
                    if (gpuHistories[i].Count > MaxHistoryPoints)
                        gpuHistories[i].Dequeue();
                }

                // Update Disk
                currentDiskInfo = hardwareMonitor.GetDiskInfo();
                for (int i = 0; i < currentDiskInfo.Count && i < diskHistories.Count; i++)
                {
                    diskHistories[i].Enqueue(currentDiskInfo[i].ActiveTime);
                    if (diskHistories[i].Count > MaxHistoryPoints)
                        diskHistories[i].Dequeue();
                }

                // Update Network
                currentNetworkInfo = hardwareMonitor.GetNetworkInfo();
                for (int i = 0; i < currentNetworkInfo.Count && i < networkHistories.Count; i++)
                {
                    networkHistories[i].Enqueue(currentNetworkInfo[i].Usage);
                    if (networkHistories[i].Count > MaxHistoryPoints)
                        networkHistories[i].Dequeue();
                }

                // Update sidebar
                sidebar.UpdateItem(0, $"{systemInfo.CpuUsage:F1}%", cpuHistory);
                sidebar.UpdateItem(1, $"{ramPercent:F1}%", ramHistory);

                int sidebarIndex = 2;
                for (int i = 0; i < currentGpuInfo.Count; i++)
                {
                    sidebar.UpdateItem(sidebarIndex++, $"{currentGpuInfo[i].Usage:F1}%", gpuHistories[i]);
                }
                for (int i = 0; i < currentDiskInfo.Count; i++)
                {
                    sidebar.UpdateItem(sidebarIndex++, $"{currentDiskInfo[i].ActiveTime:F0}%", diskHistories[i]);
                }
                for (int i = 0; i < currentNetworkInfo.Count; i++)
                {
                    sidebar.UpdateItem(sidebarIndex++, currentNetworkInfo[i].UsageText, networkHistories[i]);
                }

                pictureBoxMain.Invalidate();
                UpdateDetailView();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"L?i c?p nh?t hi?u n?ng: {ex.Message}");
            }
        }

        private void UpdateDetailView()
        {
            if (selectedHardwareIndex == 0 && currentCpuInfo != null)
            {
                // CPU selected
                lblTitle.Text = LocalizationService.T("perf_title_cpu");
                lblSubtitle.Text = currentCpuInfo.Name;
                lblUtilization.Text = LocalizationService.T("perf_utilization");
                lblUtilizationValue.Text = $"{currentCpuInfo.Usage:F1}%";
                lblSpeed.Text = LocalizationService.T("perf_speed");
                lblSpeedValue.Text = $"{currentCpuInfo.CurrentSpeed:F2} GHz";
                
                // Add more details
                lblDetail1.Text = LocalizationService.T("perf_base_speed");
                lblDetail1Value.Text = $"{currentCpuInfo.BaseSpeed:F2} GHz";
                lblDetail2.Text = LocalizationService.T("perf_cores");
                lblDetail2Value.Text = currentCpuInfo.Cores.ToString();
                lblDetail3.Text = LocalizationService.T("perf_logical_processors");
                lblDetail3Value.Text = currentCpuInfo.LogicalProcessors.ToString();
                lblDetail4.Text = LocalizationService.T("perf_temperature");
                lblDetail4Value.Text = currentCpuInfo.Temperature > 0
                    ? $"{currentCpuInfo.Temperature:F1} °C"
                    : LocalizationService.T("common_na");

                ShowDetailLabels(true);
            }
            else if (selectedHardwareIndex == 1)
            {
                // Memory selected
                var systemInfo = processMonitor?.GetSystemInfo();
                if (systemInfo != null)
                {
                    lblTitle.Text = LocalizationService.T("perf_title_memory");
                    lblSubtitle.Text = string.Format(LocalizationService.T("perf_memory_total_format"), systemInfo.TotalRAM);
                    lblUtilization.Text = LocalizationService.T("perf_in_use");
                    lblUtilizationValue.Text = $"{systemInfo.UsedRAM:F0} MB";
                    lblSpeed.Text = LocalizationService.T("perf_available");
                    lblSpeedValue.Text = $"{systemInfo.AvailableRAM:F0} MB";

                    double ramPercent = (systemInfo.UsedRAM / systemInfo.TotalRAM) * 100;
                    lblDetail1.Text = LocalizationService.T("perf_usage");
                    lblDetail1Value.Text = $"{ramPercent:F1}%";
                    lblDetail2.Text = LocalizationService.T("perf_committed");
                    lblDetail2Value.Text = $"{systemInfo.UsedRAM:F0} MB";
                    lblDetail3.Text = LocalizationService.T("perf_cached");
                    lblDetail3Value.Text = LocalizationService.T("common_na");
                    lblDetail4.Text = LocalizationService.T("perf_paged_pool");
                    lblDetail4Value.Text = LocalizationService.T("common_na");

                    ShowDetailLabels(true);
                }
            }
            else
            {
                int gpuCount = gpuHistories.Count;
                int diskCount = diskHistories.Count;

                if (selectedHardwareIndex - 2 < gpuCount && currentGpuInfo != null)
                {
                    // GPU selected
                    var gpu = currentGpuInfo[selectedHardwareIndex - 2];
                    lblTitle.Text = string.Format(LocalizationService.T("perf_title_gpu"), selectedHardwareIndex - 2);
                    lblSubtitle.Text = gpu.Name;
                    lblUtilization.Text = LocalizationService.T("perf_utilization");
                    lblUtilizationValue.Text = $"{gpu.Usage:F1}%";
                    lblSpeed.Text = LocalizationService.T("perf_dedicated_memory");
                    lblSpeedValue.Text = gpu.DedicatedMemory;

                    lblDetail1.Text = LocalizationService.T("perf_temperature");
                    lblDetail1Value.Text = gpu.Temperature > 0
                        ? $"{gpu.Temperature:F1} °C"
                        : LocalizationService.T("common_na");
                    lblDetail2.Text = LocalizationService.T("perf_driver");
                    lblDetail2Value.Text = gpu.Details ?? LocalizationService.T("common_unknown");
                    lblDetail3.Visible = false;
                    lblDetail3Value.Visible = false;
                    lblDetail4.Visible = false;
                    lblDetail4Value.Visible = false;
                }
                else if (selectedHardwareIndex - 2 - gpuCount < diskCount && currentDiskInfo != null)
                {
                    // Disk selected
                    var disk = currentDiskInfo[selectedHardwareIndex - 2 - gpuCount];
                    lblTitle.Text = disk.Name;
                    lblSubtitle.Text = $"{disk.Type} - {disk.Capacity}";
                    lblUtilization.Text = LocalizationService.T("perf_active_time");
                    lblUtilizationValue.Text = $"{disk.ActiveTime:F1}%";
                    lblSpeed.Text = LocalizationService.T("perf_read_write");
                    lblSpeedValue.Text = disk.Speed;

                    lblDetail1.Text = LocalizationService.T("perf_capacity");
                    lblDetail1Value.Text = disk.Capacity;
                    lblDetail2.Text = LocalizationService.T("perf_used");
                    lblDetail2Value.Text = disk.Details;
                    lblDetail3.Text = LocalizationService.T("perf_temperature");
                    lblDetail3Value.Text = disk.Temperature > 0
                        ? $"{disk.Temperature:F1} °C"
                        : LocalizationService.T("common_na");
                    lblDetail4.Visible = false;
                    lblDetail4Value.Visible = false;
                }
                else
                {
                    int networkIndex = selectedHardwareIndex - 2 - gpuCount - diskCount;
                    if (networkIndex < networkHistories.Count && currentNetworkInfo != null && networkIndex < currentNetworkInfo.Count)
                    {
                        // Network selected
                        var network = currentNetworkInfo[networkIndex];
                        lblTitle.Text = network.Name;
                        lblSubtitle.Text = network.AdapterName;
                        lblUtilization.Text = LocalizationService.T("perf_send");
                        lblUtilizationValue.Text = $"{network.SendSpeed:F0} KB/s";
                        lblSpeed.Text = LocalizationService.T("perf_receive");
                        lblSpeedValue.Text = $"{network.ReceiveSpeed:F0} KB/s";

                        lblDetail1.Text = LocalizationService.T("perf_link_speed");
                        lblDetail1Value.Text = network.Speed ?? LocalizationService.T("common_unknown");
                        lblDetail2.Text = LocalizationService.T("perf_ip_address");
                        lblDetail2Value.Text = network.IpAddress;
                        lblDetail3.Text = LocalizationService.T("perf_connection_type");
                        lblDetail3Value.Text = network.ConnectionType;
                        lblDetail4.Visible = false;
                        lblDetail4Value.Visible = false;
                    }
                }
            }
        }

        public void ApplyLocalization()
        {
            UILocalizer.Apply(this);
            UpdateSidebarLabels();
            UpdateDetailView();
        }

        private void UpdateSidebarLabels()
        {
            sidebar.UpdateItemName(0, LocalizationService.T("perf_sidebar_cpu"));
            sidebar.UpdateItemName(1, LocalizationService.T("perf_sidebar_memory"));

            for (int i = 0; i < gpuHistories.Count; i++)
            {
                sidebar.UpdateItemName(2 + i, string.Format(LocalizationService.T("perf_sidebar_gpu"), i));
            }
        }

        private void SetupLocalizationTags()
        {
            // Dynamic labels are localized in UpdateDetailView.
        }

        private void ShowDetailLabels(bool show)
        {
            lblDetail1.Visible = show;
            lblDetail1Value.Visible = show;
            lblDetail2.Visible = show;
            lblDetail2Value.Visible = show;
            lblDetail3.Visible = show;
            lblDetail3Value.Visible = show;
            lblDetail4.Visible = show;
            lblDetail4Value.Visible = show;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            hardwareMonitor?.Cleanup();
            base.OnHandleDestroyed(e);
        }
    }
}
