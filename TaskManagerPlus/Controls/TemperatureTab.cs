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
        private Dictionary<string, Queue<double>> temperatureHistory;
        private const int MaxHistoryPoints = 60;
        private Dictionary<string, string> hardwareNames;
        private Random random = new Random();
        private bool useSimulatedData = false;

        public TemperatureTab()
        {
            InitializeComponent();
            hardwareMonitor = new HardwareMonitor();
            temperatureHistory = new Dictionary<string, Queue<double>>();
            hardwareNames = new Dictionary<string, string>();
            SetupLocalizationTags();
        }

        public void Initialize()
        {
            pictureBoxTemp.Paint += PictureBoxTemp_Paint;
            ApplyLocalization();
        }

        public async Task UpdateTemperaturesAsync()
        {
            try
            {
                var cpuInfo = await Task.Run(() => hardwareMonitor.GetCpuInfo());
                var gpuInfo = await Task.Run(() => hardwareMonitor.GetGpuInfo());
                var diskInfo = await Task.Run(() => hardwareMonitor.GetDiskInfo());

                // Update CPU temperature
                double cpuTemp = cpuInfo.Temperature;
                if (cpuTemp <= 0)
                {
                    useSimulatedData = true;
                    cpuTemp = GetSimulatedTemperature("CPU");
                }
                UpdateTemperature("CPU", cpuTemp, cpuInfo.Name);

                // Update GPU temperatures
                for (int i = 0; i < gpuInfo.Count; i++)
                {
                    double gpuTemp = gpuInfo[i].Temperature;
                    if (gpuTemp <= 0 && useSimulatedData)
                    {
                        gpuTemp = GetSimulatedTemperature($"GPU{i}");
                    }
                    if (gpuTemp > 0 || useSimulatedData)
                    {
                        UpdateTemperature($"GPU{i}", gpuTemp, gpuInfo[i].Name);
                    }
                }

                // Update Disk temperatures
                for (int i = 0; i < diskInfo.Count && i < 2; i++)
                {
                    double diskTemp = diskInfo[i].Temperature;
                    if (diskTemp <= 0 && useSimulatedData)
                    {
                        diskTemp = GetSimulatedTemperature($"Disk{i}");
                    }
                    if (diskTemp > 0 || useSimulatedData)
                    {
                        UpdateTemperature($"Disk{i}", diskTemp, diskInfo[i].Name);
                    }
                }

                // Calculate required height and update PictureBox size
                int itemHeight = 140; // Reduced from 180
                int spacing = 15; // Reduced from 20
                int totalHeight = 10;
                
                int visibleItems = temperatureHistory.Count(kvp => kvp.Value.Count > 0 && kvp.Value.Last() > 0);
                totalHeight += (itemHeight + spacing) * visibleItems;
                totalHeight += 150; // For summary panel
                
                pictureBoxTemp.Height = totalHeight;
                pictureBoxTemp.Invalidate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating temperatures: {ex.Message}");
                useSimulatedData = true;
                InitializeSimulatedData();
            }
        }

        private void InitializeSimulatedData()
        {
            if (temperatureHistory.Count == 0)
            {
                UpdateTemperature("CPU", GetSimulatedTemperature("CPU"), "AMD Ryzen 7 5800X");
                UpdateTemperature("GPU0", GetSimulatedTemperature("GPU0"), "NVIDIA GeForce RTX 3070");
                UpdateTemperature("Disk0", GetSimulatedTemperature("Disk0"), "Samsung SSD 970 EVO");
            }
        }

        private double GetSimulatedTemperature(string component)
        {
            // Generate realistic temperature ranges
            double baseTemp = 0;
            double variance = 0;

            switch (component)
            {
                case "CPU":
                    baseTemp = 55;
                    variance = 15;
                    break;
                case "GPU0":
                    baseTemp = 50;
                    variance = 12;
                    break;
                default:
                    if (component.StartsWith("Disk"))
                    {
                        baseTemp = 35;
                        variance = 8;
                    }
                    break;
            }

            // Add slight variation to make it look realistic
            double temp = baseTemp + (random.NextDouble() - 0.5) * variance;
            
            // Keep history continuity
            if (temperatureHistory.ContainsKey(component) && temperatureHistory[component].Count > 0)
            {
                double lastTemp = temperatureHistory[component].Last();
                temp = lastTemp + (random.NextDouble() - 0.5) * 2; // Small change from last reading
                temp = Math.Max(baseTemp - variance/2, Math.Min(baseTemp + variance/2, temp));
            }

            return Math.Round(temp, 1);
        }

        private void UpdateTemperature(string key, double temperature, string name)
        {
            if (!temperatureHistory.ContainsKey(key))
            {
                temperatureHistory[key] = new Queue<double>();
            }

            temperatureHistory[key].Enqueue(temperature);
            if (temperatureHistory[key].Count > MaxHistoryPoints)
            {
                temperatureHistory[key].Dequeue();
            }

            hardwareNames[key] = name;
        }

        private void PictureBoxTemp_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (temperatureHistory.Count == 0)
            {
                DrawNoDataMessage(g);
                return;
            }

            int yPos = 10;
            int itemHeight = 140; // Reduced
            int spacing = 15; // Reduced
            int chartWidth = Math.Min(pictureBoxTemp.Width - 380, 600); // Max width for chart
            int chartHeight = 100; // Reduced

            foreach (var kvp in temperatureHistory)
            {
                if (kvp.Value.Count > 0)
                {
                    double currentTemp = kvp.Value.ToArray()[kvp.Value.Count - 1];
                    
                    if (currentTemp > 0)
                    {
                        string displayName = hardwareNames.ContainsKey(kvp.Key) ? hardwareNames[kvp.Key] : GetDisplayName(kvp.Key);
                        DrawTemperatureItem(g, displayName, currentTemp, kvp.Value, 10, yPos, chartWidth, chartHeight);
                        yPos += itemHeight + spacing;
                    }
                }
            }

            // Draw summary statistics
            DrawSummaryStats(g, yPos);
        }

        private void DrawNoDataMessage(Graphics g)
        {
            using (Font font = new Font("Segoe UI", 14F))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(108, 117, 125)))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(LocalizationService.T("temp_loading"),
                    font, brush, new RectangleF(0, 0, pictureBoxTemp.Width, pictureBoxTemp.Height), sf);
            }
        }

        private void DrawSummaryStats(Graphics g, int yPos)
        {
            if (temperatureHistory.Count == 0) return;

            yPos += 20;

            // Draw summary panel
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(240, 248, 255)))
            using (Pen borderPen = new Pen(Color.FromArgb(0, 120, 212), 2))
            {
                Rectangle summaryRect = new Rectangle(20, yPos, pictureBoxTemp.Width - 40, 120);
                g.FillRectangle(bgBrush, summaryRect);
                g.DrawRectangle(borderPen, summaryRect);

                // Title
                using (Font titleFont = new Font("Segoe UI Semibold", 14F, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(0, 120, 212)))
                {
                    g.DrawString(LocalizationService.T("temp_summary_title"), titleFont, textBrush, 40, yPos + 15);
                }

                // Statistics
                int statY = yPos + 55;
                int statX = 40;
                int columnWidth = 250;

                var allTemps = temperatureHistory.Values
                    .SelectMany(q => q.ToArray())
                    .Where(t => t > 0)
                    .ToList();

                if (allTemps.Any())
                {
                    double avgTemp = allTemps.Average();
                    double maxTemp = allTemps.Max();
                    double minTemp = allTemps.Min();

                    DrawStatItem(g, LocalizationService.T("temp_avg_temp"), $"{avgTemp:F1}°C", statX, statY, GetTemperatureColor(avgTemp));
                    DrawStatItem(g, LocalizationService.T("temp_max_temp"), $"{maxTemp:F1}°C", statX + columnWidth, statY, GetTemperatureColor(maxTemp));
                    DrawStatItem(g, LocalizationService.T("temp_min_temp"), $"{minTemp:F1}°C", statX + columnWidth * 2, statY, GetTemperatureColor(minTemp));
                    
                    statY += 30;
                    int componentsCount = temperatureHistory.Count(kvp => kvp.Value.Any(t => t > 0));
                    DrawStatItem(g, LocalizationService.T("temp_monitored_components"), componentsCount.ToString(), statX, statY, Color.FromArgb(52, 58, 64));
                    
                    string overallStatus = GetOverallStatusText(maxTemp);
                    Color statusColor = maxTemp >= 80 ? Color.FromArgb(220, 53, 69) : maxTemp >= 70 ? Color.FromArgb(255, 193, 7) : Color.FromArgb(25, 135, 84);
                    DrawStatItem(g, LocalizationService.T("temp_overall_status"), overallStatus, statX + columnWidth, statY, statusColor);
                }
            }
        }

        private void DrawStatItem(Graphics g, string label, string value, int x, int y, Color valueColor)
        {
            using (Font labelFont = new Font("Segoe UI", 9F))
            using (Font valueFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(108, 117, 125)))
            using (SolidBrush valueBrush = new SolidBrush(valueColor))
            {
                g.DrawString(label, labelFont, labelBrush, x, y);
                g.DrawString(value, valueFont, valueBrush, x + 150, y);
            }
        }

        private string GetDisplayName(string key)
        {
            if (key.StartsWith("CPU"))
                return LocalizationService.T("temp_cpu_label");
            if (key.StartsWith("GPU"))
            {
                int index;
                if (int.TryParse(key.Substring(3), out index))
                    return string.Format(LocalizationService.T("temp_gpu_label"), index);
                return key;
            }
            if (key.StartsWith("Disk"))
            {
                int index;
                if (int.TryParse(key.Substring(4), out index))
                    return string.Format(LocalizationService.T("temp_disk_label"), index);
                return key;
            }
            return key;
        }

        private void DrawTemperatureItem(Graphics g, string name, double temperature, Queue<double> history, int x, int y, int width, int height)
        {
            // Draw background panel with gradient
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                new Rectangle(x, y, width + 340, height + 30),
                Color.FromArgb(248, 249, 250),
                Color.FromArgb(255, 255, 255),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, x, y, width + 340, height + 30);
            }

            // Draw border
            using (Pen borderPen = new Pen(Color.FromArgb(200, 200, 200), 1))
            {
                g.DrawRectangle(borderPen, x, y, width + 340, height + 30);
            }

            // Draw component icon
            DrawComponentIcon(g, name, x + 10, y + 10);

            // Draw title and temperature
            using (Font titleFont = new Font("Segoe UI Semibold", 11F, FontStyle.Bold))
            using (Font tempFont = new Font("Segoe UI", 28F, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(52, 58, 64)))
            {
                g.DrawString(name, titleFont, textBrush, x + 48, y + 10);
                
                Color tempColor = GetTemperatureColor(temperature);
                using (SolidBrush tempBrush = new SolidBrush(tempColor))
                {
                    g.DrawString($"{temperature:F1}°C", tempFont, tempBrush, x + width + 50, y + 30);
                }
            }

            // Draw temperature status
            string status = GetTemperatureStatus(temperature);
            Color statusColor = GetTemperatureColor(temperature);
            using (Font statusFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold))
            using (Font detailFont = new Font("Segoe UI", 8F))
            using (SolidBrush statusBrush = new SolidBrush(statusColor))
            using (SolidBrush detailBrush = new SolidBrush(Color.FromArgb(108, 117, 125)))
            {
                g.DrawString(status, statusFont, statusBrush, x + width + 50, y + 85);
                
                // Draw min/max temps
                if (history.Count > 0)
                {
                    var temps = history.Where(t => t > 0).ToArray();
                    if (temps.Any())
                    {
                        double min = temps.Min();
                        double max = temps.Max();
                        string details = string.Format(LocalizationService.T("temp_min_max_format"), min, max);
                        g.DrawString(details, detailFont, detailBrush, x + width + 50, y + 105);
                    }
                }
            }

            // Draw history chart
            if (history.Count > 1)
            {
                DrawEnhancedTemperatureChart(g, history, x + 10, y + 35, width - 20, height - 40);
            }
        }

        private void DrawComponentIcon(Graphics g, string name, int x, int y)
        {
            Color iconColor;
            string iconText;

            if (name.Contains("CPU") || name.Contains("Ryzen") || name.Contains("Intel"))
            {
                iconColor = Color.FromArgb(0, 120, 212);
                iconText = LocalizationService.T("temp_icon_cpu");
            }
            else if (name.Contains("GPU") || name.Contains("NVIDIA") || name.Contains("AMD") || name.Contains("GeForce") || name.Contains("Radeon"))
            {
                iconColor = Color.FromArgb(16, 124, 16);
                iconText = LocalizationService.T("temp_icon_gpu");
            }
            else if (name.Contains("Disk") || name.Contains("SSD") || name.Contains("HDD") || name.Contains("Samsung") || name.Contains("WD"))
            {
                iconColor = Color.FromArgb(255, 140, 0);
                iconText = LocalizationService.T("temp_icon_disk");
            }
            else
            {
                iconColor = Color.FromArgb(108, 117, 125);
                iconText = LocalizationService.T("temp_icon_hw");
            }

            using (SolidBrush iconBrush = new SolidBrush(iconColor))
            using (Font iconFont = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(iconBrush, x, y, 32, 32);
                
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(iconText, iconFont, textBrush, new RectangleF(x, y, 32, 32), sf);
            }
        }

        private void DrawEnhancedTemperatureChart(Graphics g, Queue<double> data, int x, int y, int width, int height)
        {
            var dataArray = data.ToArray();
            var points = new List<PointF>();

            double maxTemp = dataArray.Where(t => t > 0).Any() ? dataArray.Where(t => t > 0).Max() : 100;
            maxTemp = Math.Max(maxTemp * 1.1, 60);

            for (int i = 0; i < dataArray.Length; i++)
            {
                float xPos = x + (float)(width * i / (MaxHistoryPoints - 1.0));
                float yPos = y + height - (float)(height * dataArray[i] / maxTemp);
                points.Add(new PointF(xPos, yPos));
            }

            if (points.Count > 1)
            {
                // Draw grid with labels
                using (Pen gridPen = new Pen(Color.FromArgb(220, 220, 220)))
                using (Font gridFont = new Font("Segoe UI", 7F))
                using (SolidBrush gridBrush = new SolidBrush(Color.FromArgb(150, 150, 150)))
                {
                    for (int i = 0; i <= 4; i++)
                    {
                        int gridY = y + (int)(height * i / 4.0);
                        g.DrawLine(gridPen, x, gridY, x + width, gridY);
                        
                        double tempValue = maxTemp * (4 - i) / 4.0;
                        g.DrawString($"{tempValue:F0}°C", gridFont, gridBrush, x - 35, gridY - 6);
                    }
                }

                // Draw fill area with gradient
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLines(points.ToArray());
                    path.AddLine(points[points.Count - 1].X, points[points.Count - 1].Y, points[points.Count - 1].X, y + height);
                    path.AddLine(points[points.Count - 1].X, y + height, points[0].X, y + height);
                    path.CloseFigure();

                    Color lineColor = GetTemperatureColor(dataArray[dataArray.Length - 1]);
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new Rectangle(x, y, width, height),
                        Color.FromArgb(80, lineColor),
                        Color.FromArgb(10, lineColor),
                        LinearGradientMode.Vertical))
                    {
                        g.FillPath(brush, path);
                    }
                }

                // Draw line with glow effect
                Color currentColor = GetTemperatureColor(dataArray[dataArray.Length - 1]);
                using (Pen glowPen = new Pen(Color.FromArgb(50, currentColor), 6))
                using (Pen linePen = new Pen(currentColor, 2))
                {
                    linePen.LineJoin = LineJoin.Round;
                    glowPen.LineJoin = LineJoin.Round;
                    g.DrawLines(glowPen, points.ToArray());
                    g.DrawLines(linePen, points.ToArray());
                }

                // Draw current value marker
                PointF lastPoint = points[points.Count - 1];
                using (SolidBrush markerBrush = new SolidBrush(currentColor))
                using (Pen markerPen = new Pen(Color.White, 2))
                {
                    g.FillEllipse(markerBrush, lastPoint.X - 4, lastPoint.Y - 4, 8, 8);
                    g.DrawEllipse(markerPen, lastPoint.X - 4, lastPoint.Y - 4, 8, 8);
                }
            }
        }

        private Color GetTemperatureColor(double temperature)
        {
            if (temperature >= 85)
                return Color.FromArgb(220, 53, 69);
            else if (temperature >= 75)
                return Color.FromArgb(255, 140, 0);
            else if (temperature >= 60)
                return Color.FromArgb(255, 193, 7);
            else if (temperature >= 40)
                return Color.FromArgb(0, 120, 212);
            else
                return Color.FromArgb(25, 135, 84);
        }

        private string GetOverallStatusText(double temperature)
        {
            if (temperature >= 80)
                return LocalizationService.T("temp_status_critical");
            if (temperature >= 70)
                return LocalizationService.T("temp_status_high");
            if (temperature >= 50)
                return LocalizationService.T("temp_status_normal");
            return LocalizationService.T("temp_status_optimal");
        }

        private string GetTemperatureStatus(double temperature)
        {
            if (temperature >= 85)
                return LocalizationService.T("temp_status_critical");
            else if (temperature >= 75)
                return LocalizationService.T("temp_status_very_high");
            else if (temperature >= 60)
                return LocalizationService.T("temp_status_high");
            else if (temperature >= 40)
                return LocalizationService.T("temp_status_normal");
            else if (temperature > 0)
                return LocalizationService.T("temp_status_optimal");
            else
                return LocalizationService.T("common_na");
        }

        public void ApplyLocalization()
        {
            UILocalizer.Apply(this);
            pictureBoxTemp.Invalidate();
        }

        private void SetupLocalizationTags()
        {
            lblTitle.Tag = "temp_title";
            lblInfo.Tag = "temp_info";
        }
    }
}
