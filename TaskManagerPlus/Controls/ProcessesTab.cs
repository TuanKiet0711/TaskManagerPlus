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
    public partial class ProcessesTab : UserControl
    {
        private ProcessMonitor processMonitor;
        private Dictionary<string, List<ProcessInfo>> groupedProcesses;
        private BindingList<object> displayedRows;

        private string currentSortColumn = "MemoryColumn";
        private bool sortAscending = false;

        private Dictionary<string, bool> groupExpanded = new Dictionary<string, bool>
        {
            { "Apps", true },
            { "Background processes", true }
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
        }

        public void Initialize()
        {
            SetupDataGridView();
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
            nameColumn.HeaderText = "Name";
            nameColumn.Width = 300;
            dataGridViewProcesses.Columns.Add(nameColumn);

            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "StatusColumn",
                HeaderText = "Status",
                Width = 100
            });

            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CpuColumn",
                HeaderText = "CPU",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MemoryColumn",
                HeaderText = "Memory",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DiskColumn",
                HeaderText = "Disk",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NetworkColumn",
                HeaderText = "Network",
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
            if (rowObj is string)
            {
                string header = rowObj.ToString();
                string key = GetGroupKeyFromHeader(header);

                bool current;
                if (groupExpanded.TryGetValue(key, out current))
                {
                    groupExpanded[key] = !current;
                    RefreshDisplay();
                }
            }
        }

        private string GetGroupKeyFromHeader(string headerText)
        {
            if (string.IsNullOrWhiteSpace(headerText)) return headerText;
            int idx = headerText.IndexOf('(');
            if (idx > 0) return headerText.Substring(0, idx).Trim();
            return headerText.Trim();
        }

        private bool IsGroupExpanded(string groupHeaderText)
        {
            string key = GetGroupKeyFromHeader(groupHeaderText);
            bool v;
            return groupExpanded.TryGetValue(key, out v) && v;
        }

        private void DataGridViewProcesses_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= displayedRows.Count) return;
            if (e.ColumnIndex != 0) return;

            object row = displayedRows[e.RowIndex];

            // Group header
            if (row is string)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All);

                using (SolidBrush bg = new SolidBrush(Color.FromArgb(248, 249, 250)))
                    e.Graphics.FillRectangle(bg, e.CellBounds);

                string headerText = row.ToString();
                bool expanded = IsGroupExpanded(headerText);

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

            if (row is string)
            {
                e.CellStyle.BackColor = Color.FromArgb(248, 249, 250);
                e.CellStyle.Font = new Font(dataGridViewProcesses.Font, FontStyle.Bold);
                e.CellStyle.SelectionBackColor = Color.FromArgb(248, 249, 250);
                e.CellStyle.SelectionForeColor = Color.FromArgb(0, 120, 212);
                e.Value = (e.ColumnIndex == 0) ? row.ToString() : "";
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
                    displayedRows.Add("Apps (" + apps.Count + ")");
                    if (groupExpanded["Apps"])
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
                    displayedRows.Add("Background processes (" + bg.Count + ")");
                    if (groupExpanded["Background processes"])
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
                MessageBox.Show("Please select a process to end.",
                    "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            object selectedRow = dataGridViewProcesses.SelectedRows[0].DataBoundItem;

            // if group header selected -> toggle expand
            if (selectedRow is string)
            {
                string header = selectedRow.ToString();
                string key = GetGroupKeyFromHeader(header);

                bool current;
                if (groupExpanded.TryGetValue(key, out current))
                {
                    groupExpanded[key] = !current;
                    RefreshDisplay();
                }
                return;
            }

            ProcessInfo selectedProcess = selectedRow as ProcessInfo;
            if (selectedProcess == null) return;

            // Grouped process: kill children
            if (selectedProcess.IsGroup && selectedProcess.ChildProcesses != null && selectedProcess.ChildProcesses.Count > 1)
            {
                DialogResult result = MessageBox.Show(
                    "This will end " + selectedProcess.ChildProcesses.Count + " instances of '" + selectedProcess.ProcessName + "'. Continue?",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        btnKillProcess.Enabled = false;
                        btnKillProcess.Text = "Ending...";

                        foreach (ProcessInfo child in selectedProcess.ChildProcesses)
                        {
                            try { processMonitor.KillProcess(child.ProcessId); }
                            catch { }
                        }

                        await Task.Delay(100);
                        await LoadProcessesAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error ending processes: " + ex.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        btnKillProcess.Enabled = true;
                        btnKillProcess.Text = "End task";
                    }
                }
            }
            else
            {
                DialogResult result = MessageBox.Show(
                    "Do you want to end '" + selectedProcess.ProcessName + "' (PID: " + selectedProcess.ProcessId + ")?",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        btnKillProcess.Enabled = false;
                        btnKillProcess.Text = "Ending...";

                        processMonitor.KillProcess(selectedProcess.ProcessId);

                        await Task.Delay(100);
                        await LoadProcessesAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Cannot end process: " + ex.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        btnKillProcess.Enabled = true;
                        btnKillProcess.Text = "End task";
                    }
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
