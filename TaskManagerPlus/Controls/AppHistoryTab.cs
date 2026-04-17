using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Models;
using TaskManagerPlus.Services;

namespace TaskManagerPlus.Controls
{
    public partial class AppHistoryTab : UserControl
    {
        private AppUsageDatabase database;
        private BindingList<AppHistoryItem> historyItems;
        private DateTime selectedStartDate;
        private DateTime selectedEndDate;
        private int loadSequence;
        private readonly Timer dateChangeTimer;

        public AppHistoryTab()
        {
            InitializeComponent();
            database = new AppUsageDatabase();
            historyItems = new BindingList<AppHistoryItem>();
            selectedStartDate = DateTime.Today.AddDays(-7);
            selectedEndDate = DateTime.Today;
            dateChangeTimer = new Timer { Interval = 250 };
            dateChangeTimer.Tick += DateChangeTimer_Tick;
            SetupLocalizationTags();
        }

        public void Initialize()
        {
            SetupDataGridView();
            SetupDatePickers();
            ApplyLocalization();
        }

        private void SetupDatePickers()
        {
            dateTimePickerStart.Value = selectedStartDate;
            dateTimePickerEnd.Value = selectedEndDate;
            
            dateTimePickerStart.ValueChanged += DatePicker_ValueChanged;
            dateTimePickerEnd.ValueChanged += DatePicker_ValueChanged;
            dateTimePickerStart.CloseUp += DatePicker_CloseUp;
            dateTimePickerEnd.CloseUp += DatePicker_CloseUp;
        }

