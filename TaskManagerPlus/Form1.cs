using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Controls;
using TaskManagerPlus.Services;

namespace TaskManagerPlus
{
    public partial class Form1 : Form
    {
        private ProcessMonitor processMonitor;
        private ProcessesTab processesTab;
        private PerformanceTab performanceTab;
        private StartupTab startupTab;
        private TemperatureTab temperatureTab;
        private BatteryTab batteryTab;
        private AppHistoryTab appHistoryTab;
        private AppUsageTracker usageTracker;

        private bool isUpdating = false;
        private bool _preloaded = false;

        public Form1()
        {
            InitializeComponent();
            processMonitor = new ProcessMonitor();
            usageTracker = new AppUsageTracker(processMonitor);

            InitializeTabs();
            SetupIcon();

            // Start tracking app usage in background
            usageTracker.StartTracking();
        }

        private void SetupIcon()
        {
            try
            {
                Bitmap iconBitmap = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(iconBitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(13, 110, 253));

                    using (Font font = new Font("Segoe UI", 12F, FontStyle.Bold))
                    using (SolidBrush brush = new SolidBrush(Color.White))
                    {
                        StringFormat sf = new StringFormat();
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("TM+", font, brush, new RectangleF(0, 0, 32, 32), sf);
                    }
                }

                IntPtr hIcon = iconBitmap.GetHicon();
                this.Icon = Icon.FromHandle(hIcon);
                iconBitmap.Dispose();
            }
            catch { }
        }

        private void InitializeTabs()
        {
            processesTab = new ProcessesTab();
            processesTab.ProcessMonitor = processMonitor;
            processesTab.Dock = DockStyle.Fill;

            performanceTab = new PerformanceTab();
            performanceTab.ProcessMonitor = processMonitor;
            performanceTab.Dock = DockStyle.Fill;

            startupTab = new StartupTab();
            startupTab.Dock = DockStyle.Fill;

            temperatureTab = new TemperatureTab();
            temperatureTab.Dock = DockStyle.Fill;

            batteryTab = new BatteryTab();
            batteryTab.Dock = DockStyle.Fill;

            appHistoryTab = new AppHistoryTab();
            appHistoryTab.Dock = DockStyle.Fill;

            tabProcesses.Controls.Add(processesTab);
            tabPerformance.Controls.Add(performanceTab);
            tabStartup.Controls.Add(startupTab);
            tabTemperature.Controls.Add(temperatureTab);
            tabBattery.Controls.Add(batteryTab);
            tabAppHistory.Controls.Add(appHistoryTab);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Initialize all tabs first
            processesTab.Initialize();
            performanceTab.Initialize();
            startupTab.Initialize();
            temperatureTab.Initialize();
            batteryTab.Initialize();
            appHistoryTab.Initialize();

            // Start timer for auto-refresh
            timerRefresh.Start();

            // Preload all data (NON-BLOCKING)
            lblStatus.Text = "Đang tải dữ liệu...";
            lblStatus.ForeColor = Color.FromArgb(25, 135, 84);

            _ = PreloadAllTabsAsync(); // chạy nền, không block UI
        }

        // ===========================
        // PRELOAD ALL DATA (parallel)
        // ===========================
        private async Task PreloadAllTabsAsync()
        {
            if (isUpdating) return;

            isUpdating = true;
            try
            {
                Task tProc = processesTab.LoadProcessesAsync();
                Task tPerf = performanceTab.UpdatePerformanceAsync();

                Task tStartup = startupTab.LoadStartupAppsAsync();
                Task tTemp = temperatureTab.UpdateTemperaturesAsync();
                Task tBattery = batteryTab.UpdateBatteryInfoAsync();
                Task tHistory = appHistoryTab.LoadAppHistoryAsync();

                await Task.WhenAll(tProc, tPerf, tStartup, tTemp, tBattery, tHistory);

                _preloaded = true;

                lblStatus.Text = "Đã tải xong";
                lblStatus.ForeColor = Color.FromArgb(25, 135, 84);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Preload error: " + ex.Message);
                lblStatus.Text = "Lỗi preload";
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
            }
            finally
            {
                isUpdating = false;
            }
        }

        // ===========================
        // MANUAL/TIMER REFRESH
        // ===========================
        private async Task UpdateAllDataAsync()
        {
            if (isUpdating) return;

            isUpdating = true;
            try
            {
                // Update current tab
                if (tabControl.SelectedTab == tabProcesses)
                {
                    await processesTab.LoadProcessesAsync();
                }
                else if (tabControl.SelectedTab == tabStartup)
                {
                    await startupTab.LoadStartupAppsAsync();
                }
                else if (tabControl.SelectedTab == tabTemperature)
                {
                    await temperatureTab.UpdateTemperaturesAsync();
                }
                else if (tabControl.SelectedTab == tabBattery)
                {
                    await batteryTab.UpdateBatteryInfoAsync();
                }
                else if (tabControl.SelectedTab == tabAppHistory)
                {
                    await appHistoryTab.LoadAppHistoryAsync();
                }

                // Always update performance tab (lightweight)
                await performanceTab.UpdatePerformanceAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Update error: " + ex.Message);
            }
            finally
            {
                isUpdating = false;
            }
        }

        private async void timerRefresh_Tick(object sender, EventArgs e)
        {
            if (chkAutoRefresh.Checked && !isUpdating)
            {
                await UpdateAllDataAsync();
            }
        }

        private void chkAutoRefresh_CheckedChanged(object sender, EventArgs e)
        {
            timerRefresh.Enabled = chkAutoRefresh.Checked;
            if (chkAutoRefresh.Checked)
            {
                lblStatus.Text = "Đang cập nhật...";
                lblStatus.ForeColor = Color.FromArgb(25, 135, 84);
            }
            else
            {
                lblStatus.Text = "Đã tạm dừng";
                lblStatus.ForeColor = Color.FromArgb(108, 117, 125);
            }
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            btnRefresh.Enabled = false;
            await UpdateAllDataAsync();
            btnRefresh.Enabled = true;
        }

        private void numRefreshInterval_ValueChanged(object sender, EventArgs e)
        {
            timerRefresh.Interval = (int)numRefreshInterval.Value * 1000;
        }

        // ===========================
        // SWITCH TAB
        // ===========================
        private async void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isUpdating) return;

            // đã preload xong thì không load lại khi switch tab => chuyển tab là tức thì
            if (_preloaded) return;

            // nếu chưa preload xong thì load tab đang chọn (để user vẫn dùng được)
            if (tabControl.SelectedTab == tabProcesses)
            {
                await processesTab.LoadProcessesAsync();
            }
            else if (tabControl.SelectedTab == tabStartup)
            {
                await startupTab.LoadStartupAppsAsync();
            }
            else if (tabControl.SelectedTab == tabTemperature)
            {
                await temperatureTab.UpdateTemperaturesAsync();
            }
            else if (tabControl.SelectedTab == tabBattery)
            {
                await batteryTab.UpdateBatteryInfoAsync();
            }
            else if (tabControl.SelectedTab == tabAppHistory)
            {
                await appHistoryTab.LoadAppHistoryAsync();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timerRefresh.Stop();

            usageTracker?.StopTracking();
            usageTracker?.UpdateDailySummary();

            processMonitor?.Cleanup();
            base.OnFormClosing(e);
        }

        private void lblStatus_Click(object sender, EventArgs e)
        {
        }
    }
}
