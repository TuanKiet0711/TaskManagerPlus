using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Models;
using TaskManagerPlus.Services;

namespace TaskManagerPlus.Controls
{
    public partial class ProcessesTab : UserControl
    {
        private ProcessMonitor processMonitor;
        private Dictionary<string, List<ProcessInfo>> groupedProcesses;
        private BindingList<object> displayedRows;

        private string currentSortColumn = "MemoryColumn";
        private bool sortAscending = false;
        private bool refreshQueued = false;
        private bool watchersStarted = false;
        private readonly object refreshLock = new object();
        private readonly System.Threading.SemaphoreSlim loadSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        private int refreshPending = 0;
        private readonly Dictionary<int, DateTime> suppressedPids = new Dictionary<int, DateTime>();
        private static readonly TimeSpan SuppressDuration = TimeSpan.FromSeconds(3);
        private ManagementEventWatcher processStartWatcher;
        private ManagementEventWatcher processStopWatcher;
        private Timer processRefreshTimer;
        private const int ProcessPollIntervalMs = 500;
        private const int RefreshDebounceMs = 50;

        private Dictionary<string, bool> groupExpanded = new Dictionary<string, bool>
        {
            { "processes_group_apps", true },
            { "processes_group_background", true }
        };

        public ProcessMonitor ProcessMonitor
        {
            get { return processMonitor; }
            set { processMonitor = value; }
        }

        public ProcessesTab()
        {
            InitializeComponent();
            displayedRows = new BindingList<object>();
            SetupLocalizationTags();
        }

        public void Initialize()
        {
            SetupDataGridView();
            ApplyLocalization();
            StartProcessWatchers();
        }

        private void SetupDataGridView()
        {
            dataGridViewProcesses.AutoGenerateColumns = false;
            dataGridViewProcesses.Columns.Clear();
            dataGridViewProcesses.DoubleBuffered(true);
            dataGridViewProcesses.RowTemplate.Height = 28;
            dataGridViewProcesses.AllowUserToResizeRows = false;
            dataGridViewProcesses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewProcesses.MultiSelect = false;

            dataGridViewProcesses.EnableHeadersVisualStyles = false;
            dataGridViewProcesses.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewProcesses.GridColor = Color.FromArgb(230, 230, 230);

            // Name column (icon + text will be painted manually)
            DataGridViewTextBoxColumn nameColumn = new DataGridViewTextBoxColumn();
            nameColumn.Name = "NameColumn";
            nameColumn.Tag = "processes_col_name";
            nameColumn.HeaderText = LocalizationService.T("processes_col_name");
            nameColumn.Width = 300;
            dataGridViewProcesses.Columns.Add(nameColumn);

            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "StatusColumn",
                Tag = "processes_col_status",
                HeaderText = LocalizationService.T("processes_col_status"),
                Width = 100
            });

            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CpuColumn",
                Tag = "processes_col_cpu",
                HeaderText = LocalizationService.T("processes_col_cpu"),
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MemoryColumn",
                Tag = "processes_col_memory",
                HeaderText = LocalizationService.T("processes_col_memory"),
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DiskColumn",
                Tag = "processes_col_disk",
                HeaderText = LocalizationService.T("processes_col_disk"),
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NetworkColumn",
                Tag = "processes_col_network",
                HeaderText = LocalizationService.T("processes_col_network"),
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewProcesses.DataSource = displayedRows;

            dataGridViewProcesses.ColumnHeaderMouseClick += DataGridViewProcesses_ColumnHeaderMouseClick;
            dataGridViewProcesses.CellFormatting += DataGridViewProcesses_CellFormatting;
            dataGridViewProcesses.CellPainting += DataGridViewProcesses_CellPainting;

