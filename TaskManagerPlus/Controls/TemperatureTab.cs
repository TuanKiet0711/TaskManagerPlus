using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Models;
using TaskManagerPlus.Services;

namespace TaskManagerPlus.Controls
{
    public partial class TemperatureTab : UserControl
    {
        private HardwareMonitor hardwareMonitor;
        private readonly Dictionary<string, Queue<double>> temperatureHistory;
        private List<TemperatureEntry> temperatureEntries;
        private const int MaxHistoryPoints = 60;
        private readonly Timer updateTimer;

        // Card management
        private readonly Dictionary<string, TemperatureCardPanel> sensorCards;
        private SummaryCardPanel summaryCard;

        public TemperatureTab()
        {
            InitializeComponent();
            hardwareMonitor = new HardwareMonitor();
            temperatureHistory = new Dictionary<string, Queue<double>>();
            temperatureEntries = new List<TemperatureEntry>();
            sensorCards = new Dictionary<string, TemperatureCardPanel>();

            updateTimer = new Timer { Interval = 2000 };
            updateTimer.Tick += UpdateTimer_Tick;

            SetupLocalizationTags();
            ApplyModernStyling();
        }

        private void ApplyModernStyling()
        {
            this.BackColor = ThemeService.Background;
            this.Padding = new Padding(24);

            // Container panel
            panelHeader.BackColor = ThemeService.Surface;
            ThemeService.ApplyCardStyle(panelHeader);
            ThemeService.ApplyRounding(panelHeader, 16);
            panelHeader.Margin = new Padding(0, 0, 0, 16);

            lblTitle.Font = ThemeService.HeaderFont;
            lblTitle.ForeColor = ThemeService.Text;
            lblInfo.Font = ThemeService.MainFont;
            lblInfo.ForeColor = ThemeService.TextMuted;
            lblLastUpdate.Font = ThemeService.SmallFont;
            lblLastUpdate.ForeColor = ThemeService.TextMuted;

            panelScroll.BackColor = ThemeService.Background;
            
            // Adjust scroll panel layout properly by docking and spacers
            var spacer = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent };
            this.Controls.Add(spacer);
            
            panelScroll.Anchor = AnchorStyles.None;
            panelScroll.Dock = DockStyle.Fill;
            
            spacer.SendToBack();
            panelHeader.SendToBack();
            panelScroll.BringToFront();
        }

        public void Initialize()
        {
            // Create the summary card at top of scroll panel
            summaryCard = new SummaryCardPanel();
            summaryCard.Location = new Point(10, 10);
            summaryCard.Width = panelScroll.ClientRectangle.Width - GetScrollBarWidth() - 20;
            summaryCard.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            panelScroll.Controls.Add(summaryCard);

            panelScroll.Resize += PanelScroll_Resize;

            updateTimer.Start();
            ApplyLocalization();
        }

        public void PauseUpdates() => updateTimer.Stop();

        public void ResumeUpdates()
        {
            updateTimer.Start();
            _ = UpdateTemperaturesAsync();
        }

        private async void UpdateTimer_Tick(object sender, EventArgs e)
        {
            await UpdateTemperaturesAsync();
        }

        public async Task UpdateTemperaturesAsync()
        {
            // Fetch sensor data on background thread (never blocks UI)
            List<TemperatureEntry> entries;
            try
            {
                entries = await Task.Run(() => hardwareMonitor.GetAllTemperatureReadings());
            }
            catch
            {
                return;
            }

            // --- Back on UI thread (SynchronizationContext preserved by await) ---

            temperatureEntries = entries;

            // Update history queues
            var activeKeys = new System.Collections.Generic.HashSet<string>(entries.Select(e => e.Key));
            foreach (var entry in entries)
            {
                if (!temperatureHistory.ContainsKey(entry.Key))
                    temperatureHistory[entry.Key] = new Queue<double>();

                if (entry.Reading != null && entry.Reading.Current > 0)
                {
                    temperatureHistory[entry.Key].Enqueue(entry.Reading.Current);
                    if (temperatureHistory[entry.Key].Count > MaxHistoryPoints)
                        temperatureHistory[entry.Key].Dequeue();
                }
            }

            // Remove stale history
            foreach (var key in temperatureHistory.Keys.Where(k => !activeKeys.Contains(k)).ToList())
                temperatureHistory.Remove(key);

            SyncCards(entries);
            UpdateSummary(entries);

            lblLastUpdate.Text = $"⟳ {DateTime.Now:HH:mm:ss}";
        }

