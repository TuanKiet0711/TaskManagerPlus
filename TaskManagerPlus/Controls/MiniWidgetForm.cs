using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Services;

namespace TaskManagerPlus.Controls
{
    public class MiniWidgetForm : Form
    {
        private Label lblCpu;
        private Label lblGpu;
        private Label lblRam;
        private Label lblDisk;
        private Label lblNet;
        private Label lblTemp;
        private Button btnClose;
        
        private ProcessMonitor _procMon;
        private HardwareMonitor _hwMon;
        private Timer _timer;
        private bool _isCompact = false;
        private bool _isUpdating = false;
        private bool _isClosing = false;
        private readonly object _hardwareLock = new object();

        // Make window movable
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public MiniWidgetForm(ProcessMonitor procMon, HardwareMonitor hwMon)
        {
            _procMon = procMon;
            _hwMon = hwMon;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // Basic Form settings for floating Widget
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true; // Always on top
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(20, 20, 20); // Dark sleek background
            this.Size = new Size(290, 170);
            this.Opacity = 0.85; // Semi-transparent
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - this.Width - 20, 20); // Top right corner
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            // Labels
            Font hudFont = new Font("Consolas", 9.5F, FontStyle.Bold);

            lblCpu = new Label { ForeColor = Color.Cyan, Font = hudFont, AutoSize = true, Location = new Point(12, 12), Text = "CPU : --% | --°C" };
            lblGpu = new Label { ForeColor = Color.DeepSkyBlue, Font = hudFont, AutoSize = true, Location = new Point(12, 36), Text = "GPU : --% | --°C" };
            lblRam = new Label { ForeColor = Color.Lime, Font = hudFont, AutoSize = true, Location = new Point(12, 60), Text = "RAM : --%" };
            lblDisk = new Label { ForeColor = Color.Khaki, Font = hudFont, AutoSize = true, Location = new Point(12, 84), Text = "DISK: --% | --°C" };
            lblNet = new Label { ForeColor = Color.LightPink, Font = hudFont, AutoSize = true, Location = new Point(12, 108), Text = "NET : --" };
            lblTemp = new Label { ForeColor = Color.Orange, Font = hudFont, AutoSize = true, Location = new Point(12, 132), Text = "TEMP: --°C" };

            this.Controls.Add(lblCpu);
            this.Controls.Add(lblGpu);
            this.Controls.Add(lblRam);
            this.Controls.Add(lblDisk);
            this.Controls.Add(lblNet);
            this.Controls.Add(lblTemp);

            btnClose = new Button
            {
                Text = "x",
                Size = new Size(22, 22),
                Location = new Point(this.Width - 26, 4),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(36, 36, 36),
                ForeColor = Color.Gainsboro,
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);

            // Drag support
            this.MouseDown += Widget_MouseDown;
            lblCpu.MouseDown += Widget_MouseDown;
            lblGpu.MouseDown += Widget_MouseDown;
            lblRam.MouseDown += Widget_MouseDown;
            lblDisk.MouseDown += Widget_MouseDown;
            lblNet.MouseDown += Widget_MouseDown;
            lblTemp.MouseDown += Widget_MouseDown;
            
            this.DoubleClick += ToggleCompact;
            lblCpu.DoubleClick += ToggleCompact;
            lblRam.DoubleClick += ToggleCompact;
            lblGpu.DoubleClick += ToggleCompact;
            lblDisk.DoubleClick += ToggleCompact;
            lblNet.DoubleClick += ToggleCompact;
            lblTemp.DoubleClick += ToggleCompact;
            btnClose.DoubleClick += (s, e) => this.Close();

            this.KeyDown += MiniWidgetForm_KeyDown;

            var hudMenu = new ContextMenuStrip();
            hudMenu.Items.Add("Close HUD", null, (s, e) => this.Close());
            this.ContextMenuStrip = hudMenu;

            // Setup update timer
            _timer = new Timer();
            _timer.Interval = 2000;
            _timer.Tick += Timer_Tick;
            _timer.Start();

            ThemeService.ApplyRounding(this, 10);
            UpdateLayout();

            this.FormClosed += MiniWidgetForm_FormClosed;
            
            this.ResumeLayout(false);
        }

