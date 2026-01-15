using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Services;

namespace TaskManagerPlus.Controls
{
    public partial class BatteryTab : UserControl
    {
        private BatteryInfo currentBatteryInfo;
        private Timer updateTimer;

        public BatteryTab()
        {
            InitializeComponent();
            currentBatteryInfo = new BatteryInfo();
            SetupLocalizationTags();

            updateTimer = new Timer();
            updateTimer.Interval = 2000; // Update every 2 seconds
            updateTimer.Tick += async (s, e) => await UpdateBatteryInfoAsync();
        }

        public void Initialize()
        {
            pictureBoxBattery.Paint += PictureBoxBattery_Paint;
            updateTimer.Start();
            ApplyLocalization();
        }

        public async Task UpdateBatteryInfoAsync()
        {
            try
            {
                currentBatteryInfo = await Task.Run(() => GetBatteryInfo());
                pictureBoxBattery.Invalidate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating battery: {ex.Message}");
            }
        }

        private BatteryInfo GetBatteryInfo()
        {
            var info = new BatteryInfo();

            try
            {
                // Basic info from Windows
                PowerStatus powerStatus = SystemInformation.PowerStatus;
                info.ChargePercent = (int)(powerStatus.BatteryLifePercent * 100);
                info.IsCharging = powerStatus.PowerLineStatus == PowerLineStatus.Online;
                info.TimeRemaining = powerStatus.BatteryLifeRemaining;

                // If device has no battery, BatteryLifePercent can be 1.0 and BatteryChargeStatus may show NoSystemBattery
                if ((powerStatus.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == BatteryChargeStatus.NoSystemBattery)
                {
                    info.ErrorMessage = "No battery detected";
                    return info;
                }

                // 1) Try to get accurate capacity/health/cycle from root\WMI
                TryReadBatteryFromRootWmi(info);

                // 2) Fallback: Win32_Battery (often limited)
                if (info.DesignCapacity <= 0 || info.FullChargeCapacity <= 0)
                {
                    TryReadBatteryFromWin32Battery(info);
                }

                // 3) Compute health / wear / condition
                ComputeHealthWearCondition(info);

                // 4) Power plan
                TryReadPowerPlan(info);
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
            }

            return info;
        }

        private void TryReadBatteryFromRootWmi(BatteryInfo info)
        {
            try
            {
                // Some machines expose these in root\WMI
                var scope = new ManagementScope(@"\\.\root\WMI");
                scope.Connect();

                // Designed capacity
                using (var searchStatic = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM BatteryStaticData")))
                {
                    foreach (ManagementObject obj in searchStatic.Get())
                    {
                        // Units are typically mWh
                        info.DesignCapacity = ToInt(obj["DesignedCapacity"]);
                        break; // usually one battery; if multiple you can sum instead
                    }
                }

                // Full charged capacity
                using (var searchFull = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM BatteryFullChargedCapacity")))
                {
                    foreach (ManagementObject obj in searchFull.Get())
                    {
                        info.FullChargeCapacity = ToInt(obj["FullChargedCapacity"]);
                        break;
                    }
                }

                // Cycle count
                using (var searchCycle = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM BatteryCycleCount")))
                {
                    foreach (ManagementObject obj in searchCycle.Get())
                    {
                        info.CycleCount = ToInt(obj["CycleCount"]);
                        break;
                    }
                }

                // Battery status / voltage (optional)
                using (var searchStatus = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM BatteryStatus")))
                {
                    foreach (ManagementObject obj in searchStatus.Get())
                    {
                        // Voltage often in mV
                        int mv = ToInt(obj["Voltage"]);
                        if (mv > 0) info.Voltage = mv / 1000.0;

                        // RemainingCapacity sometimes available (mWh)
                        info.RemainingCapacity = ToInt(obj["RemainingCapacity"]);

                        // ChargeRate sometimes available (mW)
                        info.ChargeRate = ToInt(obj["ChargeRate"]);
                        break;
                    }
                }
            }
            catch
            {
                // Ignore: not supported on some hardware
            }
        }

        private void TryReadBatteryFromWin32Battery(BatteryInfo info)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        info.BatteryName = obj["Name"]?.ToString() ?? info.BatteryName;
                        info.BatteryStatus = obj["BatteryStatus"]?.ToString() ?? info.BatteryStatus;

                        // These two are often missing (0/null)
                        info.DesignCapacity = Math.Max(info.DesignCapacity, ToInt(obj["DesignCapacity"]));
                        info.FullChargeCapacity = Math.Max(info.FullChargeCapacity, ToInt(obj["FullChargeCapacity"]));

                        info.EstimatedChargeRemaining = ToInt(obj["EstimatedChargeRemaining"]);

                        // DesignVoltage often in mV
                        int mv = ToInt(obj["DesignVoltage"]);
                        if (info.Voltage <= 0 && mv > 0) info.Voltage = mv / 1000.0;

                        break;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private void TryReadPowerPlan(BatteryInfo info)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    @"root\CIMV2\power",
                    "SELECT * FROM Win32_PowerPlan WHERE IsActive=True"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        info.PowerPlan = obj["ElementName"]?.ToString() ?? "Unknown";
                        break;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private void ComputeHealthWearCondition(BatteryInfo info)
        {
            if (info.DesignCapacity > 0 && info.FullChargeCapacity > 0)
            {
                info.BatteryHealth = (int)Math.Round((double)info.FullChargeCapacity / info.DesignCapacity * 100);
                info.BatteryHealth = Math.Max(0, Math.Min(100, info.BatteryHealth));

                info.WearLevel = 100 - info.BatteryHealth;
                info.WearLevel = Math.Max(0, Math.Min(100, info.WearLevel));

                if (info.BatteryHealth >= 90)
                    info.BatteryCondition = "battery_condition_excellent";
                else if (info.BatteryHealth >= 80)
                    info.BatteryCondition = "battery_condition_good";
                else if (info.BatteryHealth >= 60)
                    info.BatteryCondition = "battery_condition_fair";
                else if (info.BatteryHealth >= 40)
                    info.BatteryCondition = "battery_condition_poor";
                else
                    info.BatteryCondition = "battery_condition_replace_soon";
            }
            else
            {
                // If cannot read capacity, at least show unknown without crashing
                info.BatteryHealth = 0;
                info.WearLevel = 0;
                info.BatteryCondition = "battery_condition_unknown";
            }
        }

        private int ToInt(object value)
        {
            try
            {
                if (value == null) return 0;
                if (value is int i) return i;
                if (value is uint ui) return unchecked((int)ui);
                if (value is long l) return (int)l;
                if (value is ulong ul) return (int)ul;
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private void PictureBoxBattery_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (!string.IsNullOrEmpty(currentBatteryInfo.ErrorMessage))
            {
                DrawNoBatteryMessage(g);
                return;
            }

            int centerX = pictureBoxBattery.Width / 2;
            int yPos = 20;

            // Draw large battery icon
            DrawBatteryIcon(g, centerX - 100, yPos, 200, 120, currentBatteryInfo.ChargePercent);
            yPos += 140;

            // Draw charge percentage
            using (Font largeFont = new Font("Segoe UI", 48F, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(GetBatteryColor(currentBatteryInfo.ChargePercent)))
            {
                string percentText = $"{currentBatteryInfo.ChargePercent}%";
                SizeF textSize = g.MeasureString(percentText, largeFont);
                g.DrawString(percentText, largeFont, textBrush, centerX - textSize.Width / 2, yPos);
            }
            yPos += 70;

            // Draw status
            string status = currentBatteryInfo.IsCharging
                ? LocalizationService.T("battery_status_charging")
                : LocalizationService.T("battery_status_on_battery");
            using (Font statusFont = new Font("Segoe UI", 16F, FontStyle.Bold))
            using (SolidBrush statusBrush = new SolidBrush(currentBatteryInfo.IsCharging ? Color.FromArgb(25, 135, 84) : Color.FromArgb(52, 58, 64)))
            {
                SizeF statusSize = g.MeasureString(status, statusFont);
                g.DrawString(status, statusFont, statusBrush, centerX - statusSize.Width / 2, yPos);
            }
            yPos += 40;

            // Show battery health + wear prominently (chai pin)
            if (currentBatteryInfo.BatteryHealth > 0)
            {
                string healthText = string.Format(LocalizationService.T("battery_health_summary_format"),
                    currentBatteryInfo.BatteryHealth,
                    currentBatteryInfo.WearLevel,
                    LocalizationService.T(currentBatteryInfo.BatteryCondition));
                Color healthColor = currentBatteryInfo.BatteryHealth >= 80 ? Color.FromArgb(25, 135, 84) :
                                   currentBatteryInfo.BatteryHealth >= 60 ? Color.FromArgb(255, 193, 7) :
                                   Color.FromArgb(220, 53, 69);

                using (Font healthFont = new Font("Segoe UI Semibold", 14F, FontStyle.Bold))
                using (SolidBrush healthBrush = new SolidBrush(healthColor))
                {
                    SizeF healthSize = g.MeasureString(healthText, healthFont);
                    g.DrawString(healthText, healthFont, healthBrush, centerX - healthSize.Width / 2, yPos);
                }
                yPos += 35;
            }

            // Draw time remaining
            if (!currentBatteryInfo.IsCharging && currentBatteryInfo.TimeRemaining > 0)
            {
                TimeSpan time = TimeSpan.FromSeconds(currentBatteryInfo.TimeRemaining);
                string timeText = string.Format(LocalizationService.T("battery_time_remaining_format"),
                    time.Hours, time.Minutes);
                using (Font timeFont = new Font("Segoe UI", 12F))
                using (SolidBrush timeBrush = new SolidBrush(Color.FromArgb(108, 117, 125)))
                {
                    SizeF timeSize = g.MeasureString(timeText, timeFont);
                    g.DrawString(timeText, timeFont, timeBrush, centerX - timeSize.Width / 2, yPos);
                }
                yPos += 40;
            }

            // Draw details panel
            DrawDetailsPanel(g, 50, yPos, pictureBoxBattery.Width - 100, 320);
        }

        private void DrawBatteryIcon(Graphics g, int x, int y, int width, int height, int chargePercent)
        {
            Rectangle batteryBody = new Rectangle(x + 10, y, width - 20, height);
            Rectangle batteryTip = new Rectangle(x + width - 10, y + height / 3, 10, height / 3);

            using (Pen outlinePen = new Pen(Color.FromArgb(100, 100, 100), 4))
            {
                g.DrawRectangle(outlinePen, batteryBody);
                g.FillRectangle(Brushes.Gray, batteryTip);
            }

            int chargeHeight = (int)(batteryBody.Height * chargePercent / 100.0);
            int chargeY = batteryBody.Bottom - chargeHeight;

            // Prevent negative rect height when battery is very low
            int innerHeight = Math.Max(0, chargeHeight - 10);

            Rectangle chargeRect = new Rectangle(batteryBody.X + 5, chargeY + 5, batteryBody.Width - 10, innerHeight);

            if (chargeRect.Height > 0)
            {
                Color chargeColor = GetBatteryColor(chargePercent);
                using (LinearGradientBrush chargeBrush = new LinearGradientBrush(
                    chargeRect,
                    Color.FromArgb(200, chargeColor),
                    chargeColor,
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(chargeBrush, chargeRect);
                }
            }
        }

        private void DrawDetailsPanel(Graphics g, int x, int y, int width, int height)
        {
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(248, 249, 250)))
            using (Pen borderPen = new Pen(Color.FromArgb(200, 200, 200)))
            {
                g.FillRectangle(bgBrush, x, y, width, height);
                g.DrawRectangle(borderPen, x, y, width, height);
            }

            int yPos = y + 20;
            int leftCol = x + 30;
            int rightCol = x + width / 2 + 30;

            using (Font titleFont = new Font("Segoe UI Semibold", 14F, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(0, 120, 212)))
            {
                g.DrawString(LocalizationService.T("battery_details_title"), titleFont, titleBrush, leftCol, yPos);
            }
            yPos += 45;

            DrawDetail(g, LocalizationService.T("battery_name_label"), currentBatteryInfo.BatteryName, leftCol, yPos);
            string powerPlan = currentBatteryInfo.PowerPlan;
            if (string.IsNullOrWhiteSpace(powerPlan) || string.Equals(powerPlan, "Unknown", StringComparison.OrdinalIgnoreCase))
                powerPlan = LocalizationService.T("common_unknown");
            DrawDetail(g, LocalizationService.T("battery_power_plan_label"), powerPlan, rightCol, yPos);
            yPos += 35;

            // Chai pin + health
            DrawDetail(g, LocalizationService.T("battery_health_label"), currentBatteryInfo.BatteryHealth > 0
                ? $"{currentBatteryInfo.BatteryHealth}% ({LocalizationService.T(currentBatteryInfo.BatteryCondition)})"
                : LocalizationService.T("common_unknown"), leftCol, yPos);

            DrawDetail(g, LocalizationService.T("battery_wear_label"), currentBatteryInfo.BatteryHealth > 0
                ? $"{currentBatteryInfo.WearLevel}%"
                : LocalizationService.T("common_unknown"), rightCol, yPos);

            yPos += 35;

            if (currentBatteryInfo.DesignCapacity > 0)
            {
                DrawDetail(g, LocalizationService.T("battery_design_capacity_label"), $"{currentBatteryInfo.DesignCapacity} mWh", leftCol, yPos);
                DrawDetail(g, LocalizationService.T("battery_full_charge_capacity_label"), $"{currentBatteryInfo.FullChargeCapacity} mWh", rightCol, yPos);
                yPos += 35;
            }

            string voltageText = currentBatteryInfo.Voltage > 0 ? $"{currentBatteryInfo.Voltage:F2} V" : LocalizationService.T("common_unknown");
            DrawDetail(g, LocalizationService.T("battery_voltage_label"), voltageText, leftCol, yPos);

            DrawDetail(g, LocalizationService.T("battery_charge_status_label"),
                currentBatteryInfo.IsCharging
                    ? LocalizationService.T("battery_charge_status_charging")
                    : LocalizationService.T("battery_charge_status_discharging"),
                rightCol, yPos);
            yPos += 35;

            if (currentBatteryInfo.CycleCount >= 0)
            {
                DrawDetail(g, LocalizationService.T("battery_cycle_count_label"), currentBatteryInfo.CycleCount.ToString(), leftCol, yPos);
                yPos += 35;
            }

            if (currentBatteryInfo.RemainingCapacity > 0)
            {
                DrawDetail(g, LocalizationService.T("battery_remaining_capacity_label"), $"{currentBatteryInfo.RemainingCapacity} mWh", leftCol, yPos);
                if (currentBatteryInfo.ChargeRate != 0)
                    DrawDetail(g, LocalizationService.T("battery_charge_rate_label"), $"{currentBatteryInfo.ChargeRate} mW", rightCol, yPos);
                yPos += 35;
            }

            // Health bar
            yPos += 10;
            DrawHealthBar(g, leftCol, yPos, width - 60, currentBatteryInfo.BatteryHealth);

            // Wear bar (chai pin)
            yPos += 70;
            DrawWearBar(g, leftCol, yPos, width - 60, currentBatteryInfo.WearLevel);
        }

        private void DrawDetail(Graphics g, string label, string value, int x, int y)
        {
            using (Font labelFont = new Font("Segoe UI", 9F))
            using (Font valueFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(108, 117, 125)))
            using (SolidBrush valueBrush = new SolidBrush(Color.FromArgb(52, 58, 64)))
            {
                g.DrawString(label, labelFont, labelBrush, x, y);
                g.DrawString(value, valueFont, valueBrush, x, y + 15);
            }
        }

        private void DrawHealthBar(Graphics g, int x, int y, int width, int health)
        {
            using (Font labelFont = new Font("Segoe UI", 9F))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(108, 117, 125)))
            {
                g.DrawString(LocalizationService.T("battery_health_bar_label"), labelFont, labelBrush, x, y);
            }

            int barY = y + 25;
            int barHeight = 25;

            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(220, 220, 220)))
                g.FillRectangle(bgBrush, x, barY, width, barHeight);

            int h = Math.Max(0, Math.Min(100, health));
            int healthWidth = (int)(width * h / 100.0);

            Color healthColor = h >= 80 ? Color.FromArgb(25, 135, 84) :
                              h >= 50 ? Color.FromArgb(255, 193, 7) :
                              Color.FromArgb(220, 53, 69);

            using (SolidBrush healthBrush = new SolidBrush(healthColor))
                g.FillRectangle(healthBrush, x, barY, healthWidth, barHeight);

            using (Font percentFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                string healthText = h > 0 ? $"{h}%" : LocalizationService.T("common_na");
                SizeF textSize = g.MeasureString(healthText, percentFont);
                g.DrawString(healthText, percentFont, textBrush,
                    x + width / 2 - textSize.Width / 2, barY + 4);
            }
        }

        private void DrawWearBar(Graphics g, int x, int y, int width, int wear)
        {
            using (Font labelFont = new Font("Segoe UI", 9F))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(108, 117, 125)))
            {
                g.DrawString(LocalizationService.T("battery_wear_bar_label"), labelFont, labelBrush, x, y);
            }

