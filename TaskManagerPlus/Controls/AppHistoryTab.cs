using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Services;

namespace TaskManagerPlus.Controls
{
    public partial class AppHistoryTab : UserControl
    {
        private AppUsageDatabase database;
        private BindingList<AppHistoryItem> historyItems;
        private DateTime selectedStartDate;
        private DateTime selectedEndDate;

        public AppHistoryTab()
        {
            InitializeComponent();
            database = new AppUsageDatabase();
            historyItems = new BindingList<AppHistoryItem>();
            selectedStartDate = DateTime.Today.AddDays(-7);
            selectedEndDate = DateTime.Today;
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
        }

        private void SetupDataGridView()
        {
            dataGridViewHistory.AutoGenerateColumns = false;
            dataGridViewHistory.Columns.Clear();
            dataGridViewHistory.DoubleBuffered(true);
            dataGridViewHistory.RowTemplate.Height = 28;

            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProcessName",
                Tag = "app_history_col_application",
                HeaderText = LocalizationService.T("app_history_col_application"),
                Width = 250,
                FillWeight = 30
            });

            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "FormattedDuration",
                Tag = "app_history_col_total_runtime",
                HeaderText = LocalizationService.T("app_history_col_total_runtime"),
                Width = 120,
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "AverageCpu",
                Tag = "app_history_col_avg_cpu",
                HeaderText = LocalizationService.T("app_history_col_avg_cpu"),
                Width = 100,
                FillWeight = 12,
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
                Width = 120,
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "LaunchCount",
                Tag = "app_history_col_launch_count",
                HeaderText = LocalizationService.T("app_history_col_launch_count"),
                Width = 100,
                FillWeight = 12,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewHistory.DataSource = historyItems;
            dataGridViewHistory.CellFormatting += DataGridViewHistory_CellFormatting;
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
        }

        public async Task LoadHistoryAsync()
        {
            try
            {
                var items = await Task.Run(() => database.GetAppHistory(selectedStartDate, selectedEndDate));
                
                historyItems.Clear();
                foreach (var item in items)
                {
                    historyItems.Add(item);
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
                return;
            }

            lblTotalApps.Text = string.Format(LocalizationService.T("app_history_total_apps_format"), items.Count);
            
            int totalSeconds = items.Sum(i => i.TotalDuration);
            TimeSpan totalTime = TimeSpan.FromSeconds(totalSeconds);
            lblTotalTime.Text = string.Format(LocalizationService.T("app_history_total_time_format"),
                (int)totalTime.TotalHours, totalTime.Minutes);

            var mostUsed = items.OrderByDescending(i => i.TotalDuration).FirstOrDefault();
            if (mostUsed != null)
            {
                lblMostUsed.Text = string.Format(LocalizationService.T("app_history_most_used_format"),
                    mostUsed.ProcessName, mostUsed.FormattedDuration);
            }
        }

        private async void DatePicker_ValueChanged(object sender, EventArgs e)
        {
            selectedStartDate = dateTimePickerStart.Value.Date;
            selectedEndDate = dateTimePickerEnd.Value.Date;
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
        }

        private void btnLast30Days_Click(object sender, EventArgs e)
        {
            dateTimePickerStart.Value = DateTime.Today.AddDays(-30);
            dateTimePickerEnd.Value = DateTime.Today;
        }

        private void btnClearData_Click(object sender, EventArgs e)
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
                    LoadHistoryAsync();
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
        }
    }
}
