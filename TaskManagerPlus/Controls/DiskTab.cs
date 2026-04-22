using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Services;

namespace TaskManagerPlus.Controls
{
    public class DiskTab : UserControl
    {
        private const int ColName = 0;
        private const int ColSize = 1;
        private const int ColAllocated = 2;
        private const int ColFiles = 3;
        private const int ColFolders = 4;
        private const int ColParentPercent = 5;
        private const int ColModified = 6;

        private Label lblTitle;
        private ComboBox cmbDrives;
        private Button btnScan;
        private Button btnStop;
        private Button btnCleanTemp;
        private ProgressBar progressScan;
        private Label lblScanStatus;
        private Label lblSummary;
        private Label lblTotalFreed;

        private DataGridView gridFolders;

        private DirectorySummary rootSummary;
        private bool isScanning;
        private CancellationTokenSource scanCts;
        private readonly HashSet<string> expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public DiskTab()
        {
            InitializeComponent();
            ApplyModernStyling();
            LoadDrives();
            ApplyLocalization();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 104,
                Padding = new Padding(8, 8, 8, 4),
                BackColor = Color.Transparent
            };

            lblTitle = new Label
            {
                Name = "lblTitle",
                Font = new Font("Segoe UI", 19F, FontStyle.Bold),
                ForeColor = ThemeService.Text,
                AutoSize = true,
                Location = new Point(8, 4)
            };

            cmbDrives = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = ThemeService.MainFont,
                Location = new Point(12, 50),
                Size = new Size(120, 28)
            };

            btnScan = new Button
            {
                Font = ThemeService.BoldFont,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeService.Primary,
                ForeColor = Color.White,
                Location = new Point(142, 48),
                Size = new Size(130, 32)
            };
            btnScan.FlatAppearance.BorderSize = 0;
            btnScan.Click += BtnScan_Click;
            ThemeService.ApplyRounding(btnScan, 7);

            btnStop = new Button
            {
                Font = ThemeService.BoldFont,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Location = new Point(280, 48),
                Size = new Size(90, 32),
                Enabled = false
            };
            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.Click += BtnStop_Click;
            ThemeService.ApplyRounding(btnStop, 7);

            btnCleanTemp = new Button
            {
                Font = ThemeService.BoldFont,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeService.Warning,
                ForeColor = Color.Black,
                Location = new Point(378, 48),
                Size = new Size(170, 32)
            };
            btnCleanTemp.FlatAppearance.BorderSize = 0;
            btnCleanTemp.Click += BtnCleanTemp_Click;
            ThemeService.ApplyRounding(btnCleanTemp, 7);

            progressScan = new ProgressBar
            {
                Location = new Point(560, 53),
                Size = new Size(200, 22),
                Style = ProgressBarStyle.Blocks,
                Visible = false
            };

            lblScanStatus = new Label
            {
                Font = ThemeService.MainFont,
                ForeColor = ThemeService.TextMuted,
                AutoSize = true,
                Location = new Point(768, 55)
            };

            lblTotalFreed = new Label
            {
                Font = ThemeService.BoldFont,
                ForeColor = ThemeService.Success,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(980, 55)
            };

            lblSummary = new Label
            {
                Font = ThemeService.MainFont,
                ForeColor = ThemeService.TextMuted,
                AutoSize = true,
                Location = new Point(12, 84)
            };

            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(cmbDrives);
            topPanel.Controls.Add(btnScan);
            topPanel.Controls.Add(btnStop);
            topPanel.Controls.Add(btnCleanTemp);
            topPanel.Controls.Add(progressScan);
            topPanel.Controls.Add(lblScanStatus);
            topPanel.Controls.Add(lblTotalFreed);
            topPanel.Controls.Add(lblSummary);

