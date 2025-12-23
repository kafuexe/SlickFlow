using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Text.RegularExpressions;


namespace Flow.Launcher.Plugin.SlickFlow.Utils
{
    /// <summary>
    /// Provides helper methods for saving icons from local executables or website URLs.
    /// </summary>
    public class IconHelper
    {
        private readonly HttpClient _httpClient = new();
        private readonly Dictionary<string, string> _faviconUrls = new()
        {
            ["Default"] = "{0}/favicon.ico",
            ["DuckDuckGo"] = "https://icons.duckduckgo.com/ip2/{0}.ico",
            ["Google"] = "https://www.google.com/s2/favicons?domain_url={0}"
        };

        private static Dictionary<string, int> _attempts = new();
        private readonly string _iconFolder;

        /// <summary>
        /// Save icon for local exe or website URL
        /// </summary>
        /// 
        public IconHelper(string iconFolder)
        {
            _iconFolder = iconFolder;
            Directory.CreateDirectory(iconFolder);
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// Asynchronously saves an icon for a local executable file or a website URL and returns
        /// the full path to the saved PNG file; returns an empty string when no icon could be retrieved.
        /// </summary>
        /// <param name="pathOrUrl">Local executable path or website URL to retrieve an icon from.</param>
        /// <param name="itemId">Identifier used to name the saved icon file (saved as {itemId}.png).</param>
        /// <returns>A Task containing the full path to the saved PNG, or an empty string if not available.</returns>
        public async Task<string> SaveIconAsync(string pathOrUrl, string itemId)
        {
            string iconPath = Path.Combine(_iconFolder, $"{itemId}.png");
            if (File.Exists(iconPath))
                return iconPath;

            if (_attempts.ContainsKey(pathOrUrl))
            {
                if (_attempts[pathOrUrl] >= 3)
                    return string.Empty;
                _attempts[pathOrUrl]++;
            }
            else
                _attempts[pathOrUrl] = 1;


            if (File.Exists(pathOrUrl))
            {
                try
                {
                    using Icon? icon = Icon.ExtractAssociatedIcon(pathOrUrl);
                    if (icon == null) return string.Empty;

                    using var bmp = icon.ToBitmap();
                    bmp.Save(iconPath, ImageFormat.Png);
                    return iconPath;
                }
                catch
                {
                    return string.Empty;
                }
            }

            if (pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // Try to download favicon from the website using known patterns
                var uri = new Uri(pathOrUrl);
                try
                {
                    foreach (var faviconUrl in _faviconUrls)
                    {
                        string requestUrl = faviconUrl.Key == "Default"
                            ? $"{uri.Scheme}://{uri.Host}/favicon.ico"
                            : string.Format(faviconUrl.Value, uri.Host);

                        try
                        {
                            var response =
                                await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUrl));
                            if (response.StatusCode != HttpStatusCode.OK)
                                continue;

                            byte[] data = await response.Content.ReadAsByteArrayAsync();
                            if (SaveAsPng(data, iconPath))
                                return iconPath;
                            
                        }
                        catch
                        {
                            // ignore and try next
                        }
                    }
                }
                catch
                {
                }

                // If all failed → Try extracting <link rel="icon"> from HTML
                try
                {
                    var htmlResponse = await _httpClient.GetStringAsync(uri);
                    if (!string.IsNullOrEmpty(htmlResponse))
                    {
                        // Find <link ... rel="...icon..." ... href="...">
                        var match = Regex.Match(htmlResponse,
                            @"<link[^>]+rel\s*=\s*[""']?[^>]*icon[^>]*[""'][^>]*href\s*=\s*[""']([^""']+)[""']",
                            RegexOptions.IgnoreCase);

                        if (match.Success)
                        {
                            string href = match.Groups[1].Value.Trim();
                            string resolvedUrl = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                ? href
                                : new Uri(uri, href).ToString(); // make absolute

                            try
                            {
                                var response =
                                    await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, resolvedUrl));
                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    byte[] data = await response.Content.ReadAsByteArrayAsync();
                                    if (SaveAsPng(data, iconPath))
                                        return iconPath;
                                }
                            }
                            catch
                            {
                                // ignore and fall through
                            }
                        }
                    }
                }
                catch
                {
                    // ignore html fetch errors
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Synchronously saves an icon for a local exe or website URL
        /// </summary>
        public string SaveIcon(string pathOrUrl, string itemId)
        {
            return SaveIconAsync(pathOrUrl, itemId).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets a custom icon for an item by copying the provided icon file or downloading from URL.
        /// </summary>
        /// <param name="iconSource">Path to the icon file or URL to download.</param>
        /// <param name="itemId">Item ID for naming the saved icon.</param>
        /// <returns>Path to the saved icon, or empty if failed.</returns>
        public string SetCustomIcon(string iconSource, string itemId)
        {
            string iconPath = Path.Combine(_iconFolder, $"{itemId}.png");
            try
            {
                if (File.Exists(iconSource))
                {
                    File.Copy(iconSource, iconPath, true);
                    return iconPath;
                }
                else if (iconSource.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    return SaveIconAsync(iconSource, itemId).GetAwaiter().GetResult();
                }
            }
            catch
            {
                // ignore
            }
            return string.Empty;
        }


        private static bool SaveAsPng(byte[] data, string savePath)
        {
            try
            {
                using var ms = new MemoryStream(data);
                // First, try to load as an ICO
                try
                {
                    using Icon icon = new Icon(ms);
                    using Bitmap bmp = icon.ToBitmap();
                    bmp.Save(savePath, ImageFormat.Png);
                    return true;
                }
                catch
                {
                    ms.Position = 0;
                    // If ICO failed, try as a regular image (PNG, JPG, etc.)
                    using Bitmap bmp = new Bitmap(ms);
                    bmp.Save(savePath, ImageFormat.Png);
                    return true;
                }
            }
            catch
            {
                return false; // Failed to parse/save the image
            }
        }
    }
}
