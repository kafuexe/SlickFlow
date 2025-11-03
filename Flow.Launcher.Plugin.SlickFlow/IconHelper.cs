using System.Drawing;
using System.Drawing.Imaging;
using System.Net;

/// <summary>
/// Provides helper methods for saving icons from local executables or website URLs.
/// </summary>
namespace Flow.Launcher.Plugin.SlickFlow
{
    public class IconHelper
    {
        private readonly HttpClient _httpClient = new();
        private readonly HashSet<string> _failedUrls = new();

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
        }

        /// <summary>
        /// Asynchronously saves an icon for a local executable file or a website URL and returns
        /// the full path to the saved PNG file; returns an empty string when no icon could be retrieved.
        /// </summary>
        /// <param name="pathOrUrl">Local executable path or website URL to retrieve an icon from.</param>
        /// <param name="itemId">Identifier used to name the saved icon file (saved as {itemId}.png).</param>
        /// <returns>A Task containing the full path to the saved PNG, or an empty string if not available.</returns>
        public async Task<string> SaveIconAsync(string pathOrUrl, int itemId)
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

            if (pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !_failedUrls.Contains(pathOrUrl))
            {
                try
                {
                    var uri = new Uri(pathOrUrl);

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

                            using var ms = new MemoryStream(data);
                            using Icon icon = new Icon(ms);
                            using var bmp = icon.ToBitmap();

                            bmp.Save(iconPath, ImageFormat.Png);
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
                    _failedUrls.Add(pathOrUrl);
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Synchronously saves an icon for a local exe or website URL
        /// </summary>
        public string SaveIcon(string pathOrUrl, int itemId)
        {
            return SaveIconAsync(pathOrUrl, itemId).GetAwaiter().GetResult();
        }
    }
}
