using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
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

        public void ApplyLocalization()
        {
            UILocalizer.Apply(this);
            if (_heroTitleLabel != null) _heroTitleLabel.Text = LocalizationService.T("overview_title");
            if (_heroSubtitleLabel != null) _heroSubtitleLabel.Text = LocalizationService.T("overview_subtitle");
            RefreshSummaryUI();
        }

        private void SetupUI()
        {
            SuspendLayout();
            AutoScroll = true;
            BackColor = ThemeService.Background;
            Padding = new Padding(24);
            Controls.Clear();

            _rootPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeService.Background,
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
                BackColor = ThemeService.Background,
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
                BackColor = ThemeService.Background,
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
            _statsGrid.Controls.Add(CreateStatCard(LocalizationService.T("overview_stat_most_used"), LocalizationService.T("overview_stat_most_used_hint"), "N/A", ThemeService.Secondary, out _mostUsedValueLabel), 1, 0);
            _statsGrid.Controls.Add(CreateStatCard(LocalizationService.T("overview_stat_peak"), LocalizationService.T("overview_stat_peak_hint"), "N/A", ThemeService.Warning, out _peakHourValueLabel), 2, 0);
            _statsGrid.Controls.Add(CreateStatCard(LocalizationService.T("overview_stat_cpu"), LocalizationService.T("overview_stat_cpu_hint"), "0.0%", ThemeService.Danger, out _avgCpuValueLabel), 3, 0);
            _statsGrid.Controls.Add(CreateStatCard(LocalizationService.T("overview_stat_ram"), LocalizationService.T("overview_stat_ram_hint"), "0.0%", ThemeService.Success, out _avgRamValueLabel), 4, 0);
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
                BackColor = Color.FromArgb(243, 247, 255)
            };
            hero.Paint += HeroPanel_Paint;
            hero.Resize += (s, e) => ThemeService.ApplyRounding(hero, 20);
            ThemeService.ApplyRounding(hero, 20);

            var chip = CreateStatusChip(LocalizationService.T("overview_auto_refresh"));
            chip.Dock = DockStyle.Top;
            chip.Margin = new Padding(0, 0, 0, 12);

            _heroTitleLabel = new Label
            {
                Text = LocalizationService.T("overview_title"),
                Font = ThemeService.HeaderFont,
                ForeColor = ThemeService.Text,
                Dock = DockStyle.Top,
                Height = 40,
                AutoEllipsis = true
            };

            _heroSubtitleLabel = new Label
            {
                Text = LocalizationService.T("overview_subtitle"),
                Font = ThemeService.MainFont,
                ForeColor = ThemeService.TextMuted,
                Dock = DockStyle.Top,
                Height = 34,
                AutoEllipsis = true
            };

            _heroStatusLabel = new Label
            {
                Text = LocalizationService.T("overview_waiting_data"),
                Font = ThemeService.BoldFont,
                ForeColor = ThemeService.Primary,
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
                BackColor = ThemeService.Surface,
                Padding = new Padding(14, 12, 14, 12)
            };
            rightPanel.Resize += (s, e) => ThemeService.ApplyRounding(rightPanel, 14);
            ThemeService.ApplyRounding(rightPanel, 14);
            ThemeService.AttachBorder(rightPanel);

            var dateLabel = new Label
            {
                Text = DateTime.Now.ToString("dddd, dd/MM/yyyy"),
                ForeColor = ThemeService.Text,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 24,
                AutoEllipsis = true
            };

            var machineLabel = new Label
            {
                Text = Environment.MachineName,
                ForeColor = ThemeService.TextMuted,
                Font = ThemeService.SmallFont,
                Dock = DockStyle.Top,
                Height = 24,
                AutoEllipsis = true
            };

            var sessionLabel = new Label
            {
                Text = LocalizationService.T("overview_today_summary"),
                ForeColor = ThemeService.TextMuted,
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
                ForeColor = ThemeService.Primary,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold)
            };
        }

        private Panel CreateStatCard(string title, string hint, string footer, Color accent, out Label valueLabel)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 144,
                BackColor = ThemeService.Surface,
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(0),
            };
            ThemeService.AttachBorder(card);
            ThemeService.ApplyRounding(card, 16);
            card.Resize += (s, e) => ThemeService.ApplyRounding(card, 16);

            var accentBar = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = accent };
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(16, 14, 16, 14),
                BackColor = ThemeService.Surface
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            var titleLabel = new Label
            {
                Text = title.ToUpperInvariant(),
                Dock = DockStyle.Fill,
                Font = ThemeService.SmallFont,
                ForeColor = ThemeService.TextMuted,
                AutoEllipsis = true
            };

            valueLabel = new Label
            {
                Text = footer,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", title.Length > 12 ? 15f : 22f, FontStyle.Bold),
                ForeColor = accent,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true
            };

            var hintLabel = new Label
            {
                Text = hint,
                Dock = DockStyle.Fill,
                Font = ThemeService.SmallFont,
                ForeColor = ThemeService.TextMuted,
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
                BackColor = ThemeService.Surface,
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(0),
            };
            ThemeService.AttachBorder(card);
            ThemeService.ApplyRounding(card, 16);
            card.Resize += (s, e) => ThemeService.ApplyRounding(card, 16);

            var accentBar = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = ThemeService.Primary };
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(16, 14, 16, 14),
                BackColor = ThemeService.Surface
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

            var titleLabel = new Label
            {
                Text = LocalizationService.T("overview_stat_awake"),
                Dock = DockStyle.Fill,
                Font = ThemeService.BoldFont,
                ForeColor = ThemeService.TextMuted,
                AutoEllipsis = true
            };

            awakeValueLabel = new Label
            {
                Text = "0m",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = ThemeService.Accent,
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
                BackColor = ThemeService.Surface,
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(0)
            };
            ThemeService.AttachBorder(card);

            var accentBar = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = accent };
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                Padding = new Padding(18, 16, 18, 12),
                BackColor = ThemeService.Surface
            };

            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 24,
                Font = ThemeService.CardTitleFont,
                ForeColor = ThemeService.Text
            };

            var subtitleLabel = new Label
            {
                Text = subtitle,
                Dock = DockStyle.Bottom,
                Height = 20,
                Font = ThemeService.SmallFont,
                ForeColor = ThemeService.TextMuted
            };

            bodyPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 0, 18, 18),
                BackColor = ThemeService.Surface
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
                LocalizationService.T("overview_top_apps_title"),
                LocalizationService.T("overview_top_apps_subtitle"),
                ThemeService.Primary,
                out bodyPanel);
            card.Width = 640;
            card.Height = 408;
            ThemeService.ApplyRounding(card, 18);
            card.Resize += (s, e) => ThemeService.ApplyRounding(card, 18);

            _topAppsList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeService.SurfaceAlt,
                ForeColor = ThemeService.Text,
                BorderStyle = BorderStyle.None,
                Font = ThemeService.MainFont,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 62,
                IntegralHeight = false
            };
            ThemeService.SetDoubleBuffered(_topAppsList);
            _topAppsList.DrawItem += TopAppsList_DrawItem;
            bodyPanel.Controls.Add(_topAppsList);

            _topAppsHintLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Font = ThemeService.SmallFont,
                ForeColor = ThemeService.TextMuted,
                Text = LocalizationService.T("overview_top_apps_subtitle")
            };
            bodyPanel.Controls.Add(_topAppsHintLabel);
            return card;
        }

        private Panel CreateInsightsCard()
        {
            Panel bodyPanel;
            var card = CreateSectionCard(
                LocalizationService.T("overview_insights_title"),
                LocalizationService.T("overview_insights_subtitle"),
                ThemeService.Secondary,
                out bodyPanel);
            card.Width = 380;
            card.Height = 408;
            ThemeService.ApplyRounding(card, 18);
            card.Resize += (s, e) => ThemeService.ApplyRounding(card, 18);

            _insightsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = ThemeService.Surface,
                Padding = new Padding(0)
            };
            bodyPanel.Controls.Add(_insightsPanel);

            _insightsHintLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Font = ThemeService.SmallFont,
                ForeColor = ThemeService.TextMuted,
                Text = LocalizationService.T("overview_insights_hint")
            };
            bodyPanel.Controls.Add(_insightsHintLabel);
            return card;
        }

        private Panel CreateLiveResourceCard()
        {
            Panel bodyPanel;
            var card = CreateSectionCard(
                LocalizationService.T("overview_live_title"),
                LocalizationService.T("overview_live_subtitle"),
                ThemeService.Accent,
                out bodyPanel);
            card.Width = 1060;
            card.Height = 382;
            ThemeService.ApplyRounding(card, 18);
            card.Resize += (s, e) => ThemeService.ApplyRounding(card, 18);

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = ThemeService.Surface,
                Padding = new Padding(0)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            _topCpuList = CreateResourceListView();
            _topRamList = CreateResourceListView();

            table.Controls.Add(CreateResourcePane(LocalizationService.T("overview_live_cpu"), LocalizationService.T("overview_live_cpu_hint"), _topCpuList, ThemeService.Danger), 0, 0);
            table.Controls.Add(CreateResourcePane(LocalizationService.T("overview_live_ram"), LocalizationService.T("overview_live_ram_hint"), _topRamList, ThemeService.Success), 1, 0);
            bodyPanel.Controls.Add(table);
            return card;
        }

        private Control CreateResourcePane(string title, string subtitle, ListView listView, Color accent)
        {
            var container = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeService.SurfaceAlt,
                Padding = new Padding(14),
                Margin = new Padding(0, 0, 10, 0)
            };
            ThemeService.AttachBorder(container);
            ThemeService.ApplyRounding(container, 14);
            container.Resize += (s, e) => ThemeService.ApplyRounding(container, 14);

            var head = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = ThemeService.SurfaceAlt
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
                Font = ThemeService.BoldFont,
                ForeColor = ThemeService.Text,
                Location = new Point(18, 0)
            };

            var subtitleLabel = new Label
            {
                Text = subtitle,
                AutoSize = true,
                Font = ThemeService.SmallFont,
                ForeColor = ThemeService.TextMuted,
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
            var lv = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = ThemeService.MainFont,
                BackColor = ThemeService.Surface,
                ForeColor = ThemeService.Text
            };
            ThemeService.SetDoubleBuffered(lv);
            return lv;
        }

        private void HeroPanel_Paint(object sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null) return;

            using (var brush = new LinearGradientBrush(panel.ClientRectangle, Color.White, Color.FromArgb(243, 247, 255), LinearGradientMode.Horizontal))
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
                    _heroStatusLabel.Text = LocalizationService.T("overview_loading");

                _data = await _summaryService.GetDailySummaryAsync();
                RefreshSummaryUI();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading summary: {ex.Message}");
            }
        }

        private void RefreshSummaryUI()
        {
            if (_data == null) return;

            if (_awakeTimeValueLabel != null)
                _awakeTimeValueLabel.Text = FormatDuration(_data.SystemAwakeSeconds);
            
            _mostUsedValueLabel.Text = string.IsNullOrWhiteSpace(_data.MostUsedApp) ? "N/A" : _data.MostUsedApp;
            _peakHourValueLabel.Text = _data.PeakHour < 0 ? "N/A" : string.Format("{0:00}:00", _data.PeakHour);
            _avgCpuValueLabel.Text = string.Format("{0:F1}%", _data.AverageCpu);
            _avgRamValueLabel.Text = string.Format("{0:F1}%", _data.AverageRam);

            if (_heroStatusLabel != null)
            {
                _heroStatusLabel.Text = _data.IsEmpty
                    ? LocalizationService.T("overview_no_data")
                    : string.Format(LocalizationService.T("overview_updated_at"), DateTime.Now.ToString("HH:mm"));
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
                        _topAppsHintLabel.Visible = false;
                    }
                    else
                    {
                        _topAppsHintLabel.Text = LocalizationService.T("overview_no_apps");
                        _topAppsHintLabel.Visible = true;
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
            if (_insightsPanel == null) return;

            _insightsPanel.SuspendLayout();
            try
            {
                _insightsPanel.Controls.Clear();

                var insights = _data?.Insights ?? new List<string>();
                if (insights.Count == 0)
                {
                    _insightsPanel.Controls.Add(CreateInsightRow(LocalizationService.T("overview_no_insights"), ThemeService.TextMuted));
                    return;
                }

                foreach (var insight in insights)
                    _insightsPanel.Controls.Add(CreateInsightRow(insight, ThemeService.Text));
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
                BackColor = ThemeService.SurfaceAlt,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(14, 11, 14, 11)
            };
            ThemeService.AttachBorder(row);
            ThemeService.ApplyRounding(row, 12);
            row.Resize += (s, e) => ThemeService.ApplyRounding(row, 12);

            var bullet = new Panel
            {
                Size = new Size(10, 10),
                BackColor = ThemeService.Secondary,
                Location = new Point(14, 19)
            };

            var label = new Label
            {
                Text = text,
                AutoSize = false,
                Location = new Point(34, 10),
                Size = new Size(260, 30),
                Font = ThemeService.MainFont,
                ForeColor = textColor
            };

            row.Controls.Add(label);
            row.Controls.Add(bullet);
            return row;
        }

        private async Task RefreshLiveAsync()
        {
            if (_liveUpdateInProgress) return;
            _liveUpdateInProgress = true;

            try
            {
                var topCpu = await Task.Run(() => _liveMonitor.GetTopCpuProcesses(6));
                var topRam = await Task.Run(() => _liveMonitor.GetTopRamProcesses(6));

                await Task.Yield();
                UpdateLiveList(_topCpuList, topCpu, "CPU");
                UpdateLiveList(_topRamList, topRam, "RAM");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Live refresh error: {ex.Message}");
            }
            finally
            {
                _liveUpdateInProgress = false;
            }
        }

        private void UpdateLiveList(ListView list, IEnumerable<ProcessResourceUsage> data, string type)
        {
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                list.Columns.Clear();
                list.Columns.Add(LocalizationService.T("overview_col_app"), 220);
                list.Columns.Add(type, 80, HorizontalAlignment.Right);

                if (data == null || !data.Any())
                {
                    list.Items.Add(new ListViewItem(LocalizationService.T("overview_no_live")));
                    return;
                }

                foreach (var item in data)
                {
                    string value = type == "CPU" ? $"{item.CpuPercent:F1}%" : $"{(item.WorkingSetBytes / 1024.0 / 1024.0):F0} MB";
                    var lvi = new ListViewItem(new[] { item.DisplayName, value });
                    list.Items.Add(lvi);
                }
            }
            finally
            {
                list.EndUpdate();
            }
        }

        private void TopAppsList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var list = sender as ListBox;
            var app = list.Items[e.Index] as AppTimeUsage;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.DrawBackground();

            if (app != null)
            {
                var rect = e.Bounds;
                rect.Inflate(-10, -8);

                // App Name
                using (var nameFont = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold))
                    e.Graphics.DrawString(app.AppName, nameFont, new SolidBrush(ThemeService.Text), rect.X, rect.Y);

                // Duration
                string duration = FormatDuration(app.DurationSeconds);
                using (var durFont = new Font("Segoe UI", 9.5f))
                {
                    var size = e.Graphics.MeasureString(duration, durFont);
                    e.Graphics.DrawString(duration, durFont, new SolidBrush(ThemeService.TextMuted), rect.Right - size.Width, rect.Y + 2);
                }

                // Progress bar
                int barY = rect.Y + 28;
                int barHeight = 8;
                var trackRect = new Rectangle(rect.X, barY, rect.Width, barHeight);
                
                using (var trackBrush = new SolidBrush(Color.FromArgb(230, 235, 245)))
                using (var path = ThemeService.GetRoundedPath(trackRect, 4))
                    e.Graphics.FillPath(trackBrush, path);

                int fillWidth = (int)(rect.Width * (app.PercentageOfTotal / 100.0));
                if (fillWidth > 0)
                {
                    var fillRect = new Rectangle(rect.X, barY, Math.Max(fillWidth, 8), barHeight);
                    using (var fillBrush = new LinearGradientBrush(fillRect, ThemeService.Primary, Color.FromArgb(96, 165, 250), 0f))
                    using (var path = ThemeService.GetRoundedPath(fillRect, 4))
                        e.Graphics.FillPath(fillBrush, path);
                }
            }
            else
            {
                e.Graphics.DrawString(list.Items[e.Index].ToString(), ThemeService.MainFont, new SolidBrush(ThemeService.TextMuted), e.Bounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
            
            e.DrawFocusRectangle();
        }

        private void UpdateResponsiveWidths()
        {
            if (_rootPanel == null) return;
            int availWidth = _rootPanel.ClientSize.Width - 48; // Padding
            
            if (_heroPanel != null) _heroPanel.Width = availWidth;
            if (_statsPanel != null) _statsPanel.Width = availWidth;
            if (_statsGrid != null) _statsGrid.Width = availWidth;
            if (_contentGrid != null) _contentGrid.Width = availWidth;
            if (_liveCard != null) _liveCard.Width = availWidth;
        }

        private void UpdateEmptyStates()
        {
            // Handled in RefreshSummaryUI
        }

        private string FormatDuration(long seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }

        private void HeroPanel_Resize(object sender, EventArgs e) => ThemeService.ApplyRounding(_heroPanel, 20);
        private void TopAppsCard_Resize(object sender, EventArgs e) => ThemeService.ApplyRounding(_topAppsCard, 18);
        private void InsightsCard_Resize(object sender, EventArgs e) => ThemeService.ApplyRounding(_insightsCard, 18);
    }
}