        // ── Card synchronization ────────────────────────────────────────────────

        private void SyncCards(List<TemperatureEntry> entries)
        {
            var visible = entries
                .Where(e => e.Reading != null && (e.Reading.Current > 0 || e.Reading.Max > 0 || e.Reading.Min > 0))
                .OrderBy(e => GetTypeOrder(e.Name))
                .ThenBy(e => e.Name)
                .ToList();

            // Create cards for new sensors
            foreach (var entry in visible)
            {
                if (!sensorCards.ContainsKey(entry.Key))
                {
                    string type = GetComponentType(entry.Name);
                    var card = new TemperatureCardPanel(entry.Name, type);
                    card.Width = panelScroll.ClientRectangle.Width - GetScrollBarWidth() - 20;
                    card.Tag = type;
                    sensorCards[entry.Key] = card;
                    panelScroll.Controls.Add(card);
                }

                var history = temperatureHistory.ContainsKey(entry.Key)
                    ? temperatureHistory[entry.Key]
                    : new Queue<double>();
                sensorCards[entry.Key].UpdateTemperature(entry, history);
            }

            RepositionCards();
        }

        private void RepositionCards()
        {
            int cardW = panelScroll.ClientRectangle.Width - GetScrollBarWidth() - 20;
            int y = 10;

            if (summaryCard != null)
            {
                summaryCard.Location = new Point(10, y);
                summaryCard.Width = cardW;
                y += summaryCard.Height + 8;
            }

            var sortedCards = sensorCards.Values.OrderBy(c => GetTypeOrder((string)c.Tag)).ToList();

            foreach (var card in sortedCards)
            {
                card.Location = new Point(10, y);
                card.Width = cardW;
                y += card.Height + 8;
            }

            // Adjust scroll min size
            panelScroll.AutoScrollMinSize = new Size(0, y + 10);
        }

        private void PanelScroll_Resize(object sender, EventArgs e)
        {
            RepositionCards();
        }

