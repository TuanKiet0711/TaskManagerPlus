using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TaskManagerPlus.Models;
using TaskManagerPlus.Services;

namespace TaskManagerPlus.Controls
{
    public class HealthInsightsForm : Form
    {
        private HealthScoreService _healthService;
        private ProcessMonitor _monitor;
        private Label lblTitle;
        private Label lblScore;
        private Label lblDetails;
        private Label lblRecommendation;
        private Button btnAction;
        private Button btnClose;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        public HealthInsightsForm(HealthScoreService healthService, ProcessMonitor monitor)
        {
            _healthService = healthService;
            _monitor = monitor;
            InitializeComponent();
            PopulateData();
            ThemeService.ApplyRounding(this, 12);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(400, 320);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = ThemeService.Surface;

            lblTitle = new Label
            {
                Text = LocalizationService.CurrentLanguage == AppLanguage.VI ? "🔍 Phân Tích Sức Khoẻ" : "🔍 Health Insights",
                Font = ThemeService.HeaderFont,
                ForeColor = ThemeService.Text,
                Location = new Point(20, 20),
                AutoSize = true
            };
            this.Controls.Add(lblTitle);

            lblScore = new Label
            {
                Font = new Font("Segoe UI", 36F, FontStyle.Bold),
                Location = new Point(20, 60),
                AutoSize = true
            };
            this.Controls.Add(lblScore);

            lblDetails = new Label
            {
                Font = ThemeService.MainFont,
                ForeColor = ThemeService.TextMuted,
                Location = new Point(20, 130),
                Size = new Size(360, 80),
                AutoEllipsis = true
            };
            this.Controls.Add(lblDetails);

            lblRecommendation = new Label
            {
                Font = ThemeService.BoldFont,
                ForeColor = ThemeService.Warning,
                Location = new Point(20, 210),
                Size = new Size(360, 40),
                AutoEllipsis = true
            };
            this.Controls.Add(lblRecommendation);

            btnAction = new Button
            {
                Size = new Size(160, 40),
                Location = new Point(20, 260),
                FlatStyle = FlatStyle.Flat,
                Font = ThemeService.BoldFont,
                BackColor = ThemeService.Primary,
                ForeColor = Color.White,
                Visible = false
            };
            btnAction.FlatAppearance.BorderSize = 0;
            btnAction.Click += BtnAction_Click;
            ThemeService.ApplyRounding(btnAction, 8);
            this.Controls.Add(btnAction);

            btnClose = new Button
            {
                Text = LocalizationService.CurrentLanguage == AppLanguage.VI ? "Đóng" : "Close",
                Size = new Size(160, 40),
                Location = new Point(220, 260),
                FlatStyle = FlatStyle.Flat,
                Font = ThemeService.BoldFont,
                BackColor = ThemeService.SurfaceAlt,
                ForeColor = ThemeService.Text
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();
            ThemeService.ApplyRounding(btnClose, 8);
            this.Controls.Add(btnClose);

            this.MouseDown += Draggable_MouseDown;
            lblTitle.MouseDown += Draggable_MouseDown;

            this.ResumeLayout(false);
        }

        private void PopulateData()
        {
            var dataLine = _healthService.GetLastComputedData();
            if (dataLine == null)
            {
                lblScore.Text = "N/A";
                return;
            }

            int score = (int)dataLine.TotalScore;
            lblScore.Text = $"{score}%";

            if (score >= 80) lblScore.ForeColor = ThemeService.Success;
            else if (score >= 50) lblScore.ForeColor = ThemeService.Warning;
            else lblScore.ForeColor = ThemeService.Danger;

            bool isVi = LocalizationService.CurrentLanguage == AppLanguage.VI;

            string details = isVi 
                ? $"CPU: {dataLine.CpuFactor:F1}% phạt | RAM: {dataLine.MemoryFactor:F1}% phạt\nNhiệt độ: {dataLine.TempFactor:F1}% phạt"
                : $"CPU Penalty: {dataLine.CpuFactor:F1}% | RAM Penalty: {dataLine.MemoryFactor:F1}%\nTemp Penalty: {dataLine.TempFactor:F1}%";

            lblDetails.Text = details;

            // Generate recommendations
            if (dataLine.CpuFactor > 15 || dataLine.TempFactor > 15)
            {
                lblRecommendation.Text = isVi ? "Khuyên dùng: Bật chế độ Tiết Kiệm Pin (Eco Mode) cho toàn bộ ứng dụng!" : "Suggestion: Enable Eco Mode for all draining apps!";
                btnAction.Text = isVi ? "🌱 Bật Tiết Kiệm" : "🌱 Enable Eco";
                btnAction.Tag = "EcoMode";
                btnAction.BackColor = ThemeService.Success;
                btnAction.Visible = true;
            }
            else if (dataLine.MemoryFactor > 15)
            {
                lblRecommendation.Text = isVi ? "Khuyên dùng: Dọn dẹp RAM rác khỏi bộ nhớ!" : "Suggestion: Clean unused RAM memory!";
                btnAction.Text = isVi ? "🧹 Dọn RAM" : "🧹 Clean RAM";
                btnAction.Tag = "CleanRAM";
                btnAction.BackColor = ThemeService.Warning;
                btnAction.Visible = true;
            }
            else
            {
                lblRecommendation.ForeColor = ThemeService.Success;
                lblRecommendation.Text = isVi ? "Hệ thống đang hoạt động trong trạng thái lí tưởng!" : "System is running optimally!";
            }
        }

        private void BtnAction_Click(object sender, EventArgs e)
        {
            string action = btnAction.Tag?.ToString();
            if (action == "EcoMode")
            {
                int pCount = 0;
                foreach (var p in _monitor.GetAllProcesses(true))
                {
                    if (p.CpuUsage > 5.0)
                    {
                        try { _monitor.ChangePriority(p.ProcessId, System.Diagnostics.ProcessPriorityClass.Idle); pCount++; } catch { }
                    }
                }
                string msg = LocalizationService.CurrentLanguage == AppLanguage.VI ? $"Trạng thái Tiết Kiệm áp dụng cho {pCount} ứng dụng." : $"Eco mode applied to {pCount} apps.";
                MessageBox.Show(msg, "Action Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (action == "CleanRAM")
            {
                int freed = _monitor.OptimizeMemory();
                string msg = LocalizationService.CurrentLanguage == AppLanguage.VI ? $"Đã dọn dẹp {freed} MB RAM rác!" : $"Cleaned {freed} MB of unused RAM!";
                MessageBox.Show(msg, "Memory Optimizer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            this.Close();
        }

        private void Draggable_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
    }
}