        private void SetupDataGridView()
        {
            dataGridViewHistory.AutoGenerateColumns = false;
            dataGridViewHistory.Columns.Clear();
            dataGridViewHistory.DoubleBuffered(true);
            dataGridViewHistory.RowTemplate.Height = 32;

            // Make it match Processes tab UI style (Win 11)
            dataGridViewHistory.BackgroundColor = Color.White;
            dataGridViewHistory.BorderStyle = BorderStyle.None;
            dataGridViewHistory.EnableHeadersVisualStyles = false;
            dataGridViewHistory.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewHistory.GridColor = Color.FromArgb(240, 240, 240);
            dataGridViewHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewHistory.AllowUserToResizeRows = false;

            DataGridViewCellStyle headerStyle = new DataGridViewCellStyle();
            headerStyle.BackColor = Color.White;
            headerStyle.ForeColor = Color.FromArgb(60, 60, 60);
            headerStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            headerStyle.SelectionBackColor = Color.White;
            headerStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            headerStyle.Padding = new Padding(5, 5, 5, 5);
            dataGridViewHistory.ColumnHeadersDefaultCellStyle = headerStyle;
            dataGridViewHistory.ColumnHeadersHeight = 35;
            dataGridViewHistory.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            DataGridViewCellStyle cellStyle = new DataGridViewCellStyle();
            cellStyle.BackColor = Color.White;
            cellStyle.ForeColor = Color.FromArgb(30, 30, 30);
            cellStyle.SelectionBackColor = Color.FromArgb(230, 242, 255);
            cellStyle.SelectionForeColor = Color.Black;
            cellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            cellStyle.Padding = new Padding(2);
            dataGridViewHistory.DefaultCellStyle = cellStyle;

            dataGridViewHistory.Columns.Add(new DataGridViewImageColumn
            {
                Name = "Icon",
                DataPropertyName = "Icon",
                HeaderText = "",
                Width = 32,
                MinimumWidth = 32,
                FillWeight = 5,
                ImageLayout = DataGridViewImageCellLayout.Zoom,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, NullValue = null }
            });

            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProcessName",
                Tag = "app_history_col_application",
                HeaderText = LocalizationService.T("app_history_col_application"),
                FillWeight = 25
            });

            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "FormattedDuration",
                Tag = "app_history_col_total_runtime",
                HeaderText = LocalizationService.T("app_history_col_total_runtime"),
                FillWeight = 20,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "AverageCpu",
                Tag = "app_history_col_avg_cpu",
                HeaderText = LocalizationService.T("app_history_col_avg_cpu"),
                FillWeight = 10,
                DefaultCellStyle = new DataGridViewCellStyle 
                { 
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "F1"
                }
            });

            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "FormattedMemory",
                Tag = "app_history_col_avg_memory",
                HeaderText = LocalizationService.T("app_history_col_avg_memory"),
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "LaunchCount",
                Tag = "app_history_col_launch_count",
                HeaderText = LocalizationService.T("app_history_col_launch_count"),
                FillWeight = 10,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "IsRunning",
                Tag = "app_history_col_status",
                HeaderText = LocalizationService.T("app_history_col_status"),
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dataGridViewHistory.DataSource = historyItems;
            dataGridViewHistory.CellFormatting += DataGridViewHistory_CellFormatting;
            dataGridViewHistory.ColumnHeaderMouseClick += DataGridViewHistory_ColumnHeaderMouseClick;
            dataGridViewHistory.CellClick += DataGridViewHistory_CellClick;
            dataGridViewHistory.CellDoubleClick += DataGridViewHistory_CellDoubleClick;
        }

        private void DataGridViewHistory_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < historyItems.Count)
                ShowAppDetails(historyItems[e.RowIndex]);
        }

        private void DataGridViewHistory_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < historyItems.Count)
                ShowAppDetails(historyItems[e.RowIndex]);
        }

        private void ShowAppDetails(AppHistoryItem item)
        {
            var sessions = database.GetSessionsForApp(item.ProcessName, selectedStartDate, selectedEndDate);
            using (var form = new AppDetailForm(item, sessions))
                form.ShowDialog(this.FindForm());
        }

        private string currentSortColumn = "FormattedDuration";
        private bool sortAscending = false;

        private void DataGridViewHistory_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            string columnName = dataGridViewHistory.Columns[e.ColumnIndex].DataPropertyName;

            if (currentSortColumn == columnName)
                sortAscending = !sortAscending;
            else
            {
                currentSortColumn = columnName;
                sortAscending = false;
            }

            foreach (DataGridViewColumn col in dataGridViewHistory.Columns)
                col.HeaderCell.SortGlyphDirection = SortOrder.None;

            dataGridViewHistory.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection =
                sortAscending ? SortOrder.Ascending : SortOrder.Descending;

            SortHistoryList();
        }

        private void SortHistoryList()
        {
            if (historyItems == null || historyItems.Count == 0) return;

            var list = historyItems.ToList();
            IEnumerable<AppHistoryItem> sorted = list;

            if (currentSortColumn == "ProcessName")
                sorted = sortAscending ? list.OrderBy(x => x.ProcessName) : list.OrderByDescending(x => x.ProcessName);
            else if (currentSortColumn == "FormattedDuration")
                sorted = sortAscending ? list.OrderBy(x => x.TotalDuration) : list.OrderByDescending(x => x.TotalDuration);
            else if (currentSortColumn == "AverageCpu")
                sorted = sortAscending ? list.OrderBy(x => x.AverageCpu) : list.OrderByDescending(x => x.AverageCpu);
            else if (currentSortColumn == "FormattedMemory")
                sorted = sortAscending ? list.OrderBy(x => x.AverageMemory) : list.OrderByDescending(x => x.AverageMemory);
            else if (currentSortColumn == "LaunchCount")
                sorted = sortAscending ? list.OrderBy(x => x.LaunchCount) : list.OrderByDescending(x => x.LaunchCount);
            else if (currentSortColumn == "IsRunning")
                sorted = sortAscending ? list.OrderBy(x => x.IsRunning) : list.OrderByDescending(x => x.IsRunning);

            var newItems = new BindingList<AppHistoryItem>();
            foreach (var item in sorted) newItems.Add(item);

            historyItems = newItems;
            dataGridViewHistory.DataSource = historyItems;
        }

        private void DataGridViewHistory_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dataGridViewHistory.Columns[e.ColumnIndex].DataPropertyName == "AverageCpu" && e.RowIndex >= 0)
            {
                if (e.Value != null && double.TryParse(e.Value.ToString(), out double cpuValue))
                {
                    if (cpuValue > 50)
                        e.CellStyle.ForeColor = Color.FromArgb(220, 53, 69);
                    else if (cpuValue > 25)
                        e.CellStyle.ForeColor = Color.FromArgb(255, 193, 7);
                }
            }

            if (dataGridViewHistory.Columns[e.ColumnIndex].DataPropertyName == "IsRunning" && e.RowIndex >= 0)
            {
                bool isRunning = false;
                if (e.Value is bool b) isRunning = b;
                else if (e.Value != null && bool.TryParse(e.Value.ToString(), out bool parsed)) isRunning = parsed;

                e.Value = isRunning
                    ? LocalizationService.T("app_history_status_running")
                    : LocalizationService.T("app_history_status_closed");
                e.FormattingApplied = true;
            }
        }

        public async Task LoadHistoryAsync()
        {
            int seq = System.Threading.Interlocked.Increment(ref loadSequence);
            try
            {
                var items = await Task.Run(() => database.GetAppHistory(selectedStartDate, selectedEndDate));

                if (seq != loadSequence)
                    return;

                // Load icons in background (multi-strategy)
                await Task.Run(() =>
                {
                    foreach (var item in items)
                    {
                        try
                        {
                            var bestIcon = IconHelper.GetBestIcon(item.ProcessName, item.ExePath);
                            item.Icon = bestIcon != null
                                ? bestIcon.ToBitmap()
                                : IconHelper.GetPlaceholderBitmap(item.ProcessName, 24);
                        }
                        catch
                        {
                            item.Icon = IconHelper.GetPlaceholderBitmap(item.ProcessName, 24);
                        }
                    }
                });

                if (seq != loadSequence)
                    return;

                if (historyItems.Count == 0 || items.Count != historyItems.Count)
                {
                    var newItems = new BindingList<AppHistoryItem>();
                    foreach (var item in items) newItems.Add(item);
                    historyItems = newItems;
                    dataGridViewHistory.DataSource = historyItems;
                }
                else
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i].Icon == null)
                            items[i].Icon = historyItems[i].Icon;
                        historyItems[i] = items[i];
                    }
                    dataGridViewHistory.Invalidate();
                }

                UpdateStatistics(items);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationService.T("app_history_error_loading"), ex.Message),
                    LocalizationService.T("common_error_title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Alias for compatibility
        public async Task LoadAppHistoryAsync()
        {
            await LoadHistoryAsync();
        }

        private void UpdateStatistics(List<AppHistoryItem> items)
        {
            if (items.Count == 0)
            {
                lblTotalApps.Text = string.Format(LocalizationService.T("app_history_total_apps_format"), 0);
                lblTotalTime.Text = string.Format(LocalizationService.T("app_history_total_time_format"), 0, 0);
                lblMostUsed.Text = LocalizationService.T("app_history_most_used_na");
                lblTopCpu.Text = LocalizationService.T("app_history_top_na");
                lblTopRam.Text = LocalizationService.T("app_history_top_na");
                return;
            }

            lblTotalApps.Text = string.Format(LocalizationService.T("app_history_total_apps_format"), items.Count);
            
            int totalSeconds = items.Sum(i => i.TotalDuration);
            TimeSpan totalTime = TimeSpan.FromSeconds(totalSeconds);
            if (totalTime.TotalMinutes < 1)
            {
                lblTotalTime.Text = string.Format(LocalizationService.T("app_history_total_time_format_short"),
                    0, totalTime.Seconds);
            }
            else
            {
                lblTotalTime.Text = string.Format(LocalizationService.T("app_history_total_time_format"),
                    (int)totalTime.TotalHours, totalTime.Minutes);
            }

            var mostUsed = items.OrderByDescending(i => i.TotalDuration).FirstOrDefault();
            if (mostUsed != null)
            {
                lblMostUsed.Text = string.Format(LocalizationService.T("app_history_most_used_format"),
                    mostUsed.ProcessName, mostUsed.FormattedDuration);
            }

            var topCpu = items.OrderByDescending(i => i.AverageCpu).FirstOrDefault();
            if (topCpu != null && topCpu.AverageCpu > 0)
            {
                lblTopCpu.Text = string.Format(LocalizationService.T("app_history_top_cpu_format"),
                    topCpu.ProcessName, topCpu.AverageCpu);
            }
            else
            {
                lblTopCpu.Text = LocalizationService.T("app_history_top_na");
            }

            var topRam = items.OrderByDescending(i => i.AverageMemory).FirstOrDefault();
            if (topRam != null && topRam.AverageMemory > 0)
            {
                lblTopRam.Text = string.Format(LocalizationService.T("app_history_top_ram_format"),
                    topRam.ProcessName, topRam.FormattedMemory);
            }
            else
            {
                lblTopRam.Text = LocalizationService.T("app_history_top_na");
            }
        }

        private void DatePicker_ValueChanged(object sender, EventArgs e)
        {
            ScheduleHistoryReload();
        }

        private void DatePicker_CloseUp(object sender, EventArgs e)
        {
            ScheduleHistoryReload();
        }

        private void ScheduleHistoryReload()
        {
            selectedStartDate = dateTimePickerStart.Value.Date;
            selectedEndDate = dateTimePickerEnd.Value.Date;

            dateChangeTimer.Stop();
            dateChangeTimer.Start();
        }

        private async void DateChangeTimer_Tick(object sender, EventArgs e)
        {
            dateChangeTimer.Stop();
            await LoadHistoryAsync();
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            btnRefresh.Enabled = false;
            await LoadHistoryAsync();
            btnRefresh.Enabled = true;
        }

        private void btnLast7Days_Click(object sender, EventArgs e)
        {
            dateTimePickerStart.Value = DateTime.Today.AddDays(-7);
            dateTimePickerEnd.Value = DateTime.Today;
            ScheduleHistoryReload();
        }

        private void btnLast30Days_Click(object sender, EventArgs e)
        {
            dateTimePickerStart.Value = DateTime.Today.AddDays(-30);
            dateTimePickerEnd.Value = DateTime.Today;
            ScheduleHistoryReload();
        }

        private async void btnClearData_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                LocalizationService.T("app_history_confirm_clear"),
                LocalizationService.T("common_confirm_title"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    database.CleanOldData(0); // Delete all
                    await LoadHistoryAsync();
                    MessageBox.Show(LocalizationService.T("app_history_cleared_success"),
                        LocalizationService.T("common_success_title"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(LocalizationService.T("app_history_error_clear"), ex.Message),
                        LocalizationService.T("common_error_title"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public void ApplyLocalization()
        {
            UILocalizer.Apply(this);
            UpdateStatistics(historyItems.ToList());
            dataGridViewHistory.Refresh();
        }

        private void SetupLocalizationTags()
        {
            lblFrom.Tag = "app_history_from";
            lblTo.Tag = "app_history_to";
            btnRefresh.Tag = "app_history_refresh";
            btnLast7Days.Tag = "app_history_last_7_days";
            btnLast30Days.Tag = "app_history_last_30_days";
            btnClearData.Tag = "app_history_clear_data";
            lblTopCpu.Tag = "app_history_top_cpu_format";
            lblTopRam.Tag = "app_history_top_ram_format";
        }
    }
}
