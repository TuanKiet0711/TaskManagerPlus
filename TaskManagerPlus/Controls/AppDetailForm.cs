using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TaskManagerPlus.Services;

namespace TaskManagerPlus.Controls
{
    /// <summary>
    /// Rich detail form for an app history item. Shows stats, CPU/RAM bars, and session history.
    /// </summary>
    public class AppDetailForm : Form
    {
        private readonly AppHistoryItem _item;
        private readonly List<AppSessionData> _sessions;

        // Controls
        private Panel panelTitle;
        private PictureBox picIcon;
        private Label lblAppName;
        private Label lblStatusBadge;
        private Button btnCloseHeader;

        private Panel panelStats;
        private Panel panelBars;
        private Panel panelSessions;
        private Panel panelButtons;

        private Label lblTotalTime, lblLaunchCount, lblAvgCpu, lblAvgRam;
        private Label lblTotalTimeVal, lblLaunchCountVal, lblAvgCpuVal, lblAvgRamVal;

        private Panel pnlCpuBar, pnlRamBar;
        private Label lblCpuBarLabel, lblRamBarLabel;

        private DataGridView dgvSessions;
        private Button btnEndTask, btnOpenLocation, btnClose;

        // Colors
        private static readonly Color AccentColor = Color.FromArgb(13, 110, 253);
        private static readonly Color GreenColor  = Color.FromArgb(25, 135, 84);
        private static readonly Color RedColor    = Color.FromArgb(220, 53, 69);
        private static readonly Color OrangeColor = Color.FromArgb(255, 140, 0);
        private static readonly Color GrayColor   = Color.FromArgb(108, 117, 125);
        private static readonly Color LightGray   = Color.FromArgb(245, 247, 249);

        public AppDetailForm(AppHistoryItem item, List<AppSessionData> sessions)
        {
            _item = item;
            _sessions = sessions ?? new List<AppSessionData>();

            SetupForm();
            BuildUI();
            PopulateData();
        }

        private void SetupForm()
        {
            this.Text = _item.ProcessName;
            this.Size = new Size(660, 580);
            this.MinimumSize = new Size(580, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.White;
            this.DoubleBuffered = true;

            // Drop shadow effect via border + padding
            this.Padding = new Padding(1);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Color.FromArgb(200, 200, 200), 1))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private void BuildUI()
        {
            // ── Title bar ─────────────────────────────────────────────────────────
            panelTitle = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = AccentColor,
                Padding = new Padding(15, 10, 15, 10)
            };
            panelTitle.Paint += PanelTitle_Paint;
            panelTitle.MouseDown += DragForm_MouseDown;

            picIcon = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(15, 15),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            panelTitle.Controls.Add(picIcon);

            lblAppName = new Label
            {
                Location = new Point(65, 12),
                Size = new Size(480, 28),
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            panelTitle.Controls.Add(lblAppName);

            lblStatusBadge = new Label
            {
                Location = new Point(65, 42),
                Size = new Size(200, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(200, 255, 255, 255),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panelTitle.Controls.Add(lblStatusBadge);

            btnCloseHeader = new Button
            {
                Text = "✕",
                Size = new Size(32, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(panelTitle.Width - 47, 19),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnCloseHeader.FlatAppearance.BorderSize = 0;
            btnCloseHeader.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 255, 255, 255);
            btnCloseHeader.Click += (s, e) => this.Close();
            panelTitle.Controls.Add(btnCloseHeader);

            panelTitle.Resize += (s, e) => btnCloseHeader.Location = new Point(panelTitle.Width - 47, 19);
            this.Controls.Add(panelTitle);

            // ── Stats (4 cards) ───────────────────────────────────────────────────
            panelStats = new Panel
            {
                Dock = DockStyle.Top,
                Height = 95,
                BackColor = LightGray,
                Padding = new Padding(12, 10, 12, 10)
            };
            BuildStatsPanel();
            this.Controls.Add(panelStats);

            // ── CPU / RAM bars ────────────────────────────────────────────────────
            panelBars = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.White,
                Padding = new Padding(15, 8, 15, 8)
            };
            BuildBarsPanel();
            this.Controls.Add(panelBars);

            // ── Buttons (bottom) ──────────────────────────────────────────────────
            panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = LightGray,
                Padding = new Padding(15, 10, 15, 10)
            };
            BuildButtonsPanel();
            this.Controls.Add(panelButtons);

            // ── Session history (fill) ────────────────────────────────────────────
            panelSessions = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(15, 5, 15, 5)
            };
            BuildSessionsPanel();
            this.Controls.Add(panelSessions);

            // Control z-order so Fill goes between top and bottom panels
            this.Controls.SetChildIndex(panelSessions, 0);
        }

