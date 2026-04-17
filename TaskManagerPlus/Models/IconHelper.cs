using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace TaskManagerPlus.Services
{
    public static class IconHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_LARGEICON = 0x000000000;

        // Known installation paths for common apps that don't show up in PATH
        private static readonly Dictionary<string, string[]> KnownAppPaths =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "code",         new[] { @"%LocalAppData%\Programs\Microsoft VS Code\Code.exe",  @"%ProgramFiles%\Microsoft VS Code\Code.exe" } },
            { "cursor",       new[] { @"%LocalAppData%\Programs\cursor\Cursor.exe", @"%LocalAppData%\cursor\Cursor.exe" } },
            { "windsurf",     new[] { @"%LocalAppData%\Programs\Windsurf\Windsurf.exe" } },
            { "antigravity",  new[] { @"%LocalAppData%\Programs\Antigravity\Antigravity.exe", @"%ProgramFiles%\Antigravity\Antigravity.exe" } },
            { "discord",      new[] { @"%LocalAppData%\Discord\Update.exe", @"%AppData%\Discord\Discord.exe" } },
            { "slack",        new[] { @"%LocalAppData%\slack\slack.exe", @"%ProgramFiles%\Slack\slack.exe" } },
            { "spotify",      new[] { @"%AppData%\Spotify\Spotify.exe" } },
            { "steam",        new[] { @"%ProgramFiles(x86)%\Steam\steam.exe", @"%ProgramFiles%\Steam\steam.exe" } },
            { "teams",        new[] { @"%LocalAppData%\Microsoft\Teams\current\Teams.exe", @"%AppData%\Microsoft\Teams\Teams.exe" } },
            { "zoom",         new[] { @"%AppData%\Zoom\bin\Zoom.exe", @"%ProgramFiles%\Zoom\bin\Zoom.exe" } },
            { "obs64",        new[] { @"%ProgramFiles%\obs-studio\bin\64bit\obs64.exe" } },
            { "obs32",        new[] { @"%ProgramFiles%\obs-studio\bin\32bit\obs32.exe" } },
            { "postman",      new[] { @"%LocalAppData%\Postman\Postman.exe", @"%AppData%\Postman\Postman.exe" } },
            { "figma",        new[] { @"%LocalAppData%\Figma\Figma.exe", @"%AppData%\Figma\Figma.exe" } },
            { "telegram",     new[] { @"%AppData%\Telegram Desktop\Telegram.exe" } },
            { "notion",       new[] { @"%LocalAppData%\Programs\Notion\Notion.exe", @"%AppData%\Notion\Notion.exe" } },
            { "wechat",       new[] { @"%ProgramFiles%\Tencent\WeChat\WeChat.exe" } },
            { "gitkraken",    new[] { @"%LocalAppData%\Programs\gitkraken\gitkraken.exe" } },
            { "insomnia",     new[] { @"%LocalAppData%\insomnia\Insomnia.exe" } },
            { "obsidian",     new[] { @"%LocalAppData%\Obsidian\Obsidian.exe", @"%AppData%\Obsidian\Obsidian.exe" } },
            { "typora",       new[] { @"%ProgramFiles%\Typora\Typora.exe", @"%LocalAppData%\Programs\Typora\Typora.exe" } },
            { "vlc",          new[] { @"%ProgramFiles%\VideoLAN\VLC\vlc.exe" } },
            { "7zfm",         new[] { @"%ProgramFiles%\7-Zip\7zFM.exe" } },
            { "sourcetree",   new[] { @"%LocalAppData%\SourceTree\SourceTree.exe" } },
        };

        /// <summary>Original shell icon lookup by file path.</summary>
        public static Icon GetIconFromPath(string filePath, bool small)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;
            try
            {
                SHFILEINFO shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | (small ? SHGFI_SMALLICON : SHGFI_LARGEICON);
                IntPtr res = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                if (res == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero) return null;
                Icon tmp = Icon.FromHandle(shinfo.hIcon);
                Icon cloned = (Icon)tmp.Clone();
                DestroyIcon(shinfo.hIcon);
                return cloned;
            }
            catch { return null; }
        }

        /// <summary>
        /// Multi-strategy icon resolver. Tries in order:
        /// 1. Provided exePath  2. Running process  3. Known app paths  4. Shell by name
        /// </summary>
        public static Icon GetBestIcon(string processName, string exePath = null, bool small = true)
        {
            // Strategy 1: Directly supplied exe path
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                var icon = GetIconFromPath(exePath, small);
                if (icon != null) return icon;
            }

            // Strategy 2: Find the running process and get its module path
            if (!string.IsNullOrWhiteSpace(processName))
            {
                try
                {
                    var procs = System.Diagnostics.Process.GetProcessesByName(processName);
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (p.MainModule != null && !string.IsNullOrWhiteSpace(p.MainModule.FileName))
                            {
                                var icon = GetIconFromPath(p.MainModule.FileName, small);
                                if (icon != null) { p.Dispose(); return icon; }
                            }
                        }
                        catch { }
                        finally { try { p.Dispose(); } catch { } }
                    }
                }
                catch { }

                // Strategy 3: Known app path dictionary
                string lowName = processName.ToLowerInvariant();
                if (KnownAppPaths.TryGetValue(lowName, out string[] paths))
                {
                    foreach (var template in paths)
                    {
                        string expanded = Environment.ExpandEnvironmentVariables(template);
                        if (expanded.Contains("*"))
                        {
                            // Wildcard path (e.g. app-*)
                            try
                            {
                                string parentDir = Path.GetDirectoryName(Path.GetDirectoryName(expanded));
                                string dirPattern = Path.GetFileName(Path.GetDirectoryName(expanded));
                                string fileName = Path.GetFileName(expanded);
                                if (Directory.Exists(parentDir))
                                {
                                    foreach (string sub in Directory.GetDirectories(parentDir, dirPattern))
                                    {
                                        string candidate = Path.Combine(sub, fileName);
                                        if (File.Exists(candidate))
                                        {
                                            var ic = GetIconFromPath(candidate, small);
                                            if (ic != null) return ic;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                        else if (File.Exists(expanded))
                        {
                            var icon = GetIconFromPath(expanded, small);
                            if (icon != null) return icon;
                        }
                    }
                }

                // Strategy 4: Shell association with process name as exe
                var shellIcon = GetIconFromPath(lowName + ".exe", small);
                if (shellIcon != null) return shellIcon;
            }

            return null;
        }

        /// <summary>
        /// Returns a colored placeholder bitmap with 2-letter initials for apps with no icon.
        /// </summary>
        public static Bitmap GetPlaceholderBitmap(string processName, int size = 24)
        {
            string initials = GetInitials(processName);
            Color bgColor = GetColorForName(processName);

            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (SolidBrush brush = new SolidBrush(bgColor))
                    g.FillEllipse(brush, 0, 0, size - 1, size - 1);

                float fontSize = Math.Max(6f, size * 0.35f);
                using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                {
                    g.DrawString(initials, font, textBrush, new RectangleF(0, 0, size, size), sf);
                }
            }
            return bmp;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            name = name.Trim();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private static Color GetColorForName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Color.FromArgb(108, 117, 125);
            int hash = 0;
            foreach (char c in name.ToLowerInvariant())
                hash = (hash * 31 + c) & 0x7FFFFFFF;
            Color[] palette =
            {
                Color.FromArgb(13,  110, 253),   // blue
                Color.FromArgb(25,  135, 84),    // green
                Color.FromArgb(220, 53,  69),    // red
                Color.FromArgb(255, 140, 0),     // orange
                Color.FromArgb(111, 66,  193),   // purple
                Color.FromArgb(23,  162, 184),   // teal
                Color.FromArgb(102, 16,  242),   // indigo
                Color.FromArgb(32,  201, 151),   // mint
            };
            return palette[hash % palette.Length];
        }
    }
}
