using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Services;

namespace TaskManagerPlus.Controls
{
    public class DiskTab : UserControl
    {
        private ComboBox cmbDrives;
        private Button btnScan;
        private Button btnCleanTemp;
        private TreeView treeFolders;
        private ListBox listFiles;
        private Label lblTotalFreed;
        private bool isScanning = false;

        public DiskTab()
        {
            InitializeComponent();
            ApplyModernStyling();
            LoadDrives();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            Label lblTitle = new Label { Name = "lblTitle", Text = LocalizationService.GetString("lbl_disk_title") ?? "Disk Cleaner", Font = ThemeService.HeaderFont, ForeColor = ThemeService.Text, AutoSize = true, Location = new Point(20, 20) };
            
            cmbDrives = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = ThemeService.MainFont, Location = new Point(20, 70), Size = new Size(100, 30) };
            
            btnScan = new Button { Text = LocalizationService.GetString("btn_scan_drive") ?? "Scan Drive", Font = ThemeService.BoldFont, FlatStyle = FlatStyle.Flat, BackColor = ThemeService.Primary, ForeColor = Color.White, Location = new Point(140, 68), Size = new Size(150, 32) };
            btnScan.FlatAppearance.BorderSize = 0;
            btnScan.Click += BtnScan_Click;
            ThemeService.ApplyRounding(btnScan, 6);

            btnCleanTemp = new Button { Text = LocalizationService.GetString("btn_clean_temp") ?? "🧹 Clean Temp", Font = ThemeService.BoldFont, FlatStyle = FlatStyle.Flat, BackColor = ThemeService.Warning, ForeColor = Color.Black, Location = new Point(310, 68), Size = new Size(200, 32) };
            btnCleanTemp.FlatAppearance.BorderSize = 0;
            btnCleanTemp.Click += BtnCleanTemp_Click;
            ThemeService.ApplyRounding(btnCleanTemp, 6);

            lblTotalFreed = new Label { Text = "", Font = ThemeService.BoldFont, ForeColor = ThemeService.Success, AutoSize = true, Location = new Point(530, 75) };

            Label lblFolders = new Label { Name = "lblFolders", Text = LocalizationService.GetString("lbl_largest_folders") ?? "Largest Folders", Font = ThemeService.BoldFont, ForeColor = ThemeService.Text, AutoSize = true, Location = new Point(20, 115) };
            
            Panel pnlFolders = new Panel { Location = new Point(20, 140), Size = new Size(500, 430), BackColor = ThemeService.Surface };
            treeFolders = new TreeView { Dock = DockStyle.Fill, Font = ThemeService.MainFont, BackColor = ThemeService.Surface, ForeColor = ThemeService.Text, BorderStyle = BorderStyle.None };
            pnlFolders.Controls.Add(treeFolders);
            ThemeService.ApplyRounding(pnlFolders, 8);

            Label lblFiles = new Label { Name = "lblFiles", Text = LocalizationService.GetString("lbl_heaviest_files") ?? "Heaviest Files", Font = ThemeService.BoldFont, ForeColor = ThemeService.Text, AutoSize = true, Location = new Point(540, 115) };
            
            Panel pnlFiles = new Panel { Location = new Point(540, 140), Size = new Size(600, 430), BackColor = ThemeService.Surface };
            listFiles = new ListBox { Dock = DockStyle.Fill, Font = ThemeService.MainFont, BackColor = ThemeService.Surface, ForeColor = ThemeService.Text, BorderStyle = BorderStyle.None };
            pnlFiles.Controls.Add(listFiles);
            ThemeService.ApplyRounding(pnlFiles, 8);

            this.Controls.Add(lblTitle);
            this.Controls.Add(cmbDrives);
            this.Controls.Add(btnScan);
            this.Controls.Add(btnCleanTemp);
            this.Controls.Add(lblTotalFreed);
            this.Controls.Add(lblFolders);
            this.Controls.Add(pnlFolders);
            this.Controls.Add(lblFiles);
            this.Controls.Add(pnlFiles);

            this.ResumeLayout(false);
        }

        public void ApplyLocalization()
        {
            if (this.Controls.Find("lblTitle", false).FirstOrDefault() is Label lTitle) lTitle.Text = LocalizationService.GetString("lbl_disk_title") ?? "Disk Cleaner";
            if (this.Controls.Find("lblFolders", false).FirstOrDefault() is Label lFold) lFold.Text = LocalizationService.GetString("lbl_largest_folders") ?? "Largest Folders";
            if (this.Controls.Find("lblFiles", false).FirstOrDefault() is Label lFil) lFil.Text = LocalizationService.GetString("lbl_heaviest_files") ?? "Heaviest Files";
            
            if (!isScanning) btnScan.Text = LocalizationService.GetString("btn_scan_drive") ?? "Scan Drive";
            btnCleanTemp.Text = LocalizationService.GetString("btn_clean_temp") ?? "🧹 Clean Temp";
            
            if (lblTotalFreed.Text != "") {
                string fmtStr = LocalizationService.GetString("lbl_freed") ?? "Freed:";
                string oldNum = new String(lblTotalFreed.Text.Where(c => char.IsDigit(c) || c=='.' || c==',' || c==' ' || c=='B' || c=='K' || c=='M' || c=='G' || c=='T').ToArray()).Trim();
                lblTotalFreed.Text = $"{fmtStr} {oldNum}";
            }
        }