            int barY = y + 25;
            int barHeight = 25;

            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(220, 220, 220)))
                g.FillRectangle(bgBrush, x, barY, width, barHeight);

            int w = Math.Max(0, Math.Min(100, wear));
            int wearWidth = (int)(width * w / 100.0);

            // Wear càng cao càng xấu: xanh -> vàng -> đỏ
            Color wearColor = w <= 20 ? Color.FromArgb(25, 135, 84) :
                             w <= 40 ? Color.FromArgb(255, 193, 7) :
                             Color.FromArgb(220, 53, 69);

            using (SolidBrush wearBrush = new SolidBrush(wearColor))
                g.FillRectangle(wearBrush, x, barY, wearWidth, barHeight);

            using (Font percentFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                string wearText = w > 0 ? $"{w}%" : LocalizationService.T("common_na");
                SizeF textSize = g.MeasureString(wearText, percentFont);
                g.DrawString(wearText, percentFont, textBrush,
                    x + width / 2 - textSize.Width / 2, barY + 4);
            }
        }

        private void DrawNoBatteryMessage(Graphics g)
        {
            using (Font font = new Font("Segoe UI", 14F))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(108, 117, 125)))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                g.DrawString(LocalizationService.T("battery_no_battery"),
                    font, brush, new RectangleF(0, 0, pictureBoxBattery.Width, pictureBoxBattery.Height), sf);
            }
        }

        private Color GetBatteryColor(int percent)
        {
            if (percent >= 50)
                return Color.FromArgb(25, 135, 84); // Green
            else if (percent >= 20)
                return Color.FromArgb(255, 193, 7); // Yellow
            else
                return Color.FromArgb(220, 53, 69); // Red
        }

        public void ApplyLocalization()
        {
            UILocalizer.Apply(this);
            pictureBoxBattery.Invalidate();
        }

        private void SetupLocalizationTags()
        {
            lblTitle.Tag = "battery_title";
            lblInfo.Tag = "battery_info";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                updateTimer?.Stop();
                updateTimer?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private class BatteryInfo
        {
            public string BatteryName { get; set; } = "Battery";
            public int ChargePercent { get; set; }
            public bool IsCharging { get; set; }
            public int TimeRemaining { get; set; }

            // Capacities (usually mWh)
            public int DesignCapacity { get; set; }
            public int FullChargeCapacity { get; set; }
            public int RemainingCapacity { get; set; }
            public int ChargeRate { get; set; }

            public int BatteryHealth { get; set; } = 0;
            public int WearLevel { get; set; } = 0;

            public string BatteryCondition { get; set; } = "battery_condition_unknown";
            public string BatteryStatus { get; set; } = "Unknown";
            public int EstimatedChargeRemaining { get; set; }

            public string PowerPlan { get; set; } = "Balanced";
            public double Voltage { get; set; }

            public int CycleCount { get; set; } = -1;

            public string ErrorMessage { get; set; }
        }
    }
}
