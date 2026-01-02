
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
            uint uFlags
        );

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        #endregion

        #region Fields

        private static readonly HttpClient HttpClient = new(
            new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        

        private readonly ConcurrentDictionary<string, int> _attempts = new();
        private readonly Dictionary<string, string> _faviconUrlPatterns = new()
        {
            // “Default” is handled manually because it needs the scheme.
            ["Default"]   = "{0}://{1}/favicon.ico",
            ["DuckDuckGo"] = "https://icons.duckduckgo.com/ip2/{0}.ico",
            ["Google"]     = "https://www.google.com/s2/favicons?domain_url={0}"
        };

        private readonly string _iconFolder;
        private readonly Action<string>? _log;    // optional logger

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

        static IconHelper()
        {   
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; IconHelper/1.0)");
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
            cancellationToken.ThrowIfCancellationRequested();

            string safeFileName = MakeSafeFileName(itemId) + ".png";
            string targetPath   = Path.Combine(_iconFolder, safeFileName);

            // Fast path
            if (File.Exists(targetPath))
                return (true, targetPath);

            string attemptKey = $"{pathOrUrl}|{safeFileName}";

            try
            {
                // ---- URL first (cheap check, avoids filesystem hit) ----
                if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out Uri? uri) &&
                    (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                    uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
                {
                    if (await TryDownloadFaviconFromPatternsAsync(uri, targetPath, cancellationToken).ConfigureAwait(false) ||
                        await TryExtractIconFromHtmlAsync(uri, targetPath, cancellationToken).ConfigureAwait(false) ||
                        await TryDownloadRootFaviconAsync(uri, targetPath, cancellationToken).ConfigureAwait(false))
                    {
                        _attempts.TryRemove(attemptKey, out _);
                        return (true, targetPath);
                    }
                }
                // ---- Local file / directory ----
                else if (File.Exists(pathOrUrl) || Directory.Exists(pathOrUrl))
                {
                    if (ExtractIconFromPath(pathOrUrl, targetPath))
                    {
                        _attempts.TryRemove(attemptKey, out _);
                        return (true, targetPath);
                    }
                }

                // ---- Failure bookkeeping ----
                int attempts = _attempts.AddOrUpdate(
                    attemptKey,
                    _ => 1,
                    (_, cur) => cur + 1);

                if (attempts > 3)
                    _log?.Invoke($"IconHelper: giving up after {attempts} attempts for \"{pathOrUrl}\"");
            }
            catch (Exception ex) when (!ex.IsCritical())
            {
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


        private static Icon? GetShellIcon(string path, bool largeIcon)
        {
            SHFILEINFO shinfo = new SHFILEINFO();

            uint flags =
                SHGFI_ICON |
                (largeIcon ? SHGFI_LARGEICON : SHGFI_SMALLICON);

            SHGetFileInfo(
                path,
                0,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                flags
            );

            if (shinfo.hIcon == IntPtr.Zero)
                return null;

            // Clone so we can destroy original handle
            Icon icon = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
            DestroyIcon(shinfo.hIcon);

            return icon;
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
                sysIcon = GetShellIcon(path, true);
                if (sysIcon == null)
                    sysIcon = GetShellIcon(path, false);
            }
            else if (File.Exists(path))
            {
                sysIcon = Icon.ExtractAssociatedIcon(path);  
            }

            if (sysIcon == null)
                return false;

            using (sysIcon)
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
            Uri siteUri,
            string pngPath,
            CancellationToken ct)
        {
            foreach (var kvp in _faviconUrlPatterns)
            {
                IEnumerable<string> urls = kvp.Key == "Default"
                    ? new[]
                    {
                        $"https://{siteUri.Host}/favicon.ico",
                        $"http://{siteUri.Host}/favicon.ico"
                    }
                    : new[]
                    {
                        string.Format(kvp.Value, siteUri.Host)
                    };

                foreach (string requestUrl in urls)
                {
                    if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri? requestUri))
                        continue;

                    try
                    {
                        using var resp = await HttpClient.GetAsync(requestUri, ct).ConfigureAwait(false);
                        if (!resp.IsSuccessStatusCode)
                            continue;

                        byte[] data = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

                        if (TrySaveBytesAsPng(data, pngPath))
                            return true;
                    }
                    catch (Exception ex) when (!ex.IsCritical())
                    {
                        _log?.Invoke($"Favicon service \"{kvp.Key}\" failed for \"{siteUri.Host}\" – {ex.Message}");
                    }
                }
            }

            return false;
}


        #endregion

        #region Remote favicon via HTML <link rel="icon">

        // This regex tolerates any order of attributes and accepts single‑ or double‑quoted values.
        // It also captures URLs that contain spaces (escaped as %20) and ignores any trailing '>'

        private static readonly Regex IconLinkRegex = new(
        @"<link(?=[^>]*\brel\s*=\s*['""][^'""]*icon[^'""]*['""])[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex HrefRegex = new(
            @"\bhref\s*=\s*['""](?<url>[^'""]+)['""]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BaseHrefRegex = new(
            @"<base[^>]*\bhref\s*=\s*['""](?<url>[^'""]+)['""]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);


    private async Task<bool> TryExtractIconFromHtmlAsync(
            Uri siteUri,
            string pngPath,
            CancellationToken ct)
        {
            try
            {
                string html = await HttpClient.GetStringAsync(siteUri, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(html))
                    return false;

                // Detect <base href>
                Uri baseUri = siteUri;
                var baseMatch = BaseHrefRegex.Match(html);
                if (baseMatch.Success &&
                    Uri.TryCreate(baseMatch.Groups["url"].Value, UriKind.Absolute, out var b))
                {
                    baseUri = b;
                }

                foreach (Match linkMatch in IconLinkRegex.Matches(html))
                {
                    var hrefMatch = HrefRegex.Match(linkMatch.Value);
                    if (!hrefMatch.Success)
                        continue;

                    string rawHref = hrefMatch.Groups["url"].Value.Trim();
                    if (rawHref.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Uri resolved;
                    try
                    {
                        resolved = new Uri(baseUri, rawHref);
                    }
                    catch
                    {
                        continue;
                    }

                    using var resp = await HttpClient.GetAsync(resolved, ct).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                        continue;

                    byte[] data = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

                    if (TrySaveBytesAsPng(data, pngPath))
                        return true;
                }

                return false;
            }
            catch (Exception ex) when (!ex.IsCritical())
            {
                _log?.Invoke($"HTML favicon extraction failed for \"{siteUri}\" – {ex.Message}");
                return false;
            }
        }


        private async Task<bool> TryDownloadRootFaviconAsync(
            Uri siteUri,
            string pngPath,
            CancellationToken ct)
        {
            try
            {
                var uri = new Uri($"{siteUri.Scheme}://{siteUri.Host}/favicon.ico");
                using var resp = await HttpClient.GetAsync(uri, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    return false;

                byte[] data = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                return TrySaveBytesAsPng(data, pngPath);
            }
            catch
            {
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