        private void PanelTitle_Paint(object sender, PaintEventArgs e)
        {
            // Gradient background for title bar
            var g = e.Graphics;
            using (var brush = new LinearGradientBrush(
                panelTitle.ClientRectangle,
                AccentColor,
                Color.FromArgb(0, 86, 210),
                LinearGradientMode.Horizontal))
            {
                g.FillRectangle(brush, panelTitle.ClientRectangle);
            }
        }

        private Point _dragStart;
        private void DragForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragStart = e.Location;
                var p = sender as Control;
                p.MouseMove += DragForm_MouseMove;
                p.MouseUp += DragForm_MouseUp;
            }
        }
        private void DragForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Left += e.X - _dragStart.X;
                this.Top += e.Y - _dragStart.Y;
            }
        }
        private void DragForm_MouseUp(object sender, MouseEventArgs e)
        {
            var p = sender as Control;
            p.MouseMove -= DragForm_MouseMove;
            p.MouseUp -= DragForm_MouseUp;
        }

        private void BuildStatsPanel()
        {
            // 4 equal-width stat boxes
            string[] labels = new[]
            {
                LocalizationService.T("app_history_col_total_runtime"),
                LocalizationService.T("app_history_col_launch_count"),
                LocalizationService.T("app_history_col_avg_cpu"),
                LocalizationService.T("app_history_col_avg_memory")
            };

            var statLabels = new[] { lblTotalTime, lblLaunchCount, lblAvgCpu, lblAvgRam };
            var statVals   = new[] { lblTotalTimeVal, lblLaunchCountVal, lblAvgCpuVal, lblAvgRamVal };

            var statsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Margin = new Padding(0)
            };
            for (int i = 0; i < 4; i++)
                statsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            statsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var card = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.White,
                    Margin = new Padding(4, 0, 4, 0),
                    Padding = new Padding(10, 8, 10, 8)
                };
                card.Paint += (s, e) =>
                {
                    using (Pen p = new Pen(Color.FromArgb(225, 225, 225), 1))
                        e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                };

                var lbl = new Label
                {
                    Text = labels[idx],
                    Font = new Font("Segoe UI", 8f),
                    ForeColor = GrayColor,
                    Dock = DockStyle.Top,
                    Height = 16,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var val = new Label
                {
                    Text = "—",
                    Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 30, 30),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true
                };
                card.Controls.Add(val);
                card.Controls.Add(lbl);

                switch (idx)
                {
                    case 0: lblTotalTime = lbl; lblTotalTimeVal = val; break;
                    case 1: lblLaunchCount = lbl; lblLaunchCountVal = val; break;
                    case 2: lblAvgCpu = lbl; lblAvgCpuVal = val; break;
                    case 3: lblAvgRam = lbl; lblAvgRamVal = val; break;
                }

                statsTable.Controls.Add(card, idx, 0);
            }

            panelStats.Controls.Add(statsTable);
        }

        private void BuildBarsPanel()
        {
            // CPU bar
            lblCpuBarLabel = new Label
            {
                Text = "CPU",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = GrayColor,
                Location = new Point(0, 4),
                AutoSize = true
            };

            pnlCpuBar = CreateGaugePanel(new Point(60, 2), AccentColor);

            // RAM bar
            lblRamBarLabel = new Label
            {
                Text = "RAM",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = GrayColor,
                Location = new Point(0, 36),
                AutoSize = true
            };

            pnlRamBar = CreateGaugePanel(new Point(60, 34), GreenColor);

            panelBars.Controls.AddRange(new Control[] { lblCpuBarLabel, pnlCpuBar, lblRamBarLabel, pnlRamBar });
            panelBars.Resize += (s, e) =>
            {
                pnlCpuBar.Width = panelBars.ClientRectangle.Width - 75;
                pnlRamBar.Width = panelBars.ClientRectangle.Width - 75;
            };
        }

        private Panel CreateGaugePanel(Point location, Color fillColor)
        {
            var pnl = new Panel
            {
                Location = location,
                Width = 500, // Will be updated on resize
                Height = 24,
                BackColor = Color.FromArgb(235, 235, 235)
            };
            double pct = 0;
            pnl.Tag = pct;

            pnl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                double v = (double)(((Panel)s).Tag ?? 0.0);
                int fillW = (int)(((Panel)s).Width * v);
                if (fillW > 2)
                {
                    using (var brush = new LinearGradientBrush(
                        new Rectangle(0, 0, fillW, pnl.Height),
                        Color.FromArgb(180, fillColor), fillColor,
                        LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(brush, 0, 0, fillW, pnl.Height);
                    }
                }
                // Percent text
                string pctText = $"{v * 100:F0}%";
                using (Font f = new Font("Segoe UI", 8f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(v > 0.5 ? Color.White : Color.FromArgb(60, 60, 60)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(pctText, f, tb, new RectangleF(0, 0, pnl.Width, pnl.Height), sf);
            };

            return pnl;
        }

        private void BuildSessionsPanel()
        {
            var lblTitle = new Label
            {
                Text = LocalizationService.T("app_detail_sessions"),
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 58, 64),
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(0, 4, 0, 0)
            };
            panelSessions.Controls.Add(lblTitle);

            dgvSessions = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(240, 240, 240),
                EnableHeadersVisualStyles = false
            };

            dgvSessions.ColumnHeadersDefaultCellStyle.BackColor = LightGray;
            dgvSessions.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(60, 60, 60);
            dgvSessions.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            dgvSessions.ColumnHeadersHeight = 32;
            dgvSessions.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvSessions.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            dgvSessions.DefaultCellStyle.ForeColor = Color.FromArgb(30, 30, 30);
            dgvSessions.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 242, 255);
            dgvSessions.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvSessions.RowTemplate.Height = 28;
            dgvSessions.AllowUserToResizeRows = false;
            dgvSessions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgvSessions.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = LocalizationService.T("app_detail_session_start"),
                Name = "colStart",
                FillWeight = 35
            });
            dgvSessions.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = LocalizationService.T("app_detail_session_duration"),
                Name = "colDuration",
                FillWeight = 25,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dgvSessions.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = LocalizationService.T("app_history_col_avg_cpu"),
                Name = "colCpu",
                FillWeight = 20,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dgvSessions.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = LocalizationService.T("app_history_col_avg_memory"),
                Name = "colRam",
                FillWeight = 20,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            panelSessions.Controls.Add(dgvSessions);
        }

        private void BuildButtonsPanel()
        {
            btnEndTask = new Button
            {
                Text = "⏹ " + LocalizationService.T("processes_end_task"),
                Size = new Size(140, 32),
                Location = new Point(15, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = RedColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f),
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnEndTask.FlatAppearance.BorderSize = 0;
            btnEndTask.Click += BtnEndTask_Click;

            btnOpenLocation = new Button
            {
                Text = "📂 " + LocalizationService.T("app_detail_open_location"),
                Size = new Size(150, 32),
                Location = new Point(165, 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand
            };
            btnOpenLocation.FlatAppearance.BorderSize = 0;
            btnOpenLocation.Click += BtnOpenLocation_Click;

            btnClose = new Button
            {
                Text = LocalizationService.T("app_detail_close"),
                Size = new Size(100, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = LightGray,
                ForeColor = Color.FromArgb(52, 58, 64),
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            btnClose.Click += (s, e) => this.Close();

            panelButtons.Controls.AddRange(new Control[] { btnEndTask, btnOpenLocation, btnClose });
            panelButtons.Resize += (s, e) =>
            {
                btnClose.Location = new Point(panelButtons.ClientRectangle.Width - btnClose.Width - 15, 10);
            };
        }

        private void BtnEndTask_Click(object sender, EventArgs e)
        {
            try
            {
                var procs = Process.GetProcessesByName(_item.ProcessName);
                if (procs.Length == 0)
                {
                    MessageBox.Show(LocalizationService.T("app_detail_not_running"),
                        LocalizationService.T("common_info_title"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                DialogResult dr = MessageBox.Show(
                    string.Format(LocalizationService.T("processes_confirm_end_single"), _item.ProcessName, procs[0].Id),
                    LocalizationService.T("common_confirm_title"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr == DialogResult.Yes)
                {
                    foreach (var p in procs)
                    {
                        try { p.Kill(); } catch { }
                        finally { try { p.Dispose(); } catch { } }
                    }
                    btnEndTask.Enabled = false;
                    lblStatusBadge.Text = LocalizationService.T("app_history_status_closed");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationService.T("processes_error_end_single"), ex.Message),
                    LocalizationService.T("common_error_title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOpenLocation_Click(object sender, EventArgs e)
        {
            string exePath = _item.ExePath;
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                Process.Start("explorer.exe", $"/select,\"{exePath}\"");
                return;
            }

            // Try to find running process
            try
            {
                var procs = Process.GetProcessesByName(_item.ProcessName);
                if (procs.Length > 0)
                {
                    string path = procs[0].MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                        return;
                    }
                }
            }
            catch { }

            MessageBox.Show(LocalizationService.T("app_detail_location_not_found"),
                LocalizationService.T("common_info_title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PopulateData()
        {
            // Icon
            if (_item.Icon != null)
                picIcon.Image = _item.Icon;
            else
            {
                picIcon.Image = IconHelper.GetPlaceholderBitmap(_item.ProcessName, 40);
            }

            // App name + status
            lblAppName.Text = _item.ProcessName;
            lblStatusBadge.Text = _item.IsRunning
                ? "● " + LocalizationService.T("app_history_status_running")
                : "○ " + LocalizationService.T("app_history_status_closed");

            // End Task button visibility
            btnEndTask.Visible = _item.IsRunning;

            // Stats
            lblTotalTimeVal.Text = _item.FormattedDuration;
            lblLaunchCountVal.Text = _item.LaunchCount.ToString();
            lblAvgCpuVal.Text = $"{_item.AverageCpu:F1}%";
            lblAvgRamVal.Text = _item.FormattedMemory;

            // Color code Avg CPU
            if (_item.AverageCpu > 50) lblAvgCpuVal.ForeColor = RedColor;
            else if (_item.AverageCpu > 20) lblAvgCpuVal.ForeColor = OrangeColor;

            // Bars: max is rough approximation
            double cpuPct = Math.Min(1.0, _item.AverageCpu / 100.0);
            pnlCpuBar.Tag = cpuPct;
            pnlCpuBar.Invalidate();

            // RAM bar: max is 4 GB as reference
            long maxRamBytes = 4L * 1024 * 1024 * 1024;
            double ramPct = Math.Min(1.0, (double)_item.AverageMemory / maxRamBytes);
            pnlRamBar.Tag = ramPct;
            pnlRamBar.Invalidate();

            // Sessions
            PopulateSessionsGrid();
        }

        private void PopulateSessionsGrid()
        {
            dgvSessions.Rows.Clear();
            foreach (var s in _sessions.OrderByDescending(x => x.StartTime))
            {
                DateTime start = s.StartTime;
                bool isRunning = s.EndTime == null && (!s.LastSeen.HasValue || (DateTime.Now - s.LastSeen.Value).TotalSeconds <= 20);
                DateTime end = s.EndTime ?? (isRunning ? DateTime.Now : (s.LastSeen ?? s.StartTime));
                TimeSpan dur = end - start;
                string durStr = dur.TotalHours >= 1
                    ? $"{(int)dur.TotalHours}h {dur.Minutes}m"
                    : $"{dur.Minutes}m {dur.Seconds}s";

                double avgCpu = s.StatCount > 0 ? s.CpuSum / s.StatCount : 0;
                long avgRamBytes = s.StatCount > 0 ? s.RamSum / s.StatCount : 0;

                string ramStr;
                if (avgRamBytes < 1024 * 1024) ramStr = $"{avgRamBytes / 1024.0:F0} KB";
                else if (avgRamBytes < 1024L * 1024 * 1024) ramStr = $"{avgRamBytes / (1024.0 * 1024.0):F1} MB";
                else ramStr = $"{avgRamBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";

                int rowIdx = dgvSessions.Rows.Add(
                    start.ToString("dd/MM/yyyy HH:mm:ss"),
                    durStr,
                    $"{avgCpu:F1}%",
                    ramStr);

                // Color running session
                if (s.EndTime == null || (s.LastSeen.HasValue && (DateTime.Now - s.LastSeen.Value).TotalSeconds <= 20))
                    dgvSessions.Rows[rowIdx].DefaultCellStyle.ForeColor = GreenColor;
            }
        }
    }
}
