using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace cztApp1
{
    public static class SystemIconProvider
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string path, uint attrs,
            ref SHFILEINFO fi, uint size, uint flags);

        [StructLayout(LayoutKind.Sequential)]
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

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr o);

        private static readonly ConcurrentDictionary<string, BitmapSource> _iconCache = new();

        public static BitmapSource FolderIcon => GetGenericFolderIcon();
        public static BitmapSource FileIcon => GetGenericFileIcon();

        /// <summary>
        /// Gets the Windows system icon for a specific file or folder path.
        /// Returns the actual icon (e.g. .shp shows ArcGIS icon if installed, .xlsx shows Excel icon).
        /// </summary>
        public static BitmapSource GetIcon(string path)
        {
            var key = Directory.Exists(path) ? "[folder]" : Path.GetExtension(path).ToLower();
            if (string.IsNullOrEmpty(key)) key = path.ToLower();
            return _iconCache.GetOrAdd(key, _ =>
            {
                uint attrs = Directory.Exists(path) ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
                var fi = new SHFILEINFO();
                SHGetFileInfo(path, attrs, ref fi, (uint)Marshal.SizeOf(fi),
                    SHGFI_ICON | SHGFI_SMALLICON);

                if (fi.hIcon == IntPtr.Zero) return null!;
                try
                {
                    using var icon = Icon.FromHandle(fi.hIcon);
                    using var bmp = icon.ToBitmap();
                    var hbmp = bmp.GetHbitmap();
                    try
                    {
                        var bs = Imaging.CreateBitmapSourceFromHBitmap(
                            hbmp, IntPtr.Zero, Int32Rect.Empty,
                            BitmapSizeOptions.FromWidthAndHeight(16, 16));
                        bs.Freeze();
                        return bs;
                    }
                    finally { DeleteObject(hbmp); }
                }
                finally { DestroyIcon(fi.hIcon); }
            })!;
        }

        private static BitmapSource GetGenericFolderIcon()
        {
            return _iconCache.GetOrAdd("[generic_folder]", _ =>
            {
                var fi = new SHFILEINFO();
                SHGetFileInfo("x", FILE_ATTRIBUTE_DIRECTORY, ref fi, (uint)Marshal.SizeOf(fi),
                    SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
                if (fi.hIcon == IntPtr.Zero) return null!;
                try
                {
                    using var icon = Icon.FromHandle(fi.hIcon);
                    using var bmp = icon.ToBitmap();
                    var hbmp = bmp.GetHbitmap();
                    try
                    {
                        var bs = Imaging.CreateBitmapSourceFromHBitmap(
                            hbmp, IntPtr.Zero, Int32Rect.Empty,
                            BitmapSizeOptions.FromWidthAndHeight(16, 16));
                        bs.Freeze();
                        return bs;
                    }
                    finally { DeleteObject(hbmp); }
                }
                finally { DestroyIcon(fi.hIcon); }
            })!;
        }

        private static BitmapSource GetGenericFileIcon()
        {
            return _iconCache.GetOrAdd("[generic_file]", _ =>
            {
                var fi = new SHFILEINFO();
                SHGetFileInfo("x.txt", FILE_ATTRIBUTE_NORMAL, ref fi, (uint)Marshal.SizeOf(fi),
                    SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
                if (fi.hIcon == IntPtr.Zero) return null!;
                try
                {
                    using var icon = Icon.FromHandle(fi.hIcon);
                    using var bmp = icon.ToBitmap();
                    var hbmp = bmp.GetHbitmap();
                    try
                    {
                        var bs = Imaging.CreateBitmapSourceFromHBitmap(
                            hbmp, IntPtr.Zero, Int32Rect.Empty,
                            BitmapSizeOptions.FromWidthAndHeight(16, 16));
                        bs.Freeze();
                        return bs;
                    }
                    finally { DeleteObject(hbmp); }
                }
                finally { DestroyIcon(fi.hIcon); }
            })!;
        }
    }
}
