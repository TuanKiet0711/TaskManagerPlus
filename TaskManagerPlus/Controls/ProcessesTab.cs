using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
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
        private string currentSortColumn = "MemoryBytes";
        private bool sortAscending = false;
        private bool showGrouped = true;

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
            
            // Disable visual styles for better performance
            dataGridViewProcesses.EnableHeadersVisualStyles = false;
            dataGridViewProcesses.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewProcesses.GridColor = Color.FromArgb(230, 230, 230);

            // Name column with icon
            DataGridViewTextBoxColumn nameColumn = new DataGridViewTextBoxColumn
            {
                Name = "NameColumn",
                HeaderText = "Name",
                Width = 300,
                FillWeight = 35
            };
            dataGridViewProcesses.Columns.Add(nameColumn);

            // Status column
            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "StatusColumn",
                HeaderText = "Status",
                Width = 100,
                FillWeight = 10
            });

            // CPU column
            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CpuColumn",
                HeaderText = "CPU",
                Width = 80,
                FillWeight = 10,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            // Memory column
            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MemoryColumn",
                HeaderText = "Memory",
                Width = 120,
                FillWeight = 12,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            // Disk column
            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DiskColumn",
                HeaderText = "Disk",
                Width = 100,
                FillWeight = 12,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            // Network column
            dataGridViewProcesses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NetworkColumn",
                HeaderText = "Network",
                Width = 100,
                FillWeight = 12,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewProcesses.DataSource = displayedRows;
            dataGridViewProcesses.ColumnHeaderMouseClick += DataGridViewProcesses_ColumnHeaderMouseClick;
            dataGridViewProcesses.CellFormatting += DataGridViewProcesses_CellFormatting;
            dataGridViewProcesses.CellPainting += DataGridViewProcesses_CellPainting;
        }

        private void DataGridViewProcesses_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0)
            {
                var row = displayedRows[e.RowIndex];
                
                // Check if this is a group header
                if (row is string)
                {
                    e.Paint(e.CellBounds, DataGridViewPaintParts.All);
                    
                    // Draw group header background
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(248, 249, 250)))
                    {
                        e.Graphics.FillRectangle(brush, e.CellBounds);
                    }
                    
                    // Draw text
                    using (Font font = new Font(dataGridViewProcesses.Font, FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(0, 120, 212)))
                    {
                        e.Graphics.DrawString(row.ToString(), font, textBrush, 
                            e.CellBounds.X + 25, e.CellBounds.Y + 5);
                    }
                    
                    // Draw expand/collapse arrow
                    Point[] arrow;
                    if (IsGroupExpanded(row.ToString()))
                    {
                        // Down arrow
                        arrow = new Point[]
                        {
                            new Point(e.CellBounds.X + 8, e.CellBounds.Y + 10),
                            new Point(e.CellBounds.X + 14, e.CellBounds.Y + 16),
                            new Point(e.CellBounds.X + 20, e.CellBounds.Y + 10)
                        };
                    }
                    else
                    {
                        // Right arrow
                        arrow = new Point[]
                        {
                            new Point(e.CellBounds.X + 10, e.CellBounds.Y + 8),
                            new Point(e.CellBounds.X + 16, e.CellBounds.Y + 14),
                            new Point(e.CellBounds.X + 10, e.CellBounds.Y + 20)
                        };
                    }
                    
                    using (SolidBrush arrowBrush = new SolidBrush(Color.FromArgb(100, 100, 100)))
                    {
                        e.Graphics.FillPolygon(arrowBrush, arrow);
                    }
                    
                    e.Handled = true;
                }
                else if (row is ProcessInfo process)
                {
                    e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);
                    
                    int xOffset = e.CellBounds.X + 5;
                    
                    // Draw icon
                    if (process.ProcessIcon != null)
                    {
                        try
                        {
                            using (Bitmap bmp = process.ProcessIcon.ToBitmap())
                            {
                                e.Graphics.DrawImage(bmp, xOffset, e.CellBounds.Y + 2, 24, 24);
                            }
                        }
                        catch { }
                    }
                    
                    xOffset += 30;
                    
                    // Draw process name
                    string displayName = string.IsNullOrEmpty(process.Description) ? process.ProcessName : process.Description;
                    if (process.IsGroup && process.ChildProcesses.Count > 1)
                    {
                        displayName += $" ({process.ChildProcesses.Count})";
                    }
                    
                    using (SolidBrush textBrush = new SolidBrush(dataGridViewProcesses.DefaultCellStyle.ForeColor))
                    {
                        e.Graphics.DrawString(displayName, dataGridViewProcesses.Font, textBrush,
                            xOffset, e.CellBounds.Y + 6);
                    }
                    
                    e.Handled = true;
                }
            }
        }

        private Dictionary<string, bool> groupExpanded = new Dictionary<string, bool>
        {
            { "Apps", true },
            { "Background processes", true }
        };

        private bool IsGroupExpanded(string groupName)
        {
            return groupExpanded.ContainsKey(groupName) && groupExpanded[groupName];
        }

        private void DataGridViewProcesses_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= displayedRows.Count) return;

            var row = displayedRows[e.RowIndex];
            
            // Format group headers
            if (row is string)
            {
                e.CellStyle.BackColor = Color.FromArgb(248, 249, 250);
                e.CellStyle.Font = new Font(dataGridViewProcesses.Font, FontStyle.Bold);
                e.CellStyle.SelectionBackColor = Color.FromArgb(248, 249, 250);
                e.CellStyle.SelectionForeColor = Color.FromArgb(0, 120, 212);
                return;
            }
            
            ProcessInfo process = row as ProcessInfo;
            if (process == null) return;

            // Format data columns
            switch (dataGridViewProcesses.Columns[e.ColumnIndex].Name)
            {
                case "StatusColumn":
                    e.Value = process.Status;
                    break;
                    
                case "CpuColumn":
                    e.Value = $"{process.CpuUsage:F1}%";
                    if (process.CpuUsage > 50)
                        e.CellStyle.ForeColor = Color.FromArgb(220, 53, 69);
                        else if (process.CpuUsage > 25)
                        e.CellStyle.ForeColor = Color.FromArgb(255, 193, 7);
                    break;
                    
                case "MemoryColumn":
                    e.Value = process.GetMemoryFormatted();
                    break;
                    
                case "DiskColumn":
                    e.Value = process.DiskUsageFormatted;
                    break;
                    
                case "NetworkColumn":
                    e.Value = process.NetworkUsageFormatted;
                    break;
            }
        }

        private void DataGridViewProcesses_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            string columnName = dataGridViewProcesses.Columns[e.ColumnIndex].Name;
            
            if (currentSortColumn == columnName)
            {
                sortAscending = !sortAscending;
            }
            else
            {
                currentSortColumn = columnName;
                sortAscending = false;
            }

            foreach (DataGridViewColumn col in dataGridViewProcesses.Columns)
            {
                col.HeaderCell.SortGlyphDirection = SortOrder.None;
            }
            dataGridViewProcesses.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection =
                sortAscending ? SortOrder.Ascending : SortOrder.Descending;

            RefreshDisplay();
        }

        public async Task LoadProcessesAsync()
        {
            if (processMonitor == null) return;

            try
            {
                // Suspend layout to prevent flickering
                dataGridViewProcesses.SuspendLayout();
                
                groupedProcesses = await Task.Run(() => processMonitor.GetGroupedProcesses());
                RefreshDisplay();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading processes: {ex.Message}");
            }
            finally
            {
                dataGridViewProcesses.ResumeLayout();
            }
        }

        private void RefreshDisplay()
        {
            if (groupedProcesses == null) return;

            // Store current selection
            int currentRow = dataGridViewProcesses.CurrentCell?.RowIndex ?? -1;
            object selectedItem = null;
            if (currentRow >= 0 && currentRow < displayedRows.Count)
            {
                selectedItem = displayedRows[currentRow];
            }

            // Clear and rebuild efficiently
            displayedRows.RaiseListChangedEvents = false;
            displayedRows.Clear();

            // Filter by search
            string searchText = txtSearch.Text.ToLower();
            
            // Apps group
            if (groupedProcesses.ContainsKey("Apps"))
            {
                var apps = groupedProcesses["Apps"];
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    apps = apps.Where(p =>
                        p.ProcessName.ToLower().Contains(searchText) ||
                        (p.Description != null && p.Description.ToLower().Contains(searchText))
                    ).ToList();
                }
                
                if (apps.Any())
                {
                    displayedRows.Add($"Apps ({apps.Count})");
                    
                    if (IsGroupExpanded("Apps"))
                    {
                        var sortedApps = SortProcessList(apps);
                        foreach (var app in sortedApps)
                        {
                            displayedRows.Add(app);
                        }
                    }
                }
            }

            // Background processes group
            if (groupedProcesses.ContainsKey("Background processes"))
            {
                var bgProcesses = groupedProcesses["Background processes"];
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    bgProcesses = bgProcesses.Where(p =>
                        p.ProcessName.ToLower().Contains(searchText) ||
                        (p.Description != null && p.Description.ToLower().Contains(searchText))
                    ).ToList();
                }
                
                if (bgProcesses.Any())
                {
                    displayedRows.Add($"Background processes ({bgProcesses.Count})");
                    
                    if (IsGroupExpanded("Background processes"))
                    {
                        var sortedBg = SortProcessList(bgProcesses);
                        foreach (var process in sortedBg)
                        {
                            displayedRows.Add(process);
                        }
                    }
                }
            }

            displayedRows.RaiseListChangedEvents = true;
            displayedRows.ResetBindings();

            // Restore selection if possible
            if (selectedItem != null)
            {
                for (int i = 0; i < displayedRows.Count; i++)
                {
                    if (displayedRows[i] == selectedItem)
                    {
                        try
                        {
                            dataGridViewProcesses.CurrentCell = dataGridViewProcesses.Rows[i].Cells[0];
                        }
                        catch { }
                        break;
                    }
                }
            }
        }

        private List<ProcessInfo> SortProcessList(List<ProcessInfo> processes)
        {
            IEnumerable<ProcessInfo> sorted = processes;

            switch (currentSortColumn)
            {
                case "NameColumn":
                    sorted = sortAscending ? 
                        processes.OrderBy(p => string.IsNullOrEmpty(p.Description) ? p.ProcessName : p.Description) : 
                        processes.OrderByDescending(p => string.IsNullOrEmpty(p.Description) ? p.ProcessName : p.Description);
                    break;
                case "CpuColumn":
                    sorted = sortAscending ? processes.OrderBy(p => p.CpuUsage) : processes.OrderByDescending(p => p.CpuUsage);
                    break;
                case "MemoryColumn":
                    sorted = sortAscending ? processes.OrderBy(p => p.MemoryBytes) : processes.OrderByDescending(p => p.MemoryBytes);
                    break;
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

            var selectedRow = dataGridViewProcesses.SelectedRows[0].DataBoundItem;
            if (selectedRow is string)
            {
                // Toggle group expansion
                string groupName = selectedRow.ToString().Split('(')[0].Trim();
                if (groupExpanded.ContainsKey(groupName))
                {
                    groupExpanded[groupName] = !groupExpanded[groupName];
                    RefreshDisplay();
                }
                return;
            }

            ProcessInfo selectedProcess = selectedRow as ProcessInfo;
            if (selectedProcess == null) return;

            // Handle grouped processes
            if (selectedProcess.IsGroup && selectedProcess.ChildProcesses.Count > 1)
            {
                DialogResult result = MessageBox.Show(
                    $"This will end {selectedProcess.ChildProcesses.Count} instances of '{selectedProcess.ProcessName}'. Continue?",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        btnKillProcess.Enabled = false;
                        btnKillProcess.Text = "Ending...";
                        
                        foreach (var child in selectedProcess.ChildProcesses)
                        {
                            try
                            {
                                processMonitor.KillProcess(child.ProcessId);
                            }
                            catch { }
                        }
                        
                        // Wait briefly for processes to terminate
                        await Task.Delay(100);
                        
                        // Immediate refresh
                        await LoadProcessesAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error ending processes: {ex.Message}",
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
                    $"Do you want to end '{selectedProcess.ProcessName}' (PID: {selectedProcess.ProcessId})?",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        btnKillProcess.Enabled = false;
                        btnKillProcess.Text = "Ending...";
                        
                        processMonitor.KillProcess(selectedProcess.ProcessId);
                        
                        // Wait briefly for process to terminate
                        await Task.Delay(100);
                        
                        // Immediate refresh
                        await LoadProcessesAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Cannot end process: {ex.Message}",
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
