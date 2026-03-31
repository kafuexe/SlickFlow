using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Plugin.SlickFlow.Utils.Icons;

/// <summary>
/// Extracts system shell icons from local files and directories using Windows Shell API.
/// </summary>
internal static class ShellIconExtractor
{
    #region Native interop

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;

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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    #endregion

    /// <summary>
    /// Extracts a shell icon from a file or directory path.
    /// Returns null if no icon could be extracted.
    /// </summary>
    public static Icon? Extract(string path)
    {
        if (Directory.Exists(path))
            return GetShellIcon(path, largeIcon: true) ?? GetShellIcon(path, largeIcon: false);

        if (File.Exists(path))
            return Icon.ExtractAssociatedIcon(path);

        return null;
    }

    private static Icon? GetShellIcon(string path, bool largeIcon)
    {
        var shinfo = new SHFILEINFO();
        uint flags = SHGFI_ICON | (largeIcon ? SHGFI_LARGEICON : SHGFI_SMALLICON);

        SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

        if (shinfo.hIcon == IntPtr.Zero)
            return null;

        var icon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
        DestroyIcon(shinfo.hIcon);
        return icon;
    }
}
