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
        private OverviewTab overviewTab;
        private StartupTab startupTab;
        private TemperatureTab temperatureTab;
        private BatteryTab batteryTab;
        private AppHistoryTab appHistoryTab;
        private AppUsageTracker usageTracker;
        private SmartAlertService smartAlertService;
        private HealthScoreService healthScoreService;
        
        private Controls.MiniWidgetForm miniWidgetForm;

        private bool isUpdating = false;
        private bool _preloaded = false;
        private bool _allowClose = false;
        private bool _isPaused = false;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem trayOpenItem;
        private ToolStripMenuItem trayExitItem;

        public Form1()
        {
            InitializeComponent();

            // 1) Gắn Tag(key) cho các control trên Form1
            SetupLocalizationTags();

            // 2) Load ngôn ngữ mặc định (VI)
            LocalizationService.LoadLanguage(AppLanguage.VI);
            ApplyLanguageAll();

            processMonitor = new ProcessMonitor();
            usageTracker = new AppUsageTracker();

            InitializeTabs();
            SetupIcon();
            InitializeTray();

            HardwareMonitor hwMonitor = new HardwareMonitor();
            smartAlertService = new SmartAlertService(processMonitor, hwMonitor);
            smartAlertService.OnAlertGenerated += SmartAlertService_OnAlertGenerated;
            smartAlertService.Start();

            healthScoreService = new HealthScoreService(processMonitor, hwMonitor);

            usageTracker.StartTracking();
        }

        private void SmartAlertService_OnAlertGenerated(object sender, Models.AlertInfo e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowAlertBanner(e)));
                return;
            }
            ShowAlertBanner(e);
        }

        private Panel pnlAlert;
        private void ShowAlertBanner(Models.AlertInfo alert)
        {
            if (pnlAlert != null)
            {
                this.Controls.Remove(pnlAlert);
                pnlAlert.Dispose();
            }

            pnlAlert = new Panel
            {
                Height = 60,
                Dock = DockStyle.Top,
                BackColor = alert.Severity == Models.AlertSeverity.Critical ? Color.FromArgb(220, 53, 69) : Color.FromArgb(255, 193, 7), // Danger or Warning
                ForeColor = alert.Severity == Models.AlertSeverity.Critical ? Color.White : Color.Black
            };

            Label lblMsg = new Label
            {
                Text = $"{alert.Title}\n{alert.Message}",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            pnlAlert.Controls.Add(lblMsg);

            Button btnAction = new Button
            {
                Text = alert.Action == Models.SuggestedAction.SuspendProcess ? "Suspend App" :
                       alert.Action == Models.SuggestedAction.KillProcess ? "End Task" :
                       alert.Action == Models.SuggestedAction.SetEcoMode ? "Eco Mode" : "Dismiss",
                Dock = DockStyle.Right,
                Width = 120,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAction.FlatAppearance.BorderSize = 0;
            switch(alert.Action)
            {
                case Models.SuggestedAction.SuspendProcess:
                    btnAction.Click += (s, e) => { processMonitor.SuspendProcess(alert.RelatedProcessId); this.Controls.Remove(pnlAlert); }; break;
                case Models.SuggestedAction.KillProcess:
                    btnAction.Click += (s, e) => { processMonitor.KillProcess(alert.RelatedProcessId); this.Controls.Remove(pnlAlert); }; break;
                case Models.SuggestedAction.SetEcoMode:
                    btnAction.Click += (s, e) => { processMonitor.ChangePriority(alert.RelatedProcessId, System.Diagnostics.ProcessPriorityClass.Idle); this.Controls.Remove(pnlAlert); }; break;
                default:
                    btnAction.Click += (s, e) => { this.Controls.Remove(pnlAlert); }; break; // Dismiss
            }
            pnlAlert.Controls.Add(btnAction);

            Button btnDismiss = new Button
            {
                Text = "X",
                Dock = DockStyle.Right,
                Width = 40,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnDismiss.FlatAppearance.BorderSize = 0;
            btnDismiss.Click += (s, e) => this.Controls.Remove(pnlAlert);
            pnlAlert.Controls.Add(btnDismiss);

            this.Controls.Add(pnlAlert);
            pnlAlert.BringToFront();

            if (trayIcon != null && trayIcon.Visible)
            {
                trayIcon.ShowBalloonTip(5000, alert.Title, alert.Message, 
                    alert.Severity == Models.AlertSeverity.Critical ? ToolTipIcon.Error : ToolTipIcon.Warning);
            }
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
            menuHelp.Tag = "menu_help";

            // Tabs
            tabProcesses.Tag = "tab_processes";
            tabPerformance.Tag = "tab_performance";
            tabOverview.Tag = "tab_overview";
            tabTemperature.Tag = "tab_temperature";
            tabBattery.Tag = "tab_battery";
            tabStartup.Tag = "tab_startup";
            tabAppHistory.Tag = "tab_app_history";
            tabDisk.Tag = "tab_disk_cleaner";

            // Bottom
            btnRefresh.Tag = "btn_refresh";
            chkAutoRefresh.Tag = "chk_auto_refresh";
            lblRefreshInterval.Tag = "lbl_refresh_interval";
            btnExportReport.Tag = "btn_export_report";
            btnToggleHUD.Tag = "btn_toggle_hud";
            btnOptimizeRam.Tag = "btn_optimize_ram";

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
            UpdateTrayMenuLanguage();

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
            if (overviewTab != null) overviewTab.ApplyLocalization();
            if (startupTab != null) startupTab.ApplyLocalization();
            if (temperatureTab != null) temperatureTab.ApplyLocalization();
            if (batteryTab != null) batteryTab.ApplyLocalization();
            if (appHistoryTab != null) appHistoryTab.ApplyLocalization();
            if (diskTabControl != null) diskTabControl.ApplyLocalization();
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

        private void menuHelp_Click(object sender, EventArgs e)
        {
            ShowHelpForm();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
            {
                ShowHelpForm();
                e.Handled = true;
            }
        }

        private void ShowHelpForm()
        {
            string langCode = LocalizationService.CurrentLanguage == AppLanguage.VI ? "VI" : "EN";
            string htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help", $"help_{langCode.ToLowerInvariant()}.html");
            string chmPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help", $"TaskManagerPlusHelp_{langCode}.chm");

            if (System.IO.File.Exists(htmlPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(htmlPath)
                {
                    UseShellExecute = true
                });
            }
            else if (System.IO.File.Exists(chmPath))
            {
                Help.ShowHelp(this, chmPath);
            }
            else
            {
                MessageBox.Show($"Help file not found at: {htmlPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            trayOpenItem = new ToolStripMenuItem(LocalizationService.T("tray_open"), null, (s, e) => ShowFromTray());
            trayExitItem = new ToolStripMenuItem(LocalizationService.T("tray_exit"), null, (s, e) => ExitFromTray());
            trayMenu.Items.Add(trayOpenItem);
            trayMenu.Items.Add(trayExitItem);

            trayIcon = new NotifyIcon
            {
                Icon = this.Icon,
                Text = "Task Manager+",
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        private void UpdateTrayMenuLanguage()
        {
            if (trayOpenItem != null) trayOpenItem.Text = LocalizationService.T("tray_open");
            if (trayExitItem != null) trayExitItem.Text = LocalizationService.T("tray_exit");
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

            overviewTab = new OverviewTab();
            overviewTab.Dock = DockStyle.Fill;

            startupTab = new StartupTab();
            startupTab.Dock = DockStyle.Fill;

            temperatureTab = new TemperatureTab();
            temperatureTab.Dock = DockStyle.Fill;

            batteryTab = new BatteryTab(processMonitor);
            batteryTab.Dock = DockStyle.Fill;

            appHistoryTab = new AppHistoryTab();
            appHistoryTab.Dock = DockStyle.Fill;

            tabProcesses.Controls.Add(processesTab);
            tabPerformance.Controls.Add(performanceTab);
            tabOverview.Controls.Add(overviewTab);
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
            overviewTab.Initialize();
            startupTab.Initialize();
            temperatureTab.Initialize();
            batteryTab.Initialize();
            appHistoryTab.Initialize();

            timerRefresh.Start();
            UpdateHealthScore();

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
                Task tHistory = appHistoryTab.LoadHistoryAsync();

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
                    await appHistoryTab.LoadHistoryAsync();

                // Do NOT query history in background loop if we are not on the history tab - it causes WMI/JSON lag!
                // if (chkAutoRefresh.Checked && tabControl.SelectedTab != tabAppHistory)
                //    await appHistoryTab.LoadAppHistoryAsync();

                // Always update performance tab (lightweight)
                await performanceTab.UpdatePerformanceAsync();
                
                UpdateHealthScore();
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

        private void UpdateHealthScore()
        {
            if (healthScoreService == null) return;
            
            int score = healthScoreService.CalculateHealthScore();
            Color c = healthScoreService.GetHealthColor(score);
            string status = healthScoreService.GetHealthStatus(score);

            if (lblHealthScore != null)
            {
                lblHealthScore.Text = $"Health: {score}% ({status})";
                lblHealthScore.ForeColor = c;
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

        private void btnExportReport_Click(object sender, EventArgs e)
        {
            // Pause updates while dialog is open
            bool wasEnabled = timerRefresh.Enabled;
            timerRefresh.Enabled = false;
            
            ReportExporter.ExportToCsv(processMonitor, new HardwareMonitor());
            
            timerRefresh.Enabled = wasEnabled;
        }

        private void btnToggleHUD_Click(object sender, EventArgs e)
        {
             if (miniWidgetForm == null || miniWidgetForm.IsDisposed)
             {
                 HardwareMonitor hw = new HardwareMonitor();
                 miniWidgetForm = new Controls.MiniWidgetForm(processMonitor, hw);
                 miniWidgetForm.Show();
             }
             else
             {
                 miniWidgetForm.Close();
             }
        }

        private void btnOptimizeRam_Click(object sender, EventArgs e)
        {
            btnOptimizeRam.Enabled = false;
            int freed = processMonitor.OptimizeMemory();
            string msg = LocalizationService.CurrentLanguage == AppLanguage.VI 
                ? $"Đã dọn dẹp {freed} MB RAM rác!" 
                : $"Cleaned {freed} MB of unused RAM!";
            MessageBox.Show(msg, "Memory Optimizer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            btnOptimizeRam.Enabled = true;
            UpdateAllDataAsync(); // Refresh info
        }

        private void btnHealthHelp_Click(object sender, EventArgs e)
        {
            using (var dlg = new Controls.HealthInsightsForm(healthScoreService, processMonitor))
            {
                dlg.ShowDialog(this);
            }
        }

        private void numRefreshInterval_ValueChanged(object sender, EventArgs e)
        {
            timerRefresh.Interval = (int)(numRefreshInterval.Value * 1000);
        }

        // ===========================
        // SWITCH TAB
        // ===========================
        private async void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isUpdating) return;

            if (tabControl.SelectedTab == tabProcesses)
                await processesTab.LoadProcessesAsync();
            else if (tabControl.SelectedTab == tabStartup)
                await startupTab.LoadStartupAppsAsync();
            else if (tabControl.SelectedTab == tabTemperature)
                await temperatureTab.UpdateTemperaturesAsync();
            else if (tabControl.SelectedTab == tabBattery)
                await batteryTab.UpdateBatteryInfoAsync();
            else if (tabControl.SelectedTab == tabAppHistory)
                await appHistoryTab.LoadHistoryAsync();
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

            timerRefresh.Stop();

            usageTracker?.StopTracking();
            usageTracker?.UpdateDailySummary();
            
            smartAlertService?.Stop();

            processMonitor?.Cleanup();
            trayIcon?.Dispose();
            base.OnFormClosing(e);
        }

        private void lblStatus_Click(object sender, EventArgs e) { }
    }
}