            // Click header row to expand/collapse
            dataGridViewProcesses.CellClick += DataGridViewProcesses_CellClick;
        }

        private void DataGridViewProcesses_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= displayedRows.Count) return;

            object rowObj = displayedRows[e.RowIndex];
            if (rowObj is GroupHeader header)
            {
                ToggleGroup(header.Key);
            }
        }

        private bool IsGroupExpanded(string groupKey)
        {
            bool v;
            return groupExpanded.TryGetValue(groupKey, out v) && v;
        }

        private void DataGridViewProcesses_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= displayedRows.Count) return;
            if (e.ColumnIndex != 0) return;

            object row = displayedRows[e.RowIndex];

            // Group header
            if (row is GroupHeader header)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All);

                using (SolidBrush bg = new SolidBrush(Color.FromArgb(248, 249, 250)))
                    e.Graphics.FillRectangle(bg, e.CellBounds);

                string headerText = header.DisplayText;
                bool expanded = IsGroupExpanded(header.Key);

                // Arrow
                Point[] arrow;
                if (expanded)
                {
                    arrow = new Point[]
                    {
                        new Point(e.CellBounds.X + 8,  e.CellBounds.Y + 10),
                        new Point(e.CellBounds.X + 14, e.CellBounds.Y + 16),
                        new Point(e.CellBounds.X + 20, e.CellBounds.Y + 10)
                    };
                }
                else
                {
                    arrow = new Point[]
                    {
                        new Point(e.CellBounds.X + 10, e.CellBounds.Y + 8),
                        new Point(e.CellBounds.X + 16, e.CellBounds.Y + 14),
                        new Point(e.CellBounds.X + 10, e.CellBounds.Y + 20)
                    };
                }

                using (SolidBrush arrowBrush = new SolidBrush(Color.FromArgb(100, 100, 100)))
                    e.Graphics.FillPolygon(arrowBrush, arrow);

                using (Font font = new Font(dataGridViewProcesses.Font, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(0, 120, 212)))
                {
                    e.Graphics.DrawString(headerText, font, textBrush,
                        e.CellBounds.X + 25, e.CellBounds.Y + 5);
                }

                e.Handled = true;
                return;
            }

            // Process row
            ProcessInfo process = row as ProcessInfo;
            if (process != null)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                int xOffset = e.CellBounds.X + 5;

                if (process.ProcessIcon != null)
                {
                    try
                    {
                        using (Icon icon = (Icon)process.ProcessIcon.Clone())
                        using (Bitmap bmp = icon.ToBitmap())
                        {
                            e.Graphics.DrawImage(bmp, xOffset, e.CellBounds.Y + 2, 24, 24);
                        }
                    }
                    catch { }
                }

                xOffset += 30;

                string displayName = string.IsNullOrWhiteSpace(process.Description)
                    ? process.ProcessName
                    : process.Description;

                if (process.IsGroup && process.ChildProcesses != null && process.ChildProcesses.Count > 1)
                    displayName += " (" + process.ChildProcesses.Count + ")";

                using (SolidBrush textBrush = new SolidBrush(dataGridViewProcesses.DefaultCellStyle.ForeColor))
                {
                    e.Graphics.DrawString(displayName, dataGridViewProcesses.Font, textBrush,
                        xOffset, e.CellBounds.Y + 6);
                }

                e.Handled = true;
            }
        }

        private void DataGridViewProcesses_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= displayedRows.Count) return;

            object row = displayedRows[e.RowIndex];

            if (row is GroupHeader header)
            {
                e.CellStyle.BackColor = Color.FromArgb(248, 249, 250);
                e.CellStyle.Font = new Font(dataGridViewProcesses.Font, FontStyle.Bold);
                e.CellStyle.SelectionBackColor = Color.FromArgb(248, 249, 250);
                e.CellStyle.SelectionForeColor = Color.FromArgb(0, 120, 212);
                e.Value = (e.ColumnIndex == 0) ? header.DisplayText : "";
                return;
            }

            ProcessInfo process = row as ProcessInfo;
            if (process == null) return;

            string col = dataGridViewProcesses.Columns[e.ColumnIndex].Name;

            if (col == "StatusColumn")
            {
                e.Value = process.Status;
            }
            else if (col == "CpuColumn")
            {
                e.Value = string.Format("{0:F1}%", process.CpuUsage);

                if (process.CpuUsage > 50)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(220, 53, 69);
                }
                else if (process.CpuUsage > 25)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(255, 193, 7);
                }
                else
                {
                    e.CellStyle.ForeColor = dataGridViewProcesses.DefaultCellStyle.ForeColor;
                }
            }
            else if (col == "MemoryColumn")
            {
                e.Value = process.GetMemoryFormatted();
            }
            else if (col == "DiskColumn")
            {
                e.Value = process.DiskUsageFormatted;
            }
            else if (col == "NetworkColumn")
            {
                e.Value = process.NetworkUsageFormatted;
            }
        }

        private void DataGridViewProcesses_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            string columnName = dataGridViewProcesses.Columns[e.ColumnIndex].Name;

            if (currentSortColumn == columnName)
                sortAscending = !sortAscending;
            else
            {
                currentSortColumn = columnName;
                sortAscending = false;
            }

            foreach (DataGridViewColumn col in dataGridViewProcesses.Columns)
                col.HeaderCell.SortGlyphDirection = SortOrder.None;

            dataGridViewProcesses.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection =
                sortAscending ? SortOrder.Ascending : SortOrder.Descending;

            RefreshDisplay();
        }

        public async Task LoadProcessesAsync()
        {
            if (processMonitor == null) return;

            if (!loadSemaphore.Wait(0))
            {
                System.Threading.Interlocked.Exchange(ref refreshPending, 1);
                return;
            }

            try
            {
                dataGridViewProcesses.SuspendLayout();
                groupedProcesses = await Task.Run(() => processMonitor.GetGroupedProcesses());
                RefreshDisplay();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading processes: " + ex.Message);
            }
            finally
            {
                dataGridViewProcesses.ResumeLayout();
                loadSemaphore.Release();

                if (System.Threading.Interlocked.Exchange(ref refreshPending, 0) == 1)
                {
                    BeginInvoke(new Action(async () => await LoadProcessesAsync()));
                }
            }
        }

        private void RefreshDisplay()
        {
            if (groupedProcesses == null) return;

            // Keep selection
            int currentRow = (dataGridViewProcesses.CurrentCell != null) ? dataGridViewProcesses.CurrentCell.RowIndex : -1;
            object selectedItem = null;
            if (currentRow >= 0 && currentRow < displayedRows.Count)
                selectedItem = displayedRows[currentRow];

            displayedRows.RaiseListChangedEvents = false;
            displayedRows.Clear();

            string searchText = (txtSearch.Text ?? "").ToLowerInvariant();

            // Apps
            if (groupedProcesses.ContainsKey("Apps"))
            {
                List<ProcessInfo> apps = groupedProcesses["Apps"];
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    apps = apps.Where(p =>
                        (p.ProcessName ?? "").ToLowerInvariant().Contains(searchText) ||
                        (p.Description ?? "").ToLowerInvariant().Contains(searchText)
                    ).ToList();
                }

                if (apps.Count > 0)
                {
                    displayedRows.Add(new GroupHeader("processes_group_apps", apps.Count));
                    if (groupExpanded["processes_group_apps"])
                    {
                        List<ProcessInfo> sortedApps = SortProcessList(apps);
                        foreach (ProcessInfo app in sortedApps) displayedRows.Add(app);
                    }
                }
            }

            // Background
            if (groupedProcesses.ContainsKey("Background processes"))
            {
                List<ProcessInfo> bg = groupedProcesses["Background processes"];
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    bg = bg.Where(p =>
                        (p.ProcessName ?? "").ToLowerInvariant().Contains(searchText) ||
                        (p.Description ?? "").ToLowerInvariant().Contains(searchText)
                    ).ToList();
                }

                if (bg.Count > 0)
                {
                    displayedRows.Add(new GroupHeader("processes_group_background", bg.Count));
                    if (groupExpanded["processes_group_background"])
                    {
                        List<ProcessInfo> sortedBg = SortProcessList(bg);
                        foreach (ProcessInfo p in sortedBg) displayedRows.Add(p);
                    }
                }
            }

            displayedRows.RaiseListChangedEvents = true;
            displayedRows.ResetBindings();

            // Restore selection
            if (selectedItem != null)
            {
                for (int i = 0; i < displayedRows.Count; i++)
                {
                    if (object.ReferenceEquals(displayedRows[i], selectedItem))
                    {
                        try { dataGridViewProcesses.CurrentCell = dataGridViewProcesses.Rows[i].Cells[0]; }
                        catch { }
                        break;
                    }
                }
            }
        }

        private List<ProcessInfo> SortProcessList(List<ProcessInfo> processes)
        {
            IEnumerable<ProcessInfo> sorted = processes;

            if (currentSortColumn == "NameColumn")
            {
                sorted = sortAscending
                    ? processes.OrderBy(p => string.IsNullOrWhiteSpace(p.Description) ? p.ProcessName : p.Description)
                    : processes.OrderByDescending(p => string.IsNullOrWhiteSpace(p.Description) ? p.ProcessName : p.Description);
            }
            else if (currentSortColumn == "CpuColumn")
            {
                sorted = sortAscending ? processes.OrderBy(p => p.CpuUsage) : processes.OrderByDescending(p => p.CpuUsage);
            }
            else if (currentSortColumn == "MemoryColumn")
            {
                sorted = sortAscending ? processes.OrderBy(p => p.MemoryBytes) : processes.OrderByDescending(p => p.MemoryBytes);
            }

            return sorted.ToList();
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            RefreshDisplay();
        }

        private async void btnKillProcess_Click(object sender, EventArgs e)
        {
            if (dataGridViewProcesses.SelectedRows.Count == 0)
            {
                MessageBox.Show(LocalizationService.T("processes_select_process"),
                    LocalizationService.T("common_info_title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            object selectedRow = dataGridViewProcesses.SelectedRows[0].DataBoundItem;

            // if group header selected -> toggle expand
            if (selectedRow is GroupHeader header)
            {
                ToggleGroup(header.Key);
                return;
            }

            ProcessInfo selectedProcess = selectedRow as ProcessInfo;
            if (selectedProcess == null) return;

            string processName = (selectedProcess.ProcessName ?? "").ToLowerInvariant();
            if (processName == "explorer")
            {
                MessageBox.Show("Windows Explorer is required for the desktop and taskbar and cannot be ended here.",
                    LocalizationService.T("common_info_title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Grouped process: only end the selected app process (do not kill all children)
            if (selectedProcess.IsGroup && selectedProcess.ChildProcesses != null && selectedProcess.ChildProcesses.Count > 1)
            {
                DialogResult result = MessageBox.Show(
                    string.Format(LocalizationService.T("processes_confirm_end_single"),
                        selectedProcess.ProcessName, selectedProcess.ProcessId),
                    LocalizationService.T("common_confirm_title"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        btnKillProcess.Enabled = false;
                        btnKillProcess.Text = LocalizationService.T("processes_ending");

                        int killedId = selectedProcess.ProcessId;
                        processMonitor.KillProcess(killedId);

                        SuppressProcesses(new[] { killedId });
                        RemoveProcessesFromGroups(new[] { killedId });
                        _ = RefreshAfterKillAsync(new[] { killedId });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(LocalizationService.T("processes_error_end_single"), ex.Message),
                            LocalizationService.T("common_error_title"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        btnKillProcess.Enabled = true;
                        btnKillProcess.Text = LocalizationService.T("processes_end_task");
                    }
                }
            }
            else
            {
                DialogResult result = MessageBox.Show(
                    string.Format(LocalizationService.T("processes_confirm_end_single"),
                        selectedProcess.ProcessName, selectedProcess.ProcessId),
                    LocalizationService.T("common_confirm_title"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        btnKillProcess.Enabled = false;
                        btnKillProcess.Text = LocalizationService.T("processes_ending");

                        int killedId = selectedProcess.ProcessId;
                        processMonitor.KillProcess(killedId);

                        SuppressProcesses(new[] { killedId });
                        RemoveProcessesFromGroups(new[] { killedId });
                        _ = RefreshAfterKillAsync(new[] { killedId });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(LocalizationService.T("processes_error_end_single"), ex.Message),
                            LocalizationService.T("common_error_title"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        btnKillProcess.Enabled = true;
                        btnKillProcess.Text = LocalizationService.T("processes_end_task");
                    }
                }
            }
        }

        private async Task RefreshAfterKillAsync(IEnumerable<int> processIds)
        {
            try
            {
                await LoadProcessesAsync();
                RemoveSuppressedProcesses();
                if (processIds == null) return;

                await Task.Delay(500);
                await LoadProcessesAsync();
                RemoveSuppressedProcesses();
            }
            catch { }
        }

        private bool ProcessExists(int processId)
        {
            if (groupedProcesses == null) return false;
            foreach (var list in groupedProcesses.Values)
            {
                if (list.Any(p => p.ProcessId == processId))
                    return true;
            }
            return false;
        }

        private void RemoveProcessesFromGroups(IEnumerable<int> processIds)
        {
            if (groupedProcesses == null || processIds == null) return;

            HashSet<int> idSet = new HashSet<int>(processIds);
            foreach (var key in groupedProcesses.Keys.ToList())
            {
                groupedProcesses[key].RemoveAll(p => idSet.Contains(p.ProcessId));
            }

            RefreshDisplay();
        }

        private void SuppressProcesses(IEnumerable<int> processIds)
        {
            if (processIds == null) return;
            DateTime until = DateTime.UtcNow.Add(SuppressDuration);
            foreach (int id in processIds)
            {
                if (id <= 0) continue;
                suppressedPids[id] = until;
            }
        }

        private void RemoveSuppressedProcesses()
        {
            if (groupedProcesses == null) return;

            DateTime now = DateTime.UtcNow;
            List<int> expired = suppressedPids.Where(kvp => kvp.Value <= now).Select(kvp => kvp.Key).ToList();
            foreach (int id in expired) suppressedPids.Remove(id);

            if (suppressedPids.Count == 0) return;

            HashSet<int> idSet = new HashSet<int>(suppressedPids.Keys);

            if (groupedProcesses.ContainsKey("Apps"))
            {
                List<ProcessInfo> apps = groupedProcesses["Apps"];
                apps.RemoveAll(p => idSet.Contains(p.ProcessId));
                foreach (var app in apps)
                {
                    if (app.ChildProcesses != null)
                        app.ChildProcesses.RemoveAll(p => idSet.Contains(p.ProcessId));
                }
            }

            if (groupedProcesses.ContainsKey("Background processes"))
            {
                groupedProcesses["Background processes"].RemoveAll(p => idSet.Contains(p.ProcessId));
            }

            RefreshDisplay();
        }

        private void StartProcessWatchers()
        {
            if (watchersStarted) return;
            watchersStarted = true;

            try
            {
                processStartWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
                processStartWatcher.EventArrived += (s, e) => ScheduleProcessRefresh();
                processStartWatcher.Start();
            }
            catch { }

            try
            {
                processStopWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
                processStopWatcher.EventArrived += (s, e) => ScheduleProcessRefresh();
                processStopWatcher.Start();
            }
            catch { }

            StartProcessPolling();
        }

        private void StopProcessWatchers()
        {
            try { processStartWatcher?.Stop(); } catch { }
            try { processStopWatcher?.Stop(); } catch { }
            processStartWatcher?.Dispose();
            processStopWatcher?.Dispose();

            if (processRefreshTimer != null)
            {
                processRefreshTimer.Stop();
                processRefreshTimer.Dispose();
                processRefreshTimer = null;
            }
        }

        private void StartProcessPolling()
        {
            if (processRefreshTimer != null) return;

            processRefreshTimer = new Timer();
            processRefreshTimer.Interval = ProcessPollIntervalMs;
            processRefreshTimer.Tick += (s, e) => ScheduleProcessRefresh();
            processRefreshTimer.Start();
        }

        private void ScheduleProcessRefresh()
        {
            if (!IsHandleCreated) return;

            lock (refreshLock)
            {
                if (refreshQueued) return;
                refreshQueued = true;
            }

            BeginInvoke(new Action(async () =>
            {
                await Task.Delay(RefreshDebounceMs);
                lock (refreshLock) refreshQueued = false;
                await LoadProcessesAsync();
                RemoveSuppressedProcesses();
            }));
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            StopProcessWatchers();
            base.OnHandleDestroyed(e);
        }

        public void ApplyLocalization()
        {
            UILocalizer.Apply(this);
            dataGridViewProcesses.Refresh();
        }

        private void SetupLocalizationTags()
        {
            lblSearch.Tag = "processes_search_label";
            btnKillProcess.Tag = "processes_end_task";
        }

        private void ToggleGroup(string key)
        {
            bool current;
            if (groupExpanded.TryGetValue(key, out current))
            {
                groupExpanded[key] = !current;
                RefreshDisplay();
            }
        }

        private class GroupHeader
        {
            public GroupHeader(string key, int count)
            {
                Key = key;
                Count = count;
            }

            public string Key { get; }
            public int Count { get; }

            public string DisplayText
            {
                get
                {
                    return string.Format(LocalizationService.T("common_group_header_format"),
                        LocalizationService.T(Key), Count);
                }
            }
        }
    }

    public static class ExtensionMethods
    {
        public static void DoubleBuffered(this DataGridView dgv, bool setting)
        {
            Type dgvType = dgv.GetType();
            System.Reflection.PropertyInfo pi = dgvType.GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            pi.SetValue(dgv, setting, null);
        }
    }
}
