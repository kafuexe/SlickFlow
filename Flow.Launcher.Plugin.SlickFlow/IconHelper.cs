using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.SlickFlow;

/// <summary>
/// Provides helper methods for saving icons from local executables or website URLs.
/// </summary>
public class IconHelper
{
    private readonly HttpClient _httpClient = new();
    private readonly HashSet<string> _failedUrls = new();
    private readonly string _iconFolder;

    private static readonly Dictionary<string, string> _faviconUrls = new()
    {
        ["Default"] = "{0}/favicon.ico",
        ["DuckDuckGo"] = "https://icons.duckduckgo.com/ip2/{0}.ico",
        ["Google"] = "https://www.google.com/s2/favicons?domain_url={0}"
    };

    private static readonly Dictionary<string, int> _attempts = new();

    public IconHelper(string iconFolder)
    {
        _iconFolder = iconFolder;
        Directory.CreateDirectory(iconFolder);
    }

    /// <summary>
    /// Synchronously saves an icon for a local exe or website URL.
    /// </summary>
    public string SaveIcon(string pathOrUrl, int itemId)
    {
        return SaveIconAsync(pathOrUrl, itemId).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously saves an icon for a local executable or a website URL.
    /// </summary>
    /// <param name="pathOrUrl">Local executable path or website URL to retrieve an icon from.</param>
    /// <param name="itemId">Identifier used to name the saved icon file (saved as {itemId}.png).</param>
    /// <returns>The full path to the saved PNG file, or an empty string if retrieval failed.</returns>
    public async Task<string> SaveIconAsync(string pathOrUrl, int itemId)
    {
        string iconPath = Path.Combine(_iconFolder, $"{itemId}.png");
        if (File.Exists(iconPath))
            return iconPath;

        if (_attempts.TryGetValue(pathOrUrl, out int count))
        {
            if (count >= 3)
                return string.Empty;
            _attempts[pathOrUrl]++;
        }
        else
        {
            _attempts[pathOrUrl] = 1;
        }

        // Local executable
        if (File.Exists(pathOrUrl))
            return TryExtractLocalIcon(pathOrUrl, iconPath);

        if (Directory.Exists(pathOrUrl))
            return TryExtractFolderIcon(iconPath);


        // Website favicon
        if (!pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
            _failedUrls.Contains(pathOrUrl))
            return string.Empty;

        try
        {
            var uri = new Uri(pathOrUrl);

            // 1️⃣ Try known favicon providers
            var favicon = await TryGetFromProvidersAsync(uri, iconPath);
            if (!string.IsNullOrEmpty(favicon))
                return favicon;

            // 2️⃣ Try extracting <link rel="icon"> from HTML
            favicon = await TryGetFromHtmlAsync(uri, iconPath);
            if (!string.IsNullOrEmpty(favicon))
                return favicon;
        }
        catch
        {
            _failedUrls.Add(pathOrUrl);
        }

        return string.Empty;
    }


    private string TryExtractLocalIcon(string exePath, string iconPath)
    {
        try
        {
            using Icon? icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null)
                return string.Empty;

            using var bmp = icon.ToBitmap();
            bmp.Save(iconPath, ImageFormat.Png);
            return iconPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> TryGetFromProvidersAsync(Uri uri, string iconPath)
    {
        foreach (var faviconUrl in _faviconUrls)
        {
            string requestUrl = faviconUrl.Key == "Default"
                ? $"{uri.Scheme}://{uri.Host}/favicon.ico"
                : string.Format(faviconUrl.Value, uri.Host);

            if (await TryDownloadAndSaveIconAsync(requestUrl, iconPath))
                return iconPath;
        }

        return string.Empty;
    }

    private async Task<string> TryGetFromHtmlAsync(Uri uri, string iconPath)
    {
        try
        {
            string html = await _httpClient.GetStringAsync(uri);
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Match any <link rel="...icon..." href="...">
            var match = Regex.Match(html,
                @"<link[^>]+rel\s*=\s*[""'][^""']*icon[^""']*[""'][^>]*href\s*=\s*[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return string.Empty;

            string href = match.Groups[1].Value.Trim();
            string resolvedUrl = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : new Uri(uri, href).ToString();

            if (await TryDownloadAndSaveImageAsync(resolvedUrl, iconPath))
                return iconPath;
        }
        catch
        {
            // ignore html parsing failures
        }

        return string.Empty;
    }

    private async Task<bool> TryDownloadAndSaveIconAsync(string url, string iconPath)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            byte[] data = await response.Content.ReadAsByteArrayAsync();

            using var ms = new MemoryStream(data);
            using var icon = new Icon(ms);
            using var bmp = icon.ToBitmap();

            bmp.Save(iconPath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryDownloadAndSaveImageAsync(string url, string iconPath)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            byte[] data = await response.Content.ReadAsByteArrayAsync();

            using var ms = new MemoryStream(data);
            using var bmp = new Bitmap(ms);

            bmp.Save(iconPath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string TryExtractFolderIcon(string iconPath)
    {
        try
        {
            using var folderIcon = SystemIcons.WinLogo; // fallback icon
            using var bmp = folderIcon.ToBitmap();
            bmp.Save(iconPath, ImageFormat.Png);
            return iconPath;
        }
        catch
        {
            return string.Empty;
        }
    }
}
    


