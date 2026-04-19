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
    public partial class OverviewTab : UserControl
    {
        private readonly SummaryService _summaryService = new SummaryService();
        private readonly LiveResourceMonitor _liveMonitor = new LiveResourceMonitor();

        private DailySummaryData _data;
        private Timer _summaryRefreshTimer;
        private Timer _liveRefreshTimer;

        private Panel _rootPanel;
        private Panel _heroPanel;
        private FlowLayoutPanel _statsPanel;
        private TableLayoutPanel _statsGrid;
        private TableLayoutPanel _contentGrid;
        private Panel _topAppsCard;
        private Panel _insightsCard;
        private Panel _liveCard;
        private ListBox _topAppsList;
        private ListView _topCpuList;
        private ListView _topRamList;
        private FlowLayoutPanel _insightsPanel;

        private Label _heroStatusLabel;
        private Label _heroTitleLabel;
        private Label _heroSubtitleLabel;
        private Label _awakeTimeValueLabel;
        private Label _mostUsedValueLabel;
        private Label _peakHourValueLabel;
        private Label _avgCpuValueLabel;
        private Label _avgRamValueLabel;
        private Label _topAppsHintLabel;
        private Label _insightsHintLabel;

        private bool _liveUpdateInProgress;

        private readonly Color PrimaryColor = Color.FromArgb(37, 99, 235);
        private readonly Color SecondaryColor = Color.FromArgb(139, 92, 246);
        private readonly Color SuccessColor = Color.FromArgb(16, 185, 129);
        private readonly Color WarningColor = Color.FromArgb(245, 158, 11);
        private readonly Color DangerColor = Color.FromArgb(239, 68, 68);
        private readonly Color AccentColor = Color.FromArgb(6, 182, 212);
        private readonly Color BackgroundColor = Color.FromArgb(243, 246, 251);
        private readonly Color SurfaceColor = Color.FromArgb(255, 255, 255);
        private readonly Color SurfaceAltColor = Color.FromArgb(249, 250, 252);
        private readonly Color BorderColor = Color.FromArgb(218, 226, 236);
        private readonly Color TextColor = Color.FromArgb(15, 23, 42);
        private readonly Color MutedTextColor = Color.FromArgb(100, 116, 139);
        private readonly Color HeroStartColor = Color.FromArgb(255, 255, 255);
        private readonly Color HeroEndColor = Color.FromArgb(243, 247, 255);

        public OverviewTab()
        {
            InitializeComponent();
            SetupUI();
        }

        public void Initialize()
        {
            LoadSummaryAsync();

            _summaryRefreshTimer = new Timer { Interval = 5 * 60 * 1000 };
            _summaryRefreshTimer.Tick += (s, e) => LoadSummaryAsync();
            _summaryRefreshTimer.Start();

            _liveRefreshTimer = new Timer { Interval = 2000 };
            _liveRefreshTimer.Tick += async (s, e) => await RefreshLiveAsync();
            _liveRefreshTimer.Start();
        }

        private void SetupUI()
        {
            SuspendLayout();
            AutoScroll = true;
            BackColor = BackgroundColor;
            Padding = new Padding(24);
            Controls.Clear();

            _rootPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackgroundColor,
                AutoScroll = true
            };
            _rootPanel.Resize += (s, e) => UpdateResponsiveWidths();

            var stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = BackgroundColor,
                Margin = new Padding(0),
                Padding = new Padding(0, 0, 0, 24)
            };

            _heroPanel = CreateHeroPanel();
            stack.Controls.Add(_heroPanel);

            _statsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackgroundColor,
                Margin = new Padding(0, 0, 0, 24),
                Padding = new Padding(0)
            };
            _statsGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            _statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            _statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            _statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            _statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

            _statsGrid.Controls.Add(CreateMachineTimeCard(out _awakeTimeValueLabel), 0, 0);
            _statsGrid.Controls.Add(CreateStatCard("Ứng dụng nổi bật", "App dùng nhiều nhất", "N/A", SecondaryColor, out _mostUsedValueLabel), 1, 0);
            _statsGrid.Controls.Add(CreateStatCard("Giờ cao điểm", "Khung giờ hoạt động mạnh nhất", "N/A", WarningColor, out _peakHourValueLabel), 2, 0);
            _statsGrid.Controls.Add(CreateStatCard("CPU trung bình", "Mức CPU trung bình", "0.0%", DangerColor, out _avgCpuValueLabel), 3, 0);
            _statsGrid.Controls.Add(CreateStatCard("RAM trung bình", "Mức RAM trung bình", "0.0%", SuccessColor, out _avgRamValueLabel), 4, 0);
            _statsPanel.Controls.Add(_statsGrid);
            stack.Controls.Add(_statsPanel);

            _contentGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 24),
                Padding = new Padding(0)
            };
            _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));

            _topAppsCard = CreateTopAppsCard();
            _insightsCard = CreateInsightsCard();
            _contentGrid.Controls.Add(_topAppsCard, 0, 0);
            _contentGrid.Controls.Add(_insightsCard, 1, 0);
            stack.Controls.Add(_contentGrid);

            _liveCard = CreateLiveResourceCard();
            stack.Controls.Add(_liveCard);

            _rootPanel.Controls.Add(stack);
            Controls.Add(_rootPanel);
            ResumeLayout(true);
            UpdateResponsiveWidths();
        }

        private Panel CreateHeroPanel()
        {
            var hero = new Panel
            {
                Height = 154,
                Width = 1060,
                Margin = new Padding(0, 0, 0, 24),
                Padding = new Padding(20, 18, 20, 18),
                BackColor = HeroEndColor
            };
            hero.Paint += HeroPanel_Paint;
            hero.Resize += (s, e) => ApplyRoundRegion(hero, 20);
            ApplyRoundRegion(hero, 20);

            var chip = CreateStatusChip("Tự động làm mới mỗi 5 phút");
            chip.Dock = DockStyle.Top;
            chip.Margin = new Padding(0, 0, 0, 12);

            _heroTitleLabel = new Label
            {
                Text = "Tổng quan hôm nay",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = TextColor,
                Dock = DockStyle.Top,
                Height = 40,
                AutoEllipsis = true
            };

            _heroSubtitleLabel = new Label
            {
                Text = "Một cái nhìn gọn, rõ và hiện đại về thói quen sử dụng máy của bạn.",
                Font = new Font("Segoe UI", 9.75f),
                ForeColor = MutedTextColor,
                Dock = DockStyle.Top,
                Height = 34,
                AutoEllipsis = true
            };

            _heroStatusLabel = new Label
            {
                Text = "Đang chờ dữ liệu...",
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = PrimaryColor,
                Dock = DockStyle.Top,
                Height = 22,
                AutoEllipsis = true
            };

            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 2, 16, 0)
            };
            leftPanel.Controls.Add(_heroStatusLabel);
            leftPanel.Controls.Add(_heroSubtitleLabel);
            leftPanel.Controls.Add(_heroTitleLabel);
            leftPanel.Controls.Add(chip);

            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SurfaceColor,
                Padding = new Padding(14, 12, 14, 12)
            };
            rightPanel.Resize += (s, e) => ApplyRoundRegion(rightPanel, 14);
            ApplyRoundRegion(rightPanel, 14);
            AttachBorderPaint(rightPanel);

            var dateLabel = new Label
            {
                Text = DateTime.Now.ToString("dddd, dd/MM/yyyy"),
                ForeColor = TextColor,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 24,
                AutoEllipsis = true
            };

            var machineLabel = new Label
            {
                Text = Environment.MachineName,
                ForeColor = MutedTextColor,
                Font = new Font("Segoe UI", 8.75f),
                Dock = DockStyle.Top,
                Height = 24,
                AutoEllipsis = true
            };

            var sessionLabel = new Label
            {
                Text = "Tổng hợp hôm nay",
                ForeColor = MutedTextColor,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Italic),
                Dock = DockStyle.Top,
                Height = 22,
                AutoEllipsis = true
            };

            rightPanel.Controls.Add(machineLabel);
            rightPanel.Controls.Add(dateLabel);
            rightPanel.Controls.Add(sessionLabel);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280f));
            layout.Controls.Add(leftPanel, 0, 0);
            layout.Controls.Add(rightPanel, 1, 0);

            hero.Controls.Add(layout);
            return hero;
        }

        private Control CreateStatusChip(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(12, 6, 12, 6),
                BackColor = Color.FromArgb(225, 235, 255),
                ForeColor = PrimaryColor,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold)
            };
        }

        private Panel CreateStatCard(string title, string hint, string footer, Color accent, out Label valueLabel)
        {
            var isMostUsedCard = title.IndexOf("Ứng dụng nổi bật", StringComparison.OrdinalIgnoreCase) >= 0;
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 144,
                BackColor = SurfaceColor,
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(0),
            };
            AttachBorderPaint(card);
            ApplyRoundRegion(card, 16);
            card.Resize += (s, e) => ApplyRoundRegion(card, 16);

            var accentBar = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = accent };
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(16, 14, 16, 14),
                BackColor = SurfaceColor
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            var titleLabel = new Label
            {
                Text = title.ToUpperInvariant(),
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                ForeColor = MutedTextColor,
                AutoEllipsis = true
            };

            valueLabel = new Label
            {
                Text = footer,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", isMostUsedCard ? 15f : 22f, FontStyle.Bold),
                ForeColor = accent,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true
            };

            var hintLabel = new Label
            {
                Text = hint,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = MutedTextColor,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true
            };

            body.Controls.Add(titleLabel, 0, 0);
            body.Controls.Add(valueLabel, 0, 1);
            body.Controls.Add(hintLabel, 0, 2);
            card.Controls.Add(body);
            card.Controls.Add(accentBar);
            return card;
        }

        private Panel CreateMachineTimeCard(out Label awakeValueLabel)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 144,
                BackColor = SurfaceColor,
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(0),
            };
            AttachBorderPaint(card);
            ApplyRoundRegion(card, 16);
            card.Resize += (s, e) => ApplyRoundRegion(card, 16);

            var accentBar = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = PrimaryColor };
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(16, 14, 16, 14),
                BackColor = SurfaceColor
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

            var titleLabel = new Label
            {
                Text = "THỜI GIAN THỨC",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                ForeColor = MutedTextColor,
                AutoEllipsis = true
            };

            awakeValueLabel = new Label
            {
                Text = "0m",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = AccentColor,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true
            };

            body.Controls.Add(titleLabel, 0, 0);
            body.Controls.Add(awakeValueLabel, 0, 2);
            card.Controls.Add(body);
            card.Controls.Add(accentBar);
            return card;
        }

        private Panel CreateSectionCard(string title, string subtitle, Color accent, out Panel bodyPanel)
        {
            var card = new Panel
            {
                BackColor = SurfaceColor,
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(0)
            };
            AttachBorderPaint(card);

            var accentBar = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = accent };
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                Padding = new Padding(18, 16, 18, 12),
                BackColor = SurfaceColor
            };

            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextColor
            };

            var subtitleLabel = new Label
            {
                Text = subtitle,
                Dock = DockStyle.Bottom,
                Height = 20,
                Font = new Font("Segoe UI", 8.75f),
                ForeColor = MutedTextColor
            };

            bodyPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 0, 18, 18),
                BackColor = SurfaceColor
            };

            header.Controls.Add(subtitleLabel);
            header.Controls.Add(titleLabel);
            card.Controls.Add(bodyPanel);
            card.Controls.Add(header);
            card.Controls.Add(accentBar);
            return card;
        }

        private Panel CreateTopAppsCard()
        {
            Panel bodyPanel;
            var card = CreateSectionCard(
                "Top ứng dụng theo thời gian",
                "Danh sách những ứng dụng bạn dùng nhiều nhất trong ngày.",
                PrimaryColor,
                out bodyPanel);
            card.Width = 640;
            card.Height = 408;
            ApplyRoundRegion(card, 18);
            card.Resize += (s, e) => ApplyRoundRegion(card, 18);

            _topAppsList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = SurfaceAltColor,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 62,
                IntegralHeight = false
            };
            _topAppsList.DrawItem += TopAppsList_DrawItem;
            bodyPanel.Controls.Add(_topAppsList);

            _topAppsHintLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = MutedTextColor,
                Text = "Chưa có dữ liệu để hiển thị."
            };
            bodyPanel.Controls.Add(_topAppsHintLabel);
            return card;
        }

        private Panel CreateInsightsCard()
        {
            Panel bodyPanel;
            var card = CreateSectionCard(
                "Gợi ý nhanh",
                "Tóm tắt những điều đáng chú ý từ dữ liệu hôm nay.",
                SecondaryColor,
                out bodyPanel);
            card.Width = 380;
            card.Height = 408;
            ApplyRoundRegion(card, 18);
            card.Resize += (s, e) => ApplyRoundRegion(card, 18);

            _insightsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = SurfaceColor,
                Padding = new Padding(0)
            };
            bodyPanel.Controls.Add(_insightsPanel);

            _insightsHintLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = MutedTextColor,
                Text = "Mẹo: phần này sẽ tự cập nhật khi có dữ liệu mới."
            };
            bodyPanel.Controls.Add(_insightsHintLabel);
            return card;
        }

        private Panel CreateLiveResourceCard()
        {
            Panel bodyPanel;
            var card = CreateSectionCard(
                "Ứng dụng dùng nhiều tài nguyên (live)",
                "Quan sát CPU và RAM theo thời gian thực cho những tiến trình đang hoạt động.",
                AccentColor,
                out bodyPanel);
            card.Width = 1060;
            card.Height = 382;
            ApplyRoundRegion(card, 18);
            card.Resize += (s, e) => ApplyRoundRegion(card, 18);

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = SurfaceColor,
                Padding = new Padding(0)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            _topCpuList = CreateResourceListView();
            _topRamList = CreateResourceListView();

            table.Controls.Add(CreateResourcePane("Top CPU", "Tiến trình đang tiêu thụ CPU nhiều nhất.", _topCpuList, DangerColor), 0, 0);
            table.Controls.Add(CreateResourcePane("Top RAM", "Tiến trình đang chiếm nhiều bộ nhớ nhất.", _topRamList, SuccessColor), 1, 0);
            bodyPanel.Controls.Add(table);
            return card;
        }

        private Control CreateResourcePane(string title, string subtitle, ListView listView, Color accent)
        {
            var container = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SurfaceAltColor,
                Padding = new Padding(14),
                Margin = new Padding(0, 0, 10, 0)
            };
            AttachBorderPaint(container);
            ApplyRoundRegion(container, 14);
            container.Resize += (s, e) => ApplyRoundRegion(container, 14);

            var head = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = SurfaceAltColor
            };

            var accentDot = new Panel
            {
                Size = new Size(10, 10),
                BackColor = accent,
                Location = new Point(0, 6)
            };

            var titleLabel = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextColor,
                Location = new Point(18, 0)
            };

            var subtitleLabel = new Label
            {
                Text = subtitle,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = MutedTextColor,
                Location = new Point(18, 20)
            };

            listView.Dock = DockStyle.Fill;
            listView.Margin = new Padding(0);

            head.Controls.Add(accentDot);
            head.Controls.Add(titleLabel);
            head.Controls.Add(subtitleLabel);
            container.Controls.Add(listView);
            container.Controls.Add(head);
            return container;
        }

        private ListView CreateResourceListView()
        {
            return new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                ForeColor = TextColor
            };
        }

        private void HeroPanel_Paint(object sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null)
                return;

            using (var brush = new LinearGradientBrush(panel.ClientRectangle, HeroStartColor, HeroEndColor, LinearGradientMode.Horizontal))
                e.Graphics.FillRectangle(brush, panel.ClientRectangle);

            var rect = panel.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            using (var pen = new Pen(Color.FromArgb(55, 255, 255, 255), 1))
                e.Graphics.DrawRectangle(pen, rect);
        }

        private void LoadSummaryAsync()
        {
            _ = LoadSummaryAsyncInternal();
        }

        private async Task LoadSummaryAsyncInternal()
        {
            try
            {
                if (_heroStatusLabel != null)
                    _heroStatusLabel.Text = "Đang tải dữ liệu...";

                _data = await _summaryService.GetDailySummaryAsync();
                RefreshSummaryUI();
            }
            catch
            {
                // Giữ lại dữ liệu tốt nhất đã có trước đó.
            }
        }

        private void RefreshSummaryUI()
        {
            if (_data == null)
                return;

            if (_awakeTimeValueLabel != null)
                _awakeTimeValueLabel.Text = FormatDuration(_data.SystemAwakeSeconds);
            _mostUsedValueLabel.Text = string.IsNullOrWhiteSpace(_data.MostUsedApp) ? "N/A" : _data.MostUsedApp;
            _peakHourValueLabel.Text = _data.PeakHour < 0 ? "N/A" : string.Format("{0:00}:00", _data.PeakHour);
            _avgCpuValueLabel.Text = string.Format("{0:F1}%", _data.AverageCpu);
            _avgRamValueLabel.Text = string.Format("{0:F1}%", _data.AverageRam);

            if (_heroStatusLabel != null)
            {
                _heroStatusLabel.Text = _data.IsEmpty
                    ? "Chưa có dữ liệu hôm nay"
                    : string.Format("Đã cập nhật lúc {0:HH:mm}", DateTime.Now);
            }

            if (_topAppsList != null)
            {
                _topAppsList.BeginUpdate();
                try
                {
                    _topAppsList.Items.Clear();
                    if (_data.TopApps.Any())
                    {
                        foreach (var app in _data.TopApps.Take(5))
                            _topAppsList.Items.Add(app);
                    }
                    else
                    {
                        _topAppsList.Items.Add("Chưa có dữ liệu sử dụng hôm nay.");
                    }
                }
                finally
                {
                    _topAppsList.EndUpdate();
                }
            }

            PopulateInsights();
            UpdateEmptyStates();
        }

        private void PopulateInsights()
        {
            if (_insightsPanel == null)
                return;

            _insightsPanel.SuspendLayout();
            try
            {
                _insightsPanel.Controls.Clear();

                var insights = _data?.Insights ?? new List<string>();
                if (insights.Count == 0)
                {
                    _insightsPanel.Controls.Add(CreateInsightRow("Chưa có thông tin để phân tích.", MutedTextColor));
                    return;
                }

                foreach (var insight in insights)
                    _insightsPanel.Controls.Add(CreateInsightRow(insight, TextColor));
            }
            finally
            {
                _insightsPanel.ResumeLayout(true);
            }
        }

        private Control CreateInsightRow(string text, Color textColor)
        {
            var row = new Panel
            {
                Width = 320,
                Height = 52,
                BackColor = SurfaceAltColor,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(14, 11, 14, 11)
            };
            AttachBorderPaint(row);
            ApplyRoundRegion(row, 12);
            row.Resize += (s, e) => ApplyRoundRegion(row, 12);

            var bullet = new Panel
            {
                Size = new Size(10, 10),
                BackColor = SecondaryColor,
                Location = new Point(14, 19)
            };

            var label = new Label
            {
                Text = text,
                AutoSize = false,
                Location = new Point(34, 10),
                Size = new Size(260, 30),
                Font = new Font("Segoe UI", 9.25f),
                ForeColor = textColor
            };

            row.Controls.Add(label);
            row.Controls.Add(bullet);
            return row;
        }

        private void UpdateEmptyStates()
        {
            if (_topAppsHintLabel != null)
                _topAppsHintLabel.Visible = _data == null || !_data.TopApps.Any();

            if (_insightsHintLabel != null)
                _insightsHintLabel.Visible = _data == null || (_data.Insights == null || _data.Insights.Count == 0);
        }

        private async Task RefreshLiveAsync()
        {
            if (_liveUpdateInProgress)
                return;

            _liveUpdateInProgress = true;
            IReadOnlyList<ProcessResourceUsage> snapshot = Array.Empty<ProcessResourceUsage>();

            try
            {
                snapshot = await Task.Run(() => _liveMonitor.Sample());
            }
            catch
            {
                return;
            }
            finally
            {
                _liveUpdateInProgress = false;
            }

            if (IsDisposed)
                return;

            BeginInvoke((Action)(() =>
            {
                UpdateResourceList(_topCpuList, snapshot.OrderByDescending(x => x.CpuPercent).Take(10));
                UpdateResourceList(_topRamList, snapshot.OrderByDescending(x => x.WorkingSetBytes).Take(10));
            }));
        }

        private void UpdateResourceList(ListView lv, IEnumerable<ProcessResourceUsage> items)
        {
            if (lv == null)
                return;

            lv.BeginUpdate();
            try
            {
                lv.Columns.Clear();
                lv.Items.Clear();

                lv.Columns.Add("Ứng dụng", 220);
                lv.Columns.Add("CPU", 70, HorizontalAlignment.Right);
                lv.Columns.Add("RAM", 80, HorizontalAlignment.Right);

                foreach (var item in items)
                {
                    var name = item.DisplayName;
                    var cpu = string.Format("{0:F1}%", item.CpuPercent);
                    var ram = FormatBytes(item.WorkingSetBytes);
                    lv.Items.Add(new ListViewItem(new[] { name, cpu, ram }));
                }

                if (lv.Items.Count == 0)
                    lv.Items.Add(new ListViewItem(new[] { "Chưa có tiến trình phù hợp.", "-", "-" }));
            }
            finally
            {
                lv.EndUpdate();
            }
        }

        private static string FormatDuration(long seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (ts.TotalHours >= 1)
                return string.Format("{0}h {1}m", (int)ts.TotalHours, ts.Minutes);
            return string.Format("{0}m", ts.Minutes);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return string.Format("{0} B", bytes);

            if (bytes < 1024 * 1024)
                return string.Format("{0:F1} KB", bytes / 1024.0);

            if (bytes < 1024L * 1024L * 1024L)
                return string.Format("{0:F0} MB", bytes / (1024.0 * 1024.0));

            return string.Format("{0:F2} GB", bytes / (1024.0 * 1024.0 * 1024.0));
        }

        private void TopAppsList_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            if (e.Index < 0 || e.Index >= _topAppsList.Items.Count)
                return;

            var bounds = e.Bounds;
            var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var item = _topAppsList.Items[e.Index];

            using (var bgBrush = new SolidBrush(isSelected ? Color.FromArgb(229, 241, 255) : SurfaceAltColor))
                e.Graphics.FillRectangle(bgBrush, bounds);

            if (item is AppTimeUsage usage)
            {
                var cardRect = new Rectangle(bounds.X + 8, bounds.Y + 7, bounds.Width - 16, bounds.Height - 12);
                using (var cardBrush = new SolidBrush(Color.White))
                    e.Graphics.FillRectangle(cardBrush, cardRect);
                using (var borderPen = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(borderPen, cardRect);

                var rank = (e.Index + 1).ToString("0");
                using (var badgeBrush = new SolidBrush(PrimaryColor))
                    e.Graphics.FillEllipse(badgeBrush, bounds.X + 18, bounds.Y + 17, 26, 26);
                using (var rankFont = new Font("Segoe UI", 9, FontStyle.Bold))
                using (var rankBrush = new SolidBrush(Color.White))
                {
                    var rankRect = new Rectangle(bounds.X + 18, bounds.Y + 17, 26, 26);
                    var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(rank, rankFont, rankBrush, rankRect, format);
                }

                var nameRect = new Rectangle(bounds.X + 56, bounds.Y + 11, bounds.Width - 150, 22);
                var subRect = new Rectangle(bounds.X + 56, bounds.Y + 32, bounds.Width - 150, 18);
                using (var nameFont = new Font("Segoe UI", 10, FontStyle.Bold))
                using (var subFont = new Font("Segoe UI", 8.75f))
                using (var nameBrush = new SolidBrush(TextColor))
                using (var subBrush = new SolidBrush(MutedTextColor))
                {
                    e.Graphics.DrawString(usage.AppName, nameFont, nameBrush, nameRect);
                    e.Graphics.DrawString(usage.GetFormattedDuration(), subFont, subBrush, subRect);
                }

                var barRect = new Rectangle(bounds.X + 56, bounds.Bottom - 17, bounds.Width - 100, 6);
                using (var trackBrush = new SolidBrush(Color.FromArgb(230, 236, 245)))
                    e.Graphics.FillRectangle(trackBrush, barRect);

                var maxSeconds = 1;
                if (_topAppsList != null)
                {
                    var maxItem = _topAppsList.Items.OfType<AppTimeUsage>().OrderByDescending(x => x.DurationSeconds).FirstOrDefault();
                    if (maxItem != null)
                        maxSeconds = (int)Math.Max(1L, maxItem.DurationSeconds);
                }

                var ratio = Math.Max(0, Math.Min(1, (double)usage.DurationSeconds / maxSeconds));
                var fillWidth = (int)Math.Max(4, Math.Min(barRect.Width, barRect.Width * ratio));
                var fillRect = new Rectangle(barRect.X, barRect.Y, fillWidth, barRect.Height);
                using (var fillBrush = new SolidBrush(PrimaryColor))
                    e.Graphics.FillRectangle(fillBrush, fillRect);
            }
            else
            {
                var message = item?.ToString() ?? "Không có dữ liệu";
                using (var font = new Font("Segoe UI", 9.5f, FontStyle.Italic))
                using (var brush = new SolidBrush(MutedTextColor))
                {
                    var rect = new Rectangle(bounds.X + 14, bounds.Y + 18, bounds.Width - 28, bounds.Height - 20);
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    e.Graphics.DrawString(message, font, brush, rect, format);
                }
            }

            e.DrawFocusRectangle();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _summaryRefreshTimer?.Stop();
                _summaryRefreshTimer?.Dispose();
                _summaryRefreshTimer = null;

                _liveRefreshTimer?.Stop();
                _liveRefreshTimer?.Dispose();
                _liveRefreshTimer = null;

                _liveMonitor?.Dispose();
            }

            base.Dispose(disposing);
        }

        public void ApplyLocalization()
        {
            UILocalizer.Apply(this);
        }

        private void AttachBorderPaint(Panel panel)
        {
            panel.Paint -= Panel_PaintBorder;
            panel.Paint += Panel_PaintBorder;
        }

        private void Panel_PaintBorder(object sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null)
                return;

            var rect = panel.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            using (var pen = new Pen(BorderColor))
                e.Graphics.DrawRectangle(pen, rect);
        }

        private void UpdateResponsiveWidths()
        {
            if (_rootPanel == null)
                return;

            var width = Math.Max(900, ClientSize.Width - 24);
            _heroPanel.Width = width;
            _contentGrid.Width = width;
            _liveCard.Width = width;
            _statsPanel.Width = width;

            if (_statsGrid != null)
                _statsGrid.Width = width;
        }

        private static void ApplyRoundRegion(Control control, int radius)
        {
            if (control == null || control.Width <= 0 || control.Height <= 0)
                return;

            int d = Math.Max(1, radius * 2);
            var path = new GraphicsPath();
            var rect = new Rectangle(0, 0, control.Width, control.Height);

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            control.Region = new Region(path);
        }
    }
}
