using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Linq;
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
        
        private ProcessMonitor _procMon;
        private HardwareMonitor _hwMon;
        private Timer _timer;

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

            // Drag support
            this.MouseDown += Widget_MouseDown;
            lblCpu.MouseDown += Widget_MouseDown;
            lblGpu.MouseDown += Widget_MouseDown;
            lblRam.MouseDown += Widget_MouseDown;
            lblDisk.MouseDown += Widget_MouseDown;
            lblNet.MouseDown += Widget_MouseDown;
            lblTemp.MouseDown += Widget_MouseDown;
            
            // Double click to close
            this.DoubleClick += (s, e) => this.Close();
            lblCpu.DoubleClick += (s, e) => this.Close();

            // Setup update timer
            _timer = new Timer();
            _timer.Interval = 1500; // Slightly slower to reduce jitter in readings
            _timer.Tick += Timer_Tick;
            _timer.Start();

            ThemeService.ApplyRounding(this, 10);
            
            this.ResumeLayout(false);
        }

        private void Widget_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (this.IsDisposed) return;

            try
            {
                double cpu = _procMon.GetTotalCpuUsage();
                var cpuInfo = _hwMon.GetCpuInfo();
                var gpuInfo = _hwMon.GetGpuInfo().FirstOrDefault();
                var diskInfo = _hwMon.GetDiskInfo().FirstOrDefault();
                var netInfo = _hwMon.GetNetworkInfo().FirstOrDefault();
                var hwInfo = _procMon.GetSystemInfo();
                double ramBytes = _procMon.GetTotalMemoryUsage();
                double ramPercent = hwInfo.TotalRAM > 0 ? (ramBytes / 1024.0 / 1024.0 / hwInfo.TotalRAM) * 100.0 : 0;
                
                var tempStats = _hwMon.GetTemperatureStats();

                lblCpu.Text = $"CPU : {cpu:F1}% | {(cpuInfo.Temperature > 0 ? cpuInfo.Temperature.ToString("F0") + "°C" : "--°C")}";
                lblGpu.Text = $"GPU : {(gpuInfo != null ? gpuInfo.Usage.ToString("F1") + "%" : "--% ")} | {(gpuInfo != null && gpuInfo.Temperature > 0 ? gpuInfo.Temperature.ToString("F0") + "°C" : "--°C")}";
                lblRam.Text = $"RAM : {ramPercent:F1}%";
                lblDisk.Text = $"DISK: {(diskInfo != null ? diskInfo.ActiveTime.ToString("F1") + "%" : "--% ")} | {(diskInfo != null && diskInfo.Temperature > 0 ? diskInfo.Temperature.ToString("F0") + "°C" : "--°C")}";
                lblNet.Text = $"NET : {(netInfo != null ? netInfo.UsageText : "--")}";
                lblTemp.Text = $"TEMP: {(tempStats.MaxTemp > 0 ? tempStats.MaxTemp.ToString("F0") : "--")}°C";
            }
            catch { }
        }
    }
}

