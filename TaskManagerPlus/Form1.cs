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
        private bool _allowClose = false;
        private bool _isPaused = false;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        public Form1()
        {
            InitializeComponent();

            // 1) Gắn Tag(key) cho các control trên Form1
            SetupLocalizationTags();

            // 2) Load ngôn ngữ mặc định (VI)
            LocalizationService.LoadLanguage(AppLanguage.VI);
            ApplyLanguageAll();

            processMonitor = new ProcessMonitor();
            usageTracker = new AppUsageTracker(processMonitor);

            InitializeTabs();
            SetupIcon();
            InitializeTray();

            usageTracker.StartTracking();
        }

        private void SetupLocalizationTags()
        {
            // Form title + header
            this.Tag = "form_title";
            lblTitle.Tag = "header_title";

            // Menu
            menuLanguage.Tag = "menu_language";
            menuLangVI.Tag = "menu_vi";
            menuLangEN.Tag = "menu_en";

            // Tabs
            tabProcesses.Tag = "tab_processes";
            tabPerformance.Tag = "tab_performance";
            tabTemperature.Tag = "tab_temperature";
            tabBattery.Tag = "tab_battery";
            tabStartup.Tag = "tab_startup";
            tabAppHistory.Tag = "tab_app_history";

            // Bottom
            btnRefresh.Tag = "btn_refresh";
            chkAutoRefresh.Tag = "chk_auto_refresh";
            lblRefreshInterval.Tag = "lbl_refresh_interval";

            // Language toggle button
            btnToggleLanguage.Tag = "btn_toggle_language";
        }

        private void ApplyLanguageAll()
        {
            // Form title
            if (this.Tag is string titleKey)
                this.Text = LocalizationService.T(titleKey);

            // Apply theo Tag cho toàn bộ control tree + menu/toolstrip...
            UILocalizer.Apply(this);

            // lblStatus theo state (không gắn Tag cố định)
            if (!chkAutoRefresh.Checked)
            {
                lblStatus.Text = LocalizationService.T("status_paused");
                lblStatus.ForeColor = Color.FromArgb(108, 117, 125);
            }
            else
            {
                lblStatus.Text = isUpdating ? LocalizationService.T("status_updating")
                                            : LocalizationService.T("status_done");
                lblStatus.ForeColor = Color.FromArgb(25, 135, 84);
            }

            // checked trạng thái menu
            menuLangVI.Checked = (LocalizationService.CurrentLanguage == AppLanguage.VI);
            menuLangEN.Checked = (LocalizationService.CurrentLanguage == AppLanguage.EN);
        }

        private void ApplyLanguageToAllTabs()
        {
            if (processesTab != null) processesTab.ApplyLocalization();
            if (performanceTab != null) performanceTab.ApplyLocalization();
            if (startupTab != null) startupTab.ApplyLocalization();
            if (temperatureTab != null) temperatureTab.ApplyLocalization();
            if (batteryTab != null) batteryTab.ApplyLocalization();
            if (appHistoryTab != null) appHistoryTab.ApplyLocalization();
        }

        // ===== Menu language events =====
        private void menuLangVI_Click(object sender, EventArgs e)
        {
            LocalizationService.LoadLanguage(AppLanguage.VI);
            ApplyLanguageAll();
            ApplyLanguageToAllTabs();
        }

        private void menuLangEN_Click(object sender, EventArgs e)
        {
            LocalizationService.LoadLanguage(AppLanguage.EN);
            ApplyLanguageAll();
            ApplyLanguageToAllTabs();
        }

        private void btnToggleLanguage_Click(object sender, EventArgs e)
        {
            AppLanguage next = (LocalizationService.CurrentLanguage == AppLanguage.VI)
                ? AppLanguage.EN
                : AppLanguage.VI;

            LocalizationService.LoadLanguage(next);
            ApplyLanguageAll();
            ApplyLanguageToAllTabs();
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

                    using (Font font = new Font("Segoe UI", 12f, FontStyle.Bold))
                    using (SolidBrush brush = new SolidBrush(Color.White))
                    using (StringFormat sf = new StringFormat())
                    {
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

        private void InitializeTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open", null, (s, e) => ShowFromTray());
            trayMenu.Items.Add("Exit", null, (s, e) => ExitFromTray());

            trayIcon = new NotifyIcon
            {
                Icon = this.Icon,
                Text = "Task Manager+",
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Activate();
            ResumeUpdates();
        }

        private void ExitFromTray()
        {
            _allowClose = true;
            Close();
        }

        private void PauseUpdates()
        {
            if (_isPaused) return;
            _isPaused = true;

            timerRefresh.Stop();
            temperatureTab?.PauseUpdates();
            batteryTab?.PauseUpdates();
            usageTracker?.StopTracking();
        }

        private void ResumeUpdates()
        {
            if (!_isPaused) return;
            _isPaused = false;

            if (chkAutoRefresh.Checked)
                timerRefresh.Start();
            temperatureTab?.ResumeUpdates();
            batteryTab?.ResumeUpdates();
            usageTracker?.StartTracking();
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

            // Apply language xuống UI tab (nếu các control trong tab có Tag)
            ApplyLanguageToAllTabs();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            processesTab.Initialize();
            performanceTab.Initialize();
            startupTab.Initialize();
            temperatureTab.Initialize();
            batteryTab.Initialize();
            appHistoryTab.Initialize();

            timerRefresh.Start();

            lblStatus.Text = LocalizationService.T("status_loading");
            lblStatus.ForeColor = Color.FromArgb(25, 135, 84);

            _ = PreloadAllTabsAsync();
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

                lblStatus.Text = LocalizationService.T("status_done");
                lblStatus.ForeColor = Color.FromArgb(25, 135, 84);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Preload error: " + ex.Message);
                lblStatus.Text = LocalizationService.T("status_preload_error");
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
                if (chkAutoRefresh.Checked)
                {
                    lblStatus.Text = LocalizationService.T("status_updating");
                    lblStatus.ForeColor = Color.FromArgb(25, 135, 84);
                }

                if (tabControl.SelectedTab == tabProcesses)
                    await processesTab.LoadProcessesAsync();
                else if (tabControl.SelectedTab == tabStartup)
                    await startupTab.LoadStartupAppsAsync();
                else if (tabControl.SelectedTab == tabTemperature)
                    await temperatureTab.UpdateTemperaturesAsync();
                else if (tabControl.SelectedTab == tabBattery)
                    await batteryTab.UpdateBatteryInfoAsync();
                else if (tabControl.SelectedTab == tabAppHistory)
                    await appHistoryTab.LoadAppHistoryAsync();

                // Always update performance tab (lightweight)
                await performanceTab.UpdatePerformanceAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Update error: " + ex.Message);
                lblStatus.Text = LocalizationService.T("status_update_error");
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
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
                lblStatus.Text = LocalizationService.T("status_updating");
                lblStatus.ForeColor = Color.FromArgb(25, 135, 84);
            }
            else
            {
                lblStatus.Text = LocalizationService.T("status_paused");
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

            if (_preloaded) return;

            if (tabControl.SelectedTab == tabProcesses)
                await processesTab.LoadProcessesAsync();
            else if (tabControl.SelectedTab == tabStartup)
                await startupTab.LoadStartupAppsAsync();
            else if (tabControl.SelectedTab == tabTemperature)
                await temperatureTab.UpdateTemperaturesAsync();
            else if (tabControl.SelectedTab == tabBattery)
                await batteryTab.UpdateBatteryInfoAsync();
            else if (tabControl.SelectedTab == tabAppHistory)
                await appHistoryTab.LoadAppHistoryAsync();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                PauseUpdates();
                return;
            }

            timerRefresh.Stop();

            usageTracker?.StopTracking();
            usageTracker?.UpdateDailySummary();

            processMonitor?.Cleanup();
            trayIcon?.Dispose();
            base.OnFormClosing(e);
        }

        private void lblStatus_Click(object sender, EventArgs e) { }
    }
}
