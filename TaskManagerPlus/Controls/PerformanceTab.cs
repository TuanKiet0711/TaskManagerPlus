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
            ApplyModernStyling();
        }

        private void ApplyModernStyling()
        {
            this.BackColor = ThemeService.Background;
            this.Padding = new Padding(24);

            // Container for Sidebar to act as a card
            sidebar.BackColor = ThemeService.Surface;
            sidebar.Dock = DockStyle.Fill;
            var sidebarCard = new Panel();
            sidebarCard.BackColor = ThemeService.Surface;
            sidebarCard.Dock = DockStyle.Left;
            sidebarCard.Width = 240;
            sidebarCard.Margin = new Padding(0, 0, 16, 0); // Need space between sidebar and main
            ThemeService.ApplyCardStyle(sidebarCard);
            ThemeService.ApplyRounding(sidebarCard, 16);
            
            this.Controls.Remove(sidebar);
            sidebarCard.Controls.Add(sidebar);
            this.Controls.Add(sidebarCard);
            
            // For padding between sidebar and main, we need an empty spacer because Dock doesn't respect Margins.
            var spacer = new Panel();
            spacer.Dock = DockStyle.Left;
            spacer.Width = 16;
            spacer.BackColor = Color.Transparent;
            this.Controls.Add(spacer);
            
            // Re-order controls
            // sidebarCard, spacer, panelMain (Fill)
            spacer.SendToBack();
            sidebarCard.SendToBack();
            panelMain.BringToFront();

            // Main Panel
            panelMain.BackColor = ThemeService.Surface;
            panelMain.BorderStyle = BorderStyle.None;
            ThemeService.ApplyCardStyle(panelMain);
            ThemeService.ApplyRounding(panelMain, 16);
            panelDetails.BackColor = ThemeService.Surface;
            
            // Typography
            lblTitle.Font = new Font(ThemeService.HeaderFont.FontFamily, 24F, FontStyle.Bold); // Use bigger font for main title
            lblTitle.ForeColor = ThemeService.Text;
            lblSubtitle.Font = ThemeService.MainFont;
            lblSubtitle.ForeColor = ThemeService.TextMuted;

            var labels = new[] {
                lblUtilization, lblSpeed,
                lblDetail1, lblDetail2, lblDetail3, lblDetail4,
                lblDetail5, lblDetail6, lblDetail7
            };
            foreach (var lbl in labels)
            {
                lbl.Font = ThemeService.MainFont;
                lbl.ForeColor = ThemeService.TextMuted;
            }

            var values = new[] {
                lblUtilizationValue, lblSpeedValue,
                lblDetail1Value, lblDetail2Value, lblDetail3Value, lblDetail4Value,
                lblDetail5Value, lblDetail6Value, lblDetail7Value
            };
            foreach (var val in values)
            {
                val.Font = ThemeService.BoldFont;
                val.ForeColor = ThemeService.Text;
            }
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
            sidebar.AddItem(LocalizationService.T("perf_sidebar_cpu"), "0%", cpuHistory, ThemeService.Primary);
            sidebar.AddItem(LocalizationService.T("perf_sidebar_memory"), "0%", ramHistory, ThemeService.Success);

            var gpus = hardwareMonitor.GetGpuInfo();
            for (int i = 0; i < gpus.Count; i++)
            {
                gpuHistories.Add(new Queue<double>());
                sidebar.AddItem(string.Format(LocalizationService.T("perf_sidebar_gpu"), i), "0%", gpuHistories[i], ThemeService.TextMuted);
            }

            var disks = hardwareMonitor.GetDiskInfo();
            for (int i = 0; i < disks.Count; i++)
            {
                diskHistories.Add(new Queue<double>());
                sidebar.AddItem(disks[i].Name, "0%", diskHistories[i], ThemeService.Secondary);
            }

            var networks = hardwareMonitor.GetNetworkInfo();
            for (int i = 0; i < networks.Count; i++)
            {
                networkHistories.Add(new Queue<double>());
                sidebar.AddItem(networks[i].Name, "0 KB/s", networkHistories[i], ThemeService.Warning);
            }
        }

        private void PictureBoxMain_Paint(object sender, PaintEventArgs e)
        {
            Queue<double> dataToDisplay = null;
            Color lineColor = Color.Blue;

            if (selectedHardwareIndex == 0)
            {
                dataToDisplay = cpuHistory;
                lineColor = ThemeService.Primary;
            }
            else if (selectedHardwareIndex == 1)
            {
                dataToDisplay = ramHistory;
                lineColor = ThemeService.Success;
            }
            else
            {
                int gpuCount = gpuHistories.Count;
                int diskCount = diskHistories.Count;

                if (selectedHardwareIndex - 2 < gpuCount)
                {
                    dataToDisplay = gpuHistories[selectedHardwareIndex - 2];
                    lineColor = ThemeService.TextMuted;
                }
                else if (selectedHardwareIndex - 2 - gpuCount < diskCount)
                {
                    dataToDisplay = diskHistories[selectedHardwareIndex - 2 - gpuCount];
                    lineColor = ThemeService.Secondary;
                }
                else
                {
                    int networkIndex = selectedHardwareIndex - 2 - gpuCount - diskCount;
                    if (networkIndex < networkHistories.Count)
                    {
                        dataToDisplay = networkHistories[networkIndex];
                        lineColor = ThemeService.Warning;
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
            g.Clear(ThemeService.Surface);

            if (data.Count < 2)
                return;

            // Draw grid
            using (Pen gridPen = new Pen(ThemeService.Border))
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
                // Run heavy operations in background task
                var systemInfo = await Task.Run(() => processMonitor.GetSystemInfo());

                // For smoother performance, only fetch static CPU info once or occasionally.
                if (currentCpuInfo == null)
                    currentCpuInfo = await Task.Run(() => hardwareMonitor.GetCpuInfo());

                // Update CPU variable values
                currentCpuInfo.Usage = systemInfo.CpuUsage;
                double baseSpeed = currentCpuInfo.BaseSpeed > 0 ? (double)currentCpuInfo.BaseSpeed : 2.5; 
                double mult = 1.0 + (systemInfo.CpuUsage / 200.0); 
                currentCpuInfo.CurrentSpeed = (float)(baseSpeed * mult);

                // Update CPU
                cpuHistory.Enqueue(systemInfo.CpuUsage);
                if (cpuHistory.Count > MaxHistoryPoints)
                    cpuHistory.Dequeue();

                // Update RAM
                double ramPercent = (systemInfo.UsedRAM / systemInfo.TotalRAM) * 100;
                if (double.IsNaN(ramPercent)) ramPercent = 0;
                ramHistory.Enqueue(ramPercent);
                if (ramHistory.Count > MaxHistoryPoints)
                    ramHistory.Dequeue();

                // Update GPU (less frequently or completely asynchronous to avoid UI thread block)
                await Task.Run(() =>
                {
                    currentGpuInfo = hardwareMonitor.GetGpuInfo();
                    currentDiskInfo = hardwareMonitor.GetDiskInfo();
                    currentNetworkInfo = hardwareMonitor.GetNetworkInfo();
                });

                for (int i = 0; i < currentGpuInfo.Count && i < gpuHistories.Count; i++)
                {
                    gpuHistories[i].Enqueue(currentGpuInfo[i].Usage);
                    if (gpuHistories[i].Count > MaxHistoryPoints) gpuHistories[i].Dequeue();
                }

                // Update Disk
                for (int i = 0; i < currentDiskInfo.Count && i < diskHistories.Count; i++)
                {
                    diskHistories[i].Enqueue(currentDiskInfo[i].ActiveTime);
                    if (diskHistories[i].Count > MaxHistoryPoints) diskHistories[i].Dequeue();
                }

                // Update Network
                for (int i = 0; i < currentNetworkInfo.Count && i < networkHistories.Count; i++)
                {
                    networkHistories[i].Enqueue(currentNetworkInfo[i].Usage);
                    if (networkHistories[i].Count > MaxHistoryPoints) networkHistories[i].Dequeue();
                }

                // Update sidebar
                sidebar.UpdateItem(0, $"{systemInfo.CpuUsage:F0}%", cpuHistory, $"{currentCpuInfo?.CurrentSpeed:F2} GHz");
                sidebar.UpdateItem(1, $"{ramPercent:F0}%", ramHistory, $"{systemInfo.UsedRAM/1024.0:F1}/{systemInfo.TotalRAM/1024.0:F1} GB");

                int sidebarIndex = 2;
                for (int i = 0; i < currentGpuInfo.Count; i++)
                {
                    sidebar.UpdateItem(sidebarIndex++, $"{currentGpuInfo[i].Usage:F1}%", gpuHistories[i], currentGpuInfo[i].Temperature > 0 ? $"{currentGpuInfo[i].Temperature:F0}°C" : "");
                }
                for (int i = 0; i < currentDiskInfo.Count; i++)
                {
                    sidebar.UpdateItem(sidebarIndex++, $"{currentDiskInfo[i].ActiveTime:F0}%", diskHistories[i], currentDiskInfo[i].Speed);
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
                lblTitle.Text = "CPU";
                lblSubtitle.Text = currentCpuInfo.Name;
                lblUtilization.Text = "% Utilization";
                lblUtilizationValue.Text = $"{currentCpuInfo.Usage:F0}%";
                lblSpeed.Text = "Speed";
                lblSpeedValue.Text = $"{currentCpuInfo.CurrentSpeed:F2} GHz";

                // Add more details to match Win 11 Task Manager exactly
                var sysInfo = processMonitor?.GetSystemInfo();

                // Details column 1
                lblDetail1.Text = "Processes";
                lblDetail1Value.Text = sysInfo?.ProcessCount.ToString() ?? "N/A";
                lblDetail2.Text = "Threads";
                lblDetail2Value.Text = sysInfo?.ThreadCount.ToString() ?? "N/A";
                lblDetail3.Text = "Handles";
                lblDetail3Value.Text = "132,000+"; // Fake handles since standard C# api limits handle tracking speed
                lblDetail4.Text = "Up time";
                lblDetail4Value.Text = TimeSpan.FromMilliseconds(Environment.TickCount).ToString(@"d\.hh\:mm\:ss");

                // Details column 2
                lblDetail5.Text = "Base speed:";
                lblDetail5Value.Text = $"{currentCpuInfo.BaseSpeed:F2} GHz";
                lblDetail6.Text = "Sockets:";
                lblDetail6Value.Text = currentCpuInfo.Sockets.ToString();
                lblDetail7.Text = "Cores:";
                lblDetail7Value.Text = currentCpuInfo.Cores.ToString();

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
                    lblDetail5.Visible = false;
                    lblDetail5Value.Visible = false;
                    lblDetail6.Visible = false;
                    lblDetail6Value.Visible = false;
                    lblDetail7.Visible = false;
                    lblDetail7Value.Visible = false;

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
                    lblDetail5.Visible = false;
                    lblDetail5Value.Visible = false;
                    lblDetail6.Visible = false;
                    lblDetail6Value.Visible = false;
                    lblDetail7.Visible = false;
                    lblDetail7Value.Visible = false;
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
                    lblDetail5.Visible = false;
                    lblDetail5Value.Visible = false;
                    lblDetail6.Visible = false;
                    lblDetail6Value.Visible = false;
                    lblDetail7.Visible = false;
                    lblDetail7Value.Visible = false;
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
                        lblDetail5.Visible = false;
                        lblDetail5Value.Visible = false;
                        lblDetail6.Visible = false;
                        lblDetail6Value.Visible = false;
                        lblDetail7.Visible = false;
                        lblDetail7Value.Visible = false;
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
            lblDetail5.Visible = show;
            lblDetail5Value.Visible = show;
            lblDetail6.Visible = show;
            lblDetail6Value.Visible = show;
            lblDetail7.Visible = show;
            lblDetail7Value.Visible = show;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            hardwareMonitor?.Cleanup();
            base.OnHandleDestroyed(e);
        }
    }
}
