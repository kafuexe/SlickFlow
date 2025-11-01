using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public static class IconHelper
{
    private static readonly HttpClient _httpClient = new();
    private static readonly HashSet<string> _failedUrls = new();

    private static readonly Dictionary<string, string> _faviconUrls = new()
    {
        ["Default"] = "{0}/favicon.ico",
        ["DuckDuckGo"] = "https://icons.duckduckgo.com/ip2/{0}.ico",
        ["Google"] = "https://www.google.com/s2/favicons?domain_url={0}"
    };

    /// <summary>
    /// Save icon for local exe or website URL
    /// </summary>
    public static string SaveIcon(string pathOrUrl, int itemId, string alias, string iconFolder)
    {
        Directory.CreateDirectory(iconFolder);
        string safeAlias = alias.Replace(" ", "_").ToLowerInvariant();
        string iconPath = Path.Combine(iconFolder, $"{itemId}_{safeAlias}.png");

        // ✅ If file already exists, just return it
        if (File.Exists(iconPath))
            return iconPath;

        // Local exe
        if (File.Exists(pathOrUrl))
        {
            try
            {
                using Icon icon = Icon.ExtractAssociatedIcon(pathOrUrl);
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

        // Website URL → fire-and-forget download
        if (pathOrUrl.StartsWith("http") && !_failedUrls.Contains(pathOrUrl))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var uri = new Uri(pathOrUrl);

                    foreach (var kv in _faviconUrls)
                    {
                        string requestUrl = string.Format(kv.Value, uri.Host);

                        try
                        {
                            byte[] data = await _httpClient.GetByteArrayAsync(requestUrl);
                            if (data.Length == 0) continue;

                            using var ms = new MemoryStream(data);
                            using Icon icon = new Icon(ms);
                            using var bmp = icon.ToBitmap();
                            bmp.Save(iconPath, ImageFormat.Png);

                            // Optionally: notify Flow Launcher to refresh this result
                            // e.g., _publicAPI?.UpdateResult(result);
                            break;
                        }
                        catch { /* ignore and try next fallback */ }
                    }
                }
                catch
                {
                    _failedUrls.Add(pathOrUrl); // don't retry failed URLs
                }
            });
        }

        // Return default icon immediately if no existing file
        return "";
    }
}