        private void ApplyModernStyling()
        {
            this.BackColor = ThemeService.Background;
            this.Padding = new Padding(20);
        }

        private void LoadDrives()
        {
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in drives.Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    cmbDrives.Items.Add(drive.Name);
                }
                if (cmbDrives.Items.Count > 0)
                    cmbDrives.SelectedIndex = 0;
            }
            catch { }
        }

        private async void BtnScan_Click(object sender, EventArgs e)
        {
            if (isScanning || cmbDrives.SelectedItem == null) return;
            string driveRoot = cmbDrives.SelectedItem.ToString();
            
            isScanning = true;
            btnScan.Enabled = false;
            btnScan.Text = LocalizationService.GetString("btn_scanning") ?? "Scanning...";
            treeFolders.Nodes.Clear();
            listFiles.Items.Clear();

            try
            {
                var scanResult = await Task.Run(() => ScanDirectory(driveRoot, 3)); // Max depth 3 for speed
                
                // Populate TreeView
                TreeNode rootNode = new TreeNode($"{driveRoot} ({FormatBytes(scanResult.TotalSize)})");
                PopulateTree(rootNode, scanResult);
                treeFolders.Nodes.Add(rootNode);
                rootNode.Expand();

                // Populate Files
                var topFiles = scanResult.Files.OrderByDescending(f => f.Size).Take(50).ToList();
                foreach(var file in topFiles)
                {
                    listFiles.Items.Add($"{FormatBytes(file.Size)} - {file.Path}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                isScanning = false;
                btnScan.Enabled = true;
                btnScan.Text = LocalizationService.GetString("btn_scan_drive") ?? "Scan Drive";
            }
        }

        private void PopulateTree(TreeNode node, DirectorySummary summary)
        {
            foreach (var child in summary.SubDirectories.OrderByDescending(d => d.TotalSize).Take(15))
            {
                if (child.TotalSize > 50 * 1024 * 1024) // Only show folders > 50MB
                {
                    TreeNode subNode = new TreeNode($"{child.Name} ({FormatBytes(child.TotalSize)})");
                    PopulateTree(subNode, child);
                    node.Nodes.Add(subNode);
                }
            }
        }

        private DirectorySummary ScanDirectory(string path, int depth)
        {
            var summary = new DirectorySummary { Name = Path.GetFileName(path) ?? path, Path = path };
            
            try
            {
                var dirInfo = new DirectoryInfo(path);
                
                // Scan Files
                foreach (var file in dirInfo.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true }))
                {
                    try { summary.TotalSize += file.Length; summary.Files.Add(new FileSummary { Path = file.FullName, Size = file.Length }); } catch { }
                }

                if (depth > 0)
                {
                    foreach (var dir in dirInfo.EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true }))
                    {
                        var childSummary = ScanDirectory(dir.FullName, depth - 1);
                        summary.TotalSize += childSummary.TotalSize;
                        summary.SubDirectories.Add(childSummary);
                        summary.Files.AddRange(childSummary.Files); // Bubble up records for big file sort
                    }
                }
            }
            catch { }
            return summary;
        }

        private void BtnCleanTemp_Click(object sender, EventArgs e)
        {
            long totalFreed = 0;
            string[] tempPaths = {
                Path.GetTempPath(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
            };

            foreach (var tPath in tempPaths)
            {
                try
                {
                    if (Directory.Exists(tPath))
                    {
                        var dir = new DirectoryInfo(tPath);
                        foreach (FileInfo file in dir.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true }))
                        {
                            try { long size = file.Length; file.Delete(); totalFreed += size; } catch { }
                        }
                    }
                }
                catch { }
            }

            string fmtStr = LocalizationService.GetString("lbl_freed") ?? "Freed:";
            lblTotalFreed.Text = $"{fmtStr} {FormatBytes(totalFreed)}";
        }

        private string FormatBytes(long bytes)
        {
            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
                dblSByte = bytes / 1024.0;
            return $"{dblSByte:0.##} {suffix[i]}";
        }

        private class DirectorySummary
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public long TotalSize { get; set; } = 0;
            public List<DirectorySummary> SubDirectories { get; set; } = new List<DirectorySummary>();
            public List<FileSummary> Files { get; set; } = new List<FileSummary>();
        }
        
        private class FileSummary
        {
            public string Path { get; set; }
            public long Size { get; set; } = 0;
        }
    }
}
