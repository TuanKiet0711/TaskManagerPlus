using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TaskManagerPlus.Services
{
    public static class IconHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

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

        public static Icon GetIconFromPath(string filePath, bool small)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;

            try
            {
                SHFILEINFO shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | (small ? SHGFI_SMALLICON : SHGFI_LARGEICON);

                IntPtr res = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                if (res == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                    return null;

                Icon tmp = Icon.FromHandle(shinfo.hIcon);
                Icon cloned = (Icon)tmp.Clone();     // detach from handle
                DestroyIcon(shinfo.hIcon);
                return cloned;
            }
            catch
            {
                return null;
            }
        }
    }
}
