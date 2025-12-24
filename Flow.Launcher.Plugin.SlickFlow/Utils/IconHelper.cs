
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Flow.Launcher.Plugin.SlickFlow.Utils
{
    /// <summary>
    /// Provides helper methods for retrieving an icon (local or remote) and storing it as a PNG file.
    /// </summary>
    public sealed class IconHelper
    {
        #region Native interop

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

        private const uint SHGFI_ICON               = 0x000000100;
        private const uint SHGFI_LARGEICON          = 0x000000000;   // 0 = default (large)

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
        #endregion

        #region Fields

        // Shared HttpClient – one per process, thread‑safe.
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        // Retry counter – key = input (path or url), value = attempts made.
        private readonly ConcurrentDictionary<string, int> _attempts = new();

        // Known favicon services.
        private readonly Dictionary<string, string> _faviconUrlPatterns = new()
        {
            // “Default” is handled manually because it needs the scheme.
            ["Default"]   = "{0}://{1}/favicon.ico",
            ["DuckDuckGo"] = "https://icons.duckduckgo.com/ip2/{0}.ico",
            ["Google"]     = "https://www.google.com/s2/favicons?domain_url={0}"
        };

        private readonly string _iconFolder;
        private readonly Action<string>? _log;    // optional host logger

        #endregion

        #region Construction 

        /// <summary>
        /// Creates a new <see cref="IconHelper"/>.
        /// </summary>
        /// <param name="iconFolder">
        /// Folder where the PNG files will be stored. The folder is created if it does not exist.
        /// </param>
        /// <param name="log">
        /// Optional logger – any string will be forwarded to the host (for debugging).
        /// </param>
        public IconHelper(string iconFolder, Action<string>? log = null)
        {
            _iconFolder = Path.GetFullPath(iconFolder);
            Directory.CreateDirectory(_iconFolder);
            _log = log;
        }
        #endregion

        #region Public API (async & sync)

        /// <summary>
        /// Tries to retrieve an icon for <paramref name="pathOrUrl"/> and saves it as <c>{itemId}.png</c>.
        /// </summary>
        /// <param name="pathOrUrl">Local file/dir or a web URL.</param>
        /// <param name="itemId">Identifier used for the file name. All illegal file‑name characters are stripped.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns><c>true</c> on success, otherwise <c>false</c>.</returns>
        public async Task<(bool Success, string SavedPath)> TrySaveIconAsync(
        string pathOrUrl,
        string itemId,
        CancellationToken cancellationToken = default)
        {
            string safeFileName = MakeSafeFileName(itemId) + ".png";
            string targetPath   = Path.Combine(_iconFolder, safeFileName);

            if (File.Exists(targetPath))
                return (true, targetPath);

            int attempts = _attempts.AddOrUpdate(
                key: pathOrUrl,
                addValueFactory: _ => 1,
                updateValueFactory: (_, cur) => cur + 1);

            if (attempts > 3)
            {
                _log?.Invoke($"IconHelper: giving up after {attempts} attempts for \"{pathOrUrl}\"");
                return (false, string.Empty);
            }

            try
            {
                // Local file / folder ?
                if (File.Exists(pathOrUrl) || Directory.Exists(pathOrUrl))
                {
                    if (ExtractIconFromPath(pathOrUrl, targetPath))
                    {
                        var savedPath = targetPath;
                        return (true, savedPath);
                    }
                }

                //  Remote URL ?
                if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out Uri? uri) &&
                    (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                     uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
                {
                    // a) try known favicon services
                    if (await TryDownloadFaviconFromPatternsAsync(uri, targetPath, cancellationToken)
                              .ConfigureAwait(false))
                    {
                        var savedPath = targetPath;
                        return (true, savedPath);
                    }

                    // b) fall‑back to HTML <link rel="icon"...> parsing
                    if (await TryExtractIconFromHtmlAsync(uri, targetPath, cancellationToken)
                              .ConfigureAwait(false))
                    {
                        var savedPath = targetPath;
                        return (true, savedPath);
                    }
                }
            }
            catch (Exception ex) when (!ex.IsCritical())
            {
                // Swallow non‑critical exceptions but log them – the contract is “false on failure”.
                _log?.Invoke($"IconHelper: unexpected error for \"{pathOrUrl}\" – {ex.Message}");
            }

            return (false, string.Empty);
        }

        /// <summary>
        /// Set a custom icon for an item. The source can be a local image file or a remote URL.
        /// </summary>
        /// <param name="iconSource">File path or URL.</param>
        /// <param name="itemId">Identifier used to name the PNG file.</param>
        /// <param name="ct">CancellationToken </param>
        /// <returns>Full path of the saved PNG on success, otherwise <c>string.Empty</c>.</returns>
        public async Task<string> SetCustomIconAsync(string iconSource, string itemId, CancellationToken ct = default)
        {
            string safeFileName = MakeSafeFileName(itemId) + ".png";
            string destPath = Path.Combine(_iconFolder, safeFileName);

            try
            {
                // Local file → just copy
                if (File.Exists(iconSource))
                {
                    File.Copy(iconSource, destPath, overwrite: true);
                    return destPath;
                }

                // Remote URL → download the image (re‑use the same pipeline as SaveIcon)
                var result = await TrySaveIconAsync(iconSource, itemId, ct);
                return result.Success ? result.SavedPath : "";
            }
            catch (Exception ex) when (!ex.IsCritical())
            {
                _log?.Invoke($"IconHelper:SetCustomIcon failed for \"{iconSource}\" {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Synchronous version of <see cref="SetCustomIconAsync"/> (blocks the calling thread).
        /// </summary>
        public string SetCustomIcon(string iconSource, string itemId)
            => SetCustomIconAsync(iconSource, itemId).GetAwaiter().GetResult();

        #endregion

        #region Private helpers

        /// <summary>
        /// Normalises a user supplied identifier into a safe file name.
        /// </summary>
        private static string MakeSafeFileName(string input)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char ch in input)
            {
                if (Array.IndexOf(invalid, ch) < 0 && ch != Path.DirectorySeparatorChar && ch != Path.AltDirectorySeparatorChar)
                    sb.Append(ch);
            }
            // Trim any leading/trailing dots (Windows doesn't like those)
            return sb.ToString().Trim('.').Trim();
        }

        #region Local icon extraction

        /// <summary>
    /// Extracts a system icon from a file **or** a directory and writes it as a PNG.
    /// Returns true on success.
    /// </summary>
    private bool ExtractIconFromPath(string path, string pngPath)
    {
        try
        {
            Icon? sysIcon = null;
            IntPtr nativeIcon = IntPtr.Zero;
            // Folder ?
            if (Directory.Exists(path))
            {
                // Ask the shell for the *folder* icon.
                SHFILEINFO shfi = new();

                nativeIcon = SHGetFileInfo(
                path,
                0, ref shfi, (uint)Marshal.SizeOf(shfi),
                SHGFI_ICON | SHGFI_LARGEICON);

                if (nativeIcon != IntPtr.Zero)
                {
                    // FromHandle only wraps the native handle – we must clone it
                    // before we destroy the original handle.
                    using Icon wrapper = Icon.FromHandle(nativeIcon);
                    sysIcon = (Icon)wrapper.Clone();      // <- owns its own HICON now
                }
            }
            //  Normal file ?
            else if (File.Exists(path))
            {
                sysIcon = Icon.ExtractAssociatedIcon(path);   // already a managed copy
            }

            if (sysIcon == null)
                return false;

            //  Write to PNG (WPF path + pure‑GDI fallback)
            using (sysIcon)                    // disposes GDI+ wrapper
            {
                SaveIconToPng(sysIcon, pngPath);
            }

            // Release native handle we got from SHGetFileInfo
            if (nativeIcon != IntPtr.Zero)
                DestroyIcon(nativeIcon);

            return true;
        }
        catch (Exception ex) when (!ex.IsCritical())
        {
            _log?.Invoke($"ExtractIconFromPath failed for \"{path}\" - {ex.Message}");
            return false;
        }
    }

        /// <summary>
        /// Writes a <see cref="System.Drawing.Icon"/> to a PNG file using WPF interop (preserves transparency).
        /// </summary>
        private void SaveIconToPng(Icon icon, string destinationPath)
        {
            // Convert the HICON to a WPF BitmapSource – this respects alpha channels.
            IntPtr hIcon = icon.Handle;
            BitmapSource src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            // Ensure the source is frozen so it can be used across threads and the underlying handle can be released.
            src.Freeze();

            // Encode to PNG.
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(src));

            using var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(file);
        }

        #endregion

        #region Remote favicon download (known services)

        private async Task<bool> TryDownloadFaviconFromPatternsAsync(
            Uri uri,
            string pngPath,
            CancellationToken ct)
        {
            foreach (var kvp in _faviconUrlPatterns)
            {
                string requestUrl = kvp.Key == "Default"
                    ? string.Format(kvp.Value, uri.Scheme, uri.Host)   // “{0}://{1}/favicon.ico”
                    : string.Format(kvp.Value, uri.Host);              // other services

                if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri? requestUri))
                    continue;

                try
                {
                    using var response = await HttpClient.GetAsync(requestUri, ct).ConfigureAwait(false);
                    if (response.StatusCode != HttpStatusCode.OK)
                        continue;

                    byte[] data = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    if (TrySaveBytesAsPng(data, pngPath))
                        return true;
                }
                catch (Exception ex) when (!ex.IsCritical())
                {
                    // Swallow – try the next service.
                    _log?.Invoke($"Favicon service \"{kvp.Key}\" failed for \"{uri.Host}\" – {ex.Message}");
                }
            }

            return false;
        }

        #endregion

        #region Remote favicon via HTML <link rel="icon">

        // This regex tolerates any order of attributes and accepts single‑ or double‑quoted values.
        // It also captures URLs that contain spaces (escaped as %20) and ignores any trailing '>'
        private static readonly Regex IconLinkRegex = new(
            @"<link(?=[^>]*\brel\s*=\s*['""][^'""]*icon[^'""]*['""])" + // rel contains “icon”
            @"[^>]*\bhref\s*=\s*['""](?<url>[^'""]+)['""][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private async Task<bool> TryExtractIconFromHtmlAsync(
            Uri siteUri,
            string pngPath,
            CancellationToken ct)
        {
            try
            {
                // 1️⃣  Get the raw HTML (no parsing libraries – keep the dependency footprint small)
                string html = await HttpClient.GetStringAsync(siteUri, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(html))
                    return false;

                // 2️⃣  Search the first <link … rel="…icon…" … href="…">
                Match m = IconLinkRegex.Match(html);
                if (!m.Success)
                    return false;

                string rawHref = m.Groups["url"].Value.Trim();

                // 3️⃣  Resolve relative URLs
                Uri resolved = rawHref.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(rawHref)
                    : new Uri(siteUri, rawHref);

                // 4️⃣  Download the candidate image
                using var resp = await HttpClient.GetAsync(resolved, ct).ConfigureAwait(false);
                if (resp.StatusCode != HttpStatusCode.OK)
                    return false;

                byte[] data = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                return TrySaveBytesAsPng(data, pngPath);
            }
            catch (Exception ex) when (!ex.IsCritical())
            {
                _log?.Invoke($"HTML favicon extraction failed for \"{siteUri}\" – {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Byte-to-PNG conversion

        /// <summary>
        /// Tries to interpret <paramref name="data"/> as an image (ICO, PNG, JPEG, GIF, BMP, …) and writes it as PNG.
        /// </summary>
        private static bool TrySaveBytesAsPng(byte[] data, string pngPath)
        {
            if ( data.Length == 0)
                return false;

            try
            {
                using var ms = new MemoryStream(data, writable: false);
                // ArgumentException on unsupported data – we swallow that and fall back to
                // the Bitmap constructor which has a slightly different detection order.
                using Image img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false);

                // Ensure we are writing a 32‑bpp PNG so the alpha channel is kept.
                using var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImageUnscaled(img, 0, 0);
                }

                bmp.Save(pngPath, ImageFormat.Png);
                return true;
            }
            catch (Exception ex) when (!ex.IsCritical())
            {
                // The data was not a recognizable image type.
                // Log for debugging – the caller will just get “false”.
                // (In production you may replace this with a proper logger.)
                System.Diagnostics.Debug.WriteLine($"TrySaveBytesAsPng failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #endregion
    }
}