            var gridCard = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                BackColor = ThemeService.Surface
            };

            gridFolders = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                BackgroundColor = ThemeService.Surface,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(226, 230, 235),
                EnableHeadersVisualStyles = false
            };

            gridFolders.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
            gridFolders.ColumnHeadersDefaultCellStyle.ForeColor = ThemeService.Text;
            gridFolders.ColumnHeadersDefaultCellStyle.Font = ThemeService.BoldFont;
            gridFolders.DefaultCellStyle.BackColor = ThemeService.Surface;
            gridFolders.DefaultCellStyle.ForeColor = ThemeService.Text;
            gridFolders.DefaultCellStyle.SelectionBackColor = Color.FromArgb(222, 239, 255);
            gridFolders.DefaultCellStyle.SelectionForeColor = ThemeService.Text;
            gridFolders.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
            gridFolders.RowTemplate.Height = 24;

            gridFolders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", FillWeight = 40f, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            gridFolders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSize", Width = 110 });
            gridFolders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAllocated", Width = 110 });
            gridFolders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFiles", Width = 70 });
            gridFolders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFolders", Width = 80 });
            gridFolders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colParentPercent", Width = 120 });
            gridFolders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colModified", Width = 140 });

            gridFolders.CellPainting += GridFolders_CellPainting;
            gridFolders.CellClick += GridFolders_CellClick;
            gridFolders.CellDoubleClick += GridFolders_CellDoubleClick;

            gridCard.Controls.Add(gridFolders);
            ThemeService.ApplyRounding(gridCard, 10);

            Controls.Add(gridCard);
            Controls.Add(topPanel);

            ResumeLayout(false);
        }

        public void ApplyLocalization()
        {
            lblTitle.Text = GetLoc("lbl_disk_title", "Disk Cleaner");
            btnScan.Text = isScanning ? GetLoc("btn_scanning", "Scanning...") : GetLoc("btn_scan_drive", "Scan Drive");
            btnStop.Text = GetLoc("btn_stop_scan", "Stop");
            btnCleanTemp.Text = GetLoc("btn_clean_temp", "Clean Temp");

            gridFolders.Columns[ColName].HeaderText = GetLoc("disk_col_name", "Name");
            gridFolders.Columns[ColSize].HeaderText = GetLoc("disk_col_size", "Size");
            gridFolders.Columns[ColAllocated].HeaderText = GetLoc("disk_col_allocated", "Allocated");
            gridFolders.Columns[ColFiles].HeaderText = GetLoc("disk_col_files", "Files");
            gridFolders.Columns[ColFolders].HeaderText = GetLoc("disk_col_folders", "Folders");
            gridFolders.Columns[ColParentPercent].HeaderText = GetLoc("disk_col_parent_percent", "% Parent");
            gridFolders.Columns[ColModified].HeaderText = GetLoc("disk_col_modified", "Last Modified");

            if (!isScanning && string.IsNullOrWhiteSpace(lblScanStatus.Text))
                lblScanStatus.Text = GetLoc("disk_scan_status_ready", "Ready to scan");
        }

        private void ApplyModernStyling()
        {
            BackColor = ThemeService.Background;
            Padding = new Padding(12);
        }

        private void LoadDrives()
        {
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in drives.Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                    cmbDrives.Items.Add(drive.Name);

                if (cmbDrives.Items.Count > 0)
                    cmbDrives.SelectedIndex = 0;
            }
            catch { }
        }

        private async void BtnScan_Click(object sender, EventArgs e)
        {
            if (isScanning || cmbDrives.SelectedItem == null)
                return;

            string driveRoot = cmbDrives.SelectedItem.ToString();
            scanCts = new CancellationTokenSource();
            isScanning = true;

            expandedPaths.Clear();
            rootSummary = null;
            gridFolders.Rows.Clear();
            lblSummary.Text = string.Empty;

            SetScanningUi(true);

            try
            {
                rootSummary = await Task.Run(() => ScanDirectory(driveRoot, 6, scanCts.Token));
                expandedPaths.Add(rootSummary.Path);
                BuildGridRows();

                lblSummary.Text = string.Format(
                    GetLoc("disk_scan_summary_format", "Total: {0} | Folders: {1} | Files: {2}"),
                    FormatBytes(rootSummary.TotalSize),
                    rootSummary.DirectoryCount,
                    rootSummary.FileCount);

                lblScanStatus.Text = GetLoc("disk_scan_status_done", "Scan completed");
            }
            catch (OperationCanceledException)
            {
                lblScanStatus.Text = GetLoc("disk_scan_status_stopped", "Scan stopped");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(GetLoc("disk_scan_error", "Scan failed: {0}"), ex.Message),
                    GetLoc("common_error_title", "Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                lblScanStatus.Text = GetLoc("disk_scan_status_error", "Scan failed");
            }
            finally
            {
                isScanning = false;
                scanCts.Dispose();
                scanCts = null;
                SetScanningUi(false);
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (isScanning && scanCts != null)
                scanCts.Cancel();
        }

        private void BuildGridRows()
        {
            gridFolders.SuspendLayout();
            try
            {
                gridFolders.Rows.Clear();

                if (rootSummary == null)
                    return;

                AddSummaryRows(rootSummary, 0, rootSummary.TotalSize <= 0 ? 1 : rootSummary.TotalSize);
            }
            finally
            {
                gridFolders.ResumeLayout();
            }
        }

        private void AddSummaryRows(DirectorySummary summary, int depth, long parentSize)
        {
            summary.ParentSize = parentSize;
            bool hasChildren = summary.SubDirectories.Count > 0;
            bool expanded = expandedPaths.Contains(summary.Path);
            double percent = parentSize > 0 ? (summary.TotalSize * 100.0 / parentSize) : 100.0;

            int rowIndex = gridFolders.Rows.Add();
            DataGridViewRow row = gridFolders.Rows[rowIndex];

            string marker = hasChildren ? (expanded ? "[-] " : "[+] ") : "    ";
            row.Cells[ColName].Value = marker + summary.Name;
            row.Cells[ColName].Style.Padding = new Padding(depth * 18, 0, 0, 0);
            row.Cells[ColSize].Value = FormatBytes(summary.TotalSize);
            row.Cells[ColAllocated].Value = FormatBytes(summary.TotalSize);
            row.Cells[ColFiles].Value = summary.FileCount.ToString("N0");
            row.Cells[ColFolders].Value = summary.DirectoryCount.ToString("N0");
            row.Cells[ColParentPercent].Value = percent;
            row.Cells[ColModified].Value = summary.LastModified.ToString("yyyy-MM-dd HH:mm");

            row.Tag = summary;

            if (hasChildren && expanded)
            {
                foreach (DirectorySummary child in summary.SubDirectories.OrderByDescending(d => d.TotalSize).Take(250))
                    AddSummaryRows(child, depth + 1, summary.TotalSize <= 0 ? 1 : summary.TotalSize);
            }
        }

        private void GridFolders_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= gridFolders.Rows.Count || e.ColumnIndex != ColName)
                return;

            DataGridViewRow row = gridFolders.Rows[e.RowIndex];
            DirectorySummary summary = row.Tag as DirectorySummary;
            if (summary == null || summary.SubDirectories.Count == 0)
                return;

            if (expandedPaths.Contains(summary.Path))
                expandedPaths.Remove(summary.Path);
            else
                expandedPaths.Add(summary.Path);

            BuildGridRows();
        }

        private void GridFolders_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= gridFolders.Rows.Count)
                return;

            DirectorySummary summary = gridFolders.Rows[e.RowIndex].Tag as DirectorySummary;
            if (summary != null)
                OpenInExplorer(summary.Path);
        }

        private void GridFolders_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != ColParentPercent)
                return;

            e.Handled = true;
            e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected);

            double percent = 0;
            object raw = gridFolders.Rows[e.RowIndex].Cells[ColParentPercent].Value;
            if (raw != null)
                double.TryParse(raw.ToString(), out percent);

            percent = Math.Max(0, Math.Min(100, percent));

            Rectangle barRect = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + 6, e.CellBounds.Width - 12, e.CellBounds.Height - 12);
            using (var back = new SolidBrush(Color.FromArgb(232, 236, 241)))
                e.Graphics.FillRectangle(back, barRect);

            int fillWidth = (int)(barRect.Width * (percent / 100.0));
            if (fillWidth > 0)
            {
                using (var fill = new SolidBrush(ThemeService.Primary))
                    e.Graphics.FillRectangle(fill, barRect.X, barRect.Y, fillWidth, barRect.Height);
            }

            TextRenderer.DrawText(
                e.Graphics,
                percent.ToString("F1") + "%",
                gridFolders.Font,
                e.CellBounds,
                ThemeService.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            e.Paint(e.CellBounds, DataGridViewPaintParts.Border);
        }

        private void OpenInExplorer(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            try
            {
                Process.Start("explorer.exe", path);
            }
            catch { }
        }

        private void SetScanningUi(bool scanning)
        {
            btnScan.Enabled = !scanning;
            btnStop.Enabled = scanning;
            btnCleanTemp.Enabled = !scanning;
            cmbDrives.Enabled = !scanning;

            btnScan.Text = scanning ? GetLoc("btn_scanning", "Scanning...") : GetLoc("btn_scan_drive", "Scan Drive");
            progressScan.Visible = scanning;
            progressScan.Style = scanning ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;

            if (scanning)
                lblScanStatus.Text = GetLoc("disk_scan_status_running", "Scanning drive... this can take a while");
            else if (string.IsNullOrWhiteSpace(lblScanStatus.Text))
                lblScanStatus.Text = GetLoc("disk_scan_status_ready", "Ready to scan");
        }

        private DirectorySummary ScanDirectory(string path, int depth, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var summary = new DirectorySummary
            {
                Name = string.IsNullOrWhiteSpace(Path.GetFileName(path)) ? path.TrimEnd('\\') : Path.GetFileName(path),
                Path = path,
                LastModified = Directory.GetLastWriteTime(path)
            };

            try
            {
                var dirInfo = new DirectoryInfo(path);

                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        long length = file.Length;
                        summary.TotalSize += length;
                        summary.FileCount++;
                        if (file.LastWriteTime > summary.LastModified)
                            summary.LastModified = file.LastWriteTime;
                    }
                    catch { }
                }

                if (depth <= 0)
                    return summary;

                foreach (var dir in dirInfo.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        if ((dir.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                            continue;

                        var child = ScanDirectory(dir.FullName, depth - 1, token);
                        summary.SubDirectories.Add(child);
                        summary.TotalSize += child.TotalSize;
                        summary.FileCount += child.FileCount;
                        summary.DirectoryCount += 1 + child.DirectoryCount;
                        if (child.LastModified > summary.LastModified)
                            summary.LastModified = child.LastModified;
                    }
                    catch { }
                }
            }
            catch { }

            return summary;
        }

        private void BtnCleanTemp_Click(object sender, EventArgs e)
        {
            long totalFreed = 0;
            string[] tempPaths =
            {
                Path.GetTempPath(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
            };

            foreach (string tPath in tempPaths)
            {
                try
                {
                    if (!Directory.Exists(tPath))
                        continue;

                    var dir = new DirectoryInfo(tPath);
                    foreach (FileInfo file in dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            long size = file.Length;
                            file.Delete();
                            totalFreed += size;
                        }
                        catch { }
                    }
                }
                catch { }
            }

            lblTotalFreed.Text = string.Format("{0} {1}", GetLoc("lbl_freed", "Freed:"), FormatBytes(totalFreed));
            MessageBox.Show(
                string.Format("{0} {1}", GetLoc("lbl_freed", "Freed:"), FormatBytes(totalFreed)),
                GetLoc("common_success_title", "Success"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private string FormatBytes(long bytes)
        {
            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
                dblSByte = bytes / 1024.0;
            return string.Format("{0:0.##} {1}", dblSByte, suffix[i]);
        }

        private string GetLoc(string key, string fallback)
        {
            string val = LocalizationService.GetString(key);
            return string.IsNullOrWhiteSpace(val) || (val.StartsWith("[") && val.EndsWith("]")) ? fallback : val;
        }

        private class DirectorySummary
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public long TotalSize { get; set; }
            public long ParentSize { get; set; }
            public int FileCount { get; set; }
            public int DirectoryCount { get; set; }
            public DateTime LastModified { get; set; }
            public List<DirectorySummary> SubDirectories { get; set; } = new List<DirectorySummary>();
        }
    }
}
