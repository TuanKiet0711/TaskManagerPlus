using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TaskManagerPlus.Services
{
    public static class ThemeService
    {
        // --- Core Colors ---
        public static readonly Color Primary = Color.FromArgb(37, 99, 235);
        public static readonly Color Secondary = Color.FromArgb(139, 92, 246);
        public static readonly Color Success = Color.FromArgb(16, 185, 129);
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);
        public static readonly Color Accent = Color.FromArgb(6, 182, 212);
        
        public static readonly Color Background = Color.FromArgb(243, 246, 251);
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color SurfaceAlt = Color.FromArgb(249, 250, 252);
        public static readonly Color Border = Color.FromArgb(218, 226, 236);
        
        public static readonly Color Text = Color.FromArgb(15, 23, 42);
        public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
        
        // --- Shared Fonts ---
        public static Font HeaderFont = new Font("Segoe UI", 18, FontStyle.Bold);
        public static Font CardTitleFont = new Font("Segoe UI", 12, FontStyle.Bold);
        public static Font MainFont = new Font("Segoe UI", 9.75f);
        public static Font SmallFont = new Font("Segoe UI", 8.75f);
        public static Font BoldFont = new Font("Segoe UI", 9.25f, FontStyle.Bold);

        // --- Styling Helpers ---
        public static void ApplyRounding(Control control, int radius = 16)
        {
            if (control == null || control.Width <= 0 || control.Height <= 0)
                return;

            int d = Math.Max(1, radius * 2);
            using (var path = new GraphicsPath())
            {
                var rect = new Rectangle(0, 0, control.Width, control.Height);

                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                control.Region = new Region(path);
            }
        }

        public static void ApplyCardStyle(Panel panel)
        {
            panel.BackColor = Surface;
            panel.Paint += (s, e) =>
            {
                var p = s as Panel;
                if (p == null) return;
                var rect = p.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                using (var pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, rect);
            };
        }

        public static void AttachBorder(Control control)
        {
            control.Paint += (s, e) =>
            {
                var rect = control.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                using (var pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, rect);
            };
        }

        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void SetDoubleBuffered(Control control)
        {
            typeof(Control).GetProperty("DoubleBuffered", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance)
                ?.SetValue(control, true);
        }
    }
}
