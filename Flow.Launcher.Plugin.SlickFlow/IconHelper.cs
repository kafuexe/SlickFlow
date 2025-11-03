using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

public class IconHelper
{
    private  readonly HttpClient _httpClient = new();
    private  readonly HashSet<string> _failedUrls = new();

    private  readonly Dictionary<string, string> _faviconUrls = new()
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
                        var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUrl));
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
    /// <param name="pathOrUrl">Path to executable or URL of website</param>
    /// <param name="itemId">Unique identifier for the icon</param>
    /// <param name="iconFolder">Folder where the icon will be saved</param>
    /// <returns>Path to the saved icon file, or empty string if failed</returns>
    public string SaveIcon(string pathOrUrl, int itemId)
    {
        return SaveIconAsync(pathOrUrl, itemId).GetAwaiter().GetResult();
    }
}