        private void UpdateSummary(List<TemperatureEntry> entries)
        {
            if (summaryCard == null) return;

            var activeReadings = entries.Where(e => e.Reading != null && e.Reading.Current > 0).ToList();
            if (activeReadings.Any())
            {
                var cpuTemps = activeReadings.Where(e => GetComponentType(e.Name) == "CPU").Select(e => e.Reading.Current).ToList();
                var gpuTemps = activeReadings.Where(e => GetComponentType(e.Name) == "GPU").Select(e => e.Reading.Current).ToList();
                var diskTemps = activeReadings.Where(e => GetComponentType(e.Name) == "Disk").Select(e => e.Reading.Current).ToList();

                double pPeak = activeReadings.Max(e => e.Reading.Current);

                double avgCpu = cpuTemps.Any() ? cpuTemps.Average() : 0;
                double avgGpu = gpuTemps.Any() ? gpuTemps.Average() : 0;
                double avgDisk = diskTemps.Any() ? diskTemps.Average() : 0;

                summaryCard.Update(avgCpu, avgGpu, avgDisk, pPeak);
            }
            else
            {
                summaryCard.SetLoading();
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static int GetScrollBarWidth()
        {
            return System.Windows.Forms.SystemInformation.VerticalScrollBarWidth;
        }

        private static string GetComponentType(string name)
        {
            if (name.Contains("CPU") || name.Contains("Ryzen") || name.Contains("Intel") ||
                name.Contains("Core") || name.Contains("Package"))
                return "CPU";
            if (name.Contains("GPU") || name.Contains("NVIDIA") || name.Contains("AMD") ||
                name.Contains("GeForce") || name.Contains("Radeon") || name.Contains("GFX"))
                return "GPU";
            if (name.Contains("Disk") || name.Contains("SSD") || name.Contains("HDD") ||
                name.Contains("Samsung") || name.Contains("WD") || name.Contains("NVMe") ||
                name.Contains("SATA") || name.Contains("Temperature") && name.Contains("Disk"))
                return "Disk";
            return "Other";
        }

        private static int GetTypeOrder(string name)
        {
            string t = GetComponentType(name);
            if (t == "CPU") return 0;
            if (t == "GPU") return 1;
            if (t == "Disk") return 2;
            return 3;
        }

        private static Color GetTemperatureColor(double temperature)
        {
            if (temperature >= 85) return ThemeService.Danger;
            if (temperature >= 75) return ThemeService.Warning;
            if (temperature >= 60) return ThemeService.Warning;
            if (temperature >= 40) return ThemeService.Primary;
            return ThemeService.Success;
        }

        private static string GetTemperatureStatus(double temperature)
        {
            if (temperature >= 85) return LocalizationService.T("temp_status_critical");
            if (temperature >= 75) return LocalizationService.T("temp_status_very_high");
            if (temperature >= 60) return LocalizationService.T("temp_status_high");
            if (temperature >= 40) return LocalizationService.T("temp_status_normal");
            if (temperature > 0)   return LocalizationService.T("temp_status_optimal");
            return LocalizationService.T("common_na");
        }

        public void ApplyLocalization()
        {
            UILocalizer.Apply(this);
        }

        private void SetupLocalizationTags()
        {
            lblTitle.Tag = "temp_title";
            lblInfo.Tag = "temp_info";
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            updateTimer?.Stop();
            updateTimer?.Dispose();
            hardwareMonitor?.Cleanup();
            base.OnHandleDestroyed(e);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Summary Card Panel
        // ════════════════════════════════════════════════════════════════════════

        private class SummaryCardPanel : Panel
        {
            private readonly Label lblCpu, lblGpu, lblDisk, lblStatus;
            private bool _loading = true;

            public SummaryCardPanel()
            {
                this.Height = 64;
                this.BackColor = ThemeService.Primary;
                this.Padding = new Padding(15, 0, 15, 0);

                var table = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 5,
                    RowCount = 1,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0)
                };
                for (int i = 0; i < 4; i++)
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f));

                lblCpu    = MakeSummaryLabel("CPU: —");
                lblGpu    = MakeSummaryLabel("GPU: —");
                lblDisk   = MakeSummaryLabel("Disk: —");
                lblStatus = MakeSummaryLabel("Status: —");
                var lblIcon = MakeSummaryLabel("🌡️", true);

                table.Controls.Add(lblIcon, 0, 0);
                table.Controls.Add(lblCpu, 1, 0);
                table.Controls.Add(lblGpu, 2, 0);
                table.Controls.Add(lblDisk, 3, 0);
                table.Controls.Add(lblStatus, 4, 0);

                this.Controls.Add(table);
            }

            private static Label MakeSummaryLabel(string text, bool isIcon = false)
            {
                return new Label
                {
                    Text = text,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI Semibold", isIcon ? 18f : 9.5f, isIcon ? FontStyle.Regular : FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoEllipsis = true
                };
            }

            public void Update(double avgCpu, double avgGpu, double avgDisk, double peakAll)
            {
                _loading = false;
                lblCpu.Text    = $"CPU\n{(avgCpu > 0 ? avgCpu.ToString("F1") + "°C" : "—")}";
                lblGpu.Text   = $"GPU\n{(avgGpu > 0 ? avgGpu.ToString("F1") + "°C" : "—")}";
                lblDisk.Text  = $"Disk\n{(avgDisk > 0 ? avgDisk.ToString("F1") + "°C" : "—")}";
                
                string statusText = peakAll > 0 ? GetTemperatureStatus(peakAll) : "—";
                lblStatus.Text = $"Status\n{statusText}";
                
                // Dim the panel background based on status
                if (peakAll >= 85) this.BackColor = ThemeService.Danger;
                else if (peakAll >= 75) this.BackColor = ThemeService.Warning;
                else if (peakAll >= 60) this.BackColor = ThemeService.Warning;
                else this.BackColor = ThemeService.Primary;
            }