        private void MiniWidgetForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _isClosing = true;
            try { _timer?.Stop(); } catch { }
            try { _timer?.Dispose(); } catch { }
            // Do not call HardwareMonitor.Cleanup() here to avoid races with in-flight sampling
            // and potential cross-instance instability from the underlying monitor library.
        }

        private void MiniWidgetForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        }

        private void ToggleCompact(object sender, EventArgs e)
        {
            _isCompact = !_isCompact;
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (_isCompact)
            {
                this.Size = new Size(380, 40);
                lblCpu.Location = new Point(10, 10);
                lblGpu.Location = new Point(140, 10);
                lblRam.Location = new Point(270, 10);
                
                // Hide unnecessary labels
                lblDisk.Visible = false;
                lblNet.Visible = false;
                lblTemp.Visible = false;
            }
            else
            {
                this.Size = new Size(290, 170);
                lblCpu.Location = new Point(12, 12);
                lblGpu.Location = new Point(12, 36);
                lblRam.Location = new Point(12, 60);
                lblDisk.Location = new Point(12, 84);
                lblNet.Location = new Point(12, 108);
                lblTemp.Location = new Point(12, 132);

                lblDisk.Visible = true;
                lblNet.Visible = true;
                lblTemp.Visible = true;
            }
            ThemeService.ApplyRounding(this, 10);
        }

        private void Widget_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            if (this.IsDisposed || _isUpdating || _isClosing) return;
            _isUpdating = true;

            try
            {
                HudSnapshot data = await Task.Run(() => CollectSnapshot());
                if (this.IsDisposed || _isClosing || data == null) return;

                if (_isCompact)
                {
                    lblCpu.Text = string.Format("C:{0:F0}%|{1}", data.CpuUsage, data.CpuTempTextShort);
                    lblGpu.Text = string.Format("G:{0}|{1}", data.GpuUsageTextShort, data.GpuTempTextShort);
                    lblRam.Text = string.Format("R:{0:F0}%", data.RamPercent);
                }
                else
                {
                    lblCpu.Text = string.Format("CPU : {0:F1}% | {1}", data.CpuUsage, data.CpuTempText);
                    lblGpu.Text = string.Format("GPU : {0} | {1}", data.GpuUsageText, data.GpuTempText);
                    lblRam.Text = string.Format("RAM : {0:F1}%", data.RamPercent);
                    lblDisk.Text = string.Format("DISK: {0} | {1}", data.DiskUsageText, data.DiskTempText);
                    lblNet.Text = string.Format("NET : {0}", data.NetworkUsageText);
                    lblTemp.Text = string.Format("TEMP: {0}", data.MaxTempText);
                }
            }
            catch { }
            finally
            {
                _isUpdating = false;
            }
        }

        private HudSnapshot CollectSnapshot()
        {
            if (_isClosing)
                return null;

            var snapshot = new HudSnapshot();

            double cpu = _procMon.GetTotalCpuUsage();

            lock (_hardwareLock)
            {
                if (_isClosing || _hwMon == null)
                    return null;

                var cpuInfo = _hwMon.GetCpuInfo();
                var gpuInfo = _hwMon.GetGpuInfo().FirstOrDefault();
                var diskInfo = _hwMon.GetDiskInfo().FirstOrDefault();
                var netInfo = _hwMon.GetNetworkInfo().FirstOrDefault();
                var tempStats = _hwMon.GetTemperatureStats();

                snapshot.CpuTempText = cpuInfo.Temperature > 0 ? cpuInfo.Temperature.ToString("F0") + "°C" : "--°C";
                snapshot.CpuTempTextShort = cpuInfo.Temperature > 0 ? cpuInfo.Temperature.ToString("F0") + "°" : "--°";

                snapshot.GpuUsageText = gpuInfo != null ? gpuInfo.Usage.ToString("F1") + "%" : "--%";
                snapshot.GpuUsageTextShort = gpuInfo != null ? gpuInfo.Usage.ToString("F0") + "%" : "--%";
                snapshot.GpuTempText = (gpuInfo != null && gpuInfo.Temperature > 0) ? gpuInfo.Temperature.ToString("F0") + "°C" : "--°C";
                snapshot.GpuTempTextShort = (gpuInfo != null && gpuInfo.Temperature > 0) ? gpuInfo.Temperature.ToString("F0") + "°" : "--°";

                snapshot.DiskUsageText = diskInfo != null ? diskInfo.ActiveTime.ToString("F1") + "%" : "--%";
                snapshot.DiskTempText = (diskInfo != null && diskInfo.Temperature > 0) ? diskInfo.Temperature.ToString("F0") + "°C" : "--°C";
                snapshot.NetworkUsageText = netInfo != null ? netInfo.UsageText : "--";
                snapshot.MaxTempText = (tempStats.MaxTemp > 0 ? tempStats.MaxTemp.ToString("F0") : "--") + "°C";
            }

            var hwInfo = _procMon.GetSystemInfo();
            double ramBytes = _procMon.GetTotalMemoryUsage();
            double ramPercent = hwInfo.TotalRAM > 0 ? (ramBytes / 1024.0 / 1024.0 / hwInfo.TotalRAM) * 100.0 : 0;

            snapshot.CpuUsage = cpu;
            snapshot.RamPercent = ramPercent;

            return snapshot;
        }

        private class HudSnapshot
        {
            public double CpuUsage { get; set; }
            public double RamPercent { get; set; }
            public string CpuTempText { get; set; }
            public string CpuTempTextShort { get; set; }
            public string GpuUsageText { get; set; }
            public string GpuUsageTextShort { get; set; }
            public string GpuTempText { get; set; }
            public string GpuTempTextShort { get; set; }
            public string DiskUsageText { get; set; }
            public string DiskTempText { get; set; }
            public string NetworkUsageText { get; set; }
            public string MaxTempText { get; set; }
        }
    }
}