            public void SetLoading()
            {
                lblCpu.Text   = LocalizationService.T("temp_loading").Split('\n')[0];
                lblGpu.Text  = "—";
                lblDisk.Text = "—";
                lblStatus.Text = "—";
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // Individual Sensor Card
        // ════════════════════════════════════════════════════════════════════════

        private class TemperatureCardPanel : Panel
        {
            private readonly Label lblName;
            private readonly Label lblBadge;
            private readonly Panel pnlHeader;
            private readonly Label lblTemp;
            private readonly Label lblStatus;
            private readonly Label lblMinMax;
            private readonly Panel pnlGauge;
            private readonly PictureBox pbChart;

            private double _currentTemp;
            private Color  _tempColor = ThemeService.Success;
            private Queue<double> _history = new Queue<double>();
            private readonly Color _headerColor;

            public TemperatureCardPanel(string name, string type)
            {
                _headerColor = GetTypeColor(type);
                this.Height      = 155;
                this.BackColor   = Color.White;
                this.Margin      = new Padding(0);

                // ── Header strip ──────────────────────────────────────────────
                pnlHeader = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 36,
                    BackColor = _headerColor,
                    Padding = new Padding(8, 0, 8, 0)
                };

                lblBadge = new Label
                {
                    Text = type,
                    ForeColor = Color.FromArgb(220, 255, 255, 255),
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Width = 50,
                    Dock = DockStyle.Right,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };

                lblName = new Label
                {
                    Text = TruncateName(name),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 0),
                    BackColor = Color.Transparent,
                    AutoEllipsis = true
                };
                pnlHeader.Controls.Add(lblName);
                pnlHeader.Controls.Add(lblBadge);

                // ── Temperature number ────────────────────────────────────────
                lblTemp = new Label
                {
                    Text = "—",
                    Font = new Font("Segoe UI", 22f, FontStyle.Bold),
                    ForeColor = ThemeService.Text,
                    Location = new Point(12, 40),
                    Size = new Size(130, 42),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                // ── Status label ──────────────────────────────────────────────
                lblStatus = new Label
                {
                    Text = "—",
                    Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                    ForeColor = ThemeService.TextMuted,
                    Location = new Point(12, 86),
                    Size = new Size(130, 20),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                // ── Min/Max ───────────────────────────────────────────────────
                lblMinMax = new Label
                {
                    Text = "",
                    Font = new Font("Segoe UI", 8f),
                    ForeColor = ThemeService.TextMuted,
                    Location = new Point(12, 112),
                    Size = new Size(130, 18),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                // ── Heat gauge bar ────────────────────────────────────────────
                pnlGauge = new Panel
                {
                    Location = new Point(150, 45),
                    Height = 18,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                    BackColor = Color.Transparent
                };
                pnlGauge.Paint += PnlGauge_Paint;

                // ── Sparkline ─────────────────────────────────────────────────
                pbChart = new PictureBox
                {
                    Location = new Point(150, 72),
                    Height = 60,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BorderStyle = BorderStyle.None,
                    BackColor = ThemeService.SurfaceAlt
                };

                this.Controls.Add(pnlHeader);
                this.Controls.Add(lblTemp);
                this.Controls.Add(lblStatus);
                this.Controls.Add(lblMinMax);
                this.Controls.Add(pnlGauge);
                this.Controls.Add(pbChart);

                // Card border
                this.Paint += (s, e) =>
                {
                    using (Pen p = new Pen(ThemeService.Border, 1))
                        e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                    // Left accent strip
                    using (SolidBrush b = new SolidBrush(_headerColor))
                        e.Graphics.FillRectangle(b, 0, 36, 3, Height - 36);
                };

                // Update gauge/chart width on resize
                this.Resize += (s, e) =>
                {
                    pnlGauge.Width = Width - 162;
                    pbChart.Width  = Width - 162;
                };
            }

            public void UpdateTemperature(TemperatureEntry entry, Queue<double> history)
            {
                _history = history;
                double temp = entry.Reading?.Current ?? 0;
                _currentTemp = temp;
                _tempColor = GetTemperatureColor(temp);

                lblTemp.Text   = temp > 0 ? $"{temp:F1}°C" : "—";
                lblTemp.ForeColor = temp > 0 ? _tempColor : ThemeService.TextMuted;
                lblStatus.Text = GetTemperatureStatus(temp);
                lblStatus.ForeColor = temp > 0 ? _tempColor : ThemeService.TextMuted;

                double min = entry.Reading?.Min ?? 0;
                double max = entry.Reading?.Max ?? 0;
                if (history != null && history.Count > 0)
                {
                    var valid = history.Where(t => t > 0).ToArray();
                    if (valid.Length > 0)
                    {
                        if (min <= 0) min = valid.Min();
                        if (max <= 0) max = valid.Max();
                    }
                }
                lblMinMax.Text = (min > 0 && max > 0) ? $"Min {min:F1}° | Max {max:F1}°" : "";

                pnlGauge.Invalidate();
                DrawSparkline();
            }

            private void PnlGauge_Paint(object sender, PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, pnlGauge.Width, pnlGauge.Height);

                // Background track
                using (SolidBrush bg = new SolidBrush(ThemeService.Border))
                using (GraphicsPath bgPath = RoundedRect(rect, 5))
                    g.FillPath(bg, bgPath);

                if (_currentTemp > 0)
                {
                    double pct = Math.Min(1.0, Math.Max(0, (_currentTemp - 20.0) / 80.0));
                    int fillW = Math.Max(8, (int)(rect.Width * pct));
                    var fillRect = new Rectangle(0, 0, fillW, rect.Height);

                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new Rectangle(0, 0, Math.Max(1, fillRect.Width), rect.Height),
                        Color.FromArgb(180, _tempColor), _tempColor, 0f))
                    using (GraphicsPath fillPath = RoundedRect(fillRect, 5))
                    {
                        g.FillPath(brush, fillPath);
                    }

                    // Percent text
                    string pctText = $"{pct * 100:F0}%";
                    using (Font f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (SolidBrush tb = new SolidBrush(pct > 0.45 ? Color.White : Color.FromArgb(60, 60, 60)))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(pctText, f, tb, new RectangleF(0, 0, rect.Width, rect.Height), sf);
                }
            }

            private void DrawSparkline()
            {
                if (pbChart.Width <= 2 || pbChart.Height <= 2) return;
                var data = _history?.ToArray();
                if (data == null || data.Length < 2) return;
                var valid = data.Where(d => d > 0).ToArray();
                if (valid.Length < 2) return;

                int w = pbChart.Width;
                int h = pbChart.Height;
                Bitmap bmp = new Bitmap(w, h);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(ThemeService.SurfaceAlt);
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    double maxT = valid.Max() * 1.08;
                    double minT = Math.Max(0, valid.Min() * 0.92);
                    if (maxT <= minT) maxT = minT + 10;

                    var points = new PointF[data.Length];
                    for (int i = 0; i < data.Length; i++)
                    {
                        float x = (float)(w * i / Math.Max(1, data.Length - 1.0));
                        float y = data[i] > 0
                            ? (float)(h - h * (data[i] - minT) / (maxT - minT))
                            : h;
                        // Clamp
                        y = Math.Max(1, Math.Min(h - 1, y));
                        points[i] = new PointF(x, y);
                    }

                    // Fill area
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddLines(points);
                        path.AddLine(points[points.Length - 1].X, h, 0, h);
                        path.CloseFigure();
                        using (LinearGradientBrush brush = new LinearGradientBrush(
                            new Rectangle(0, 0, w, h),
                            Color.FromArgb(70, _tempColor), Color.FromArgb(8, _tempColor),
                            LinearGradientMode.Vertical))
                            g.FillPath(brush, path);
                    }

                    // Line
                    using (Pen lp = new Pen(_tempColor, 1.8f) { LineJoin = LineJoin.Round })
                        g.DrawLines(lp, points);

                    // Current value dot
                    var last = points[points.Length - 1];
                    using (SolidBrush dot = new SolidBrush(_tempColor))
                        g.FillEllipse(dot, last.X - 3, last.Y - 3, 6, 6);
                    using (Pen dotPen = new Pen(Color.White, 1.5f))
                        g.DrawEllipse(dotPen, last.X - 3, last.Y - 3, 6, 6);
                }

                var old = pbChart.Image;
                pbChart.Image = bmp;
                old?.Dispose();
            }

            private static GraphicsPath RoundedRect(Rectangle r, int radius)
            {
                var path = new GraphicsPath();
                int d = radius * 2;
                if (r.Width < d) d = r.Width;
                if (r.Height < d) d = r.Height;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }

            private static Color GetTypeColor(string type)
            {
                if (type == "CPU")  return ThemeService.Primary;
                if (type == "GPU")  return ThemeService.Success;
                if (type == "Disk") return ThemeService.Warning;
                return ThemeService.TextMuted;
            }

            private static string TruncateName(string name, int max = 70)
            {
                return name.Length > max ? name.Substring(0, max - 3) + "..." : name;
            }
        }
    }
}
