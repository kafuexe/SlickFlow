using System.Net.Http;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.SlickFlow.Utils.Icons;

/// <summary>
/// Downloads favicons from websites using known service patterns and HTML link extraction.
/// </summary>
internal sealed class FaviconDownloader
{
    private static readonly Regex IconLinkRegex = new(
        @"<link(?=[^>]*\brel\s*=\s*['""][^'""]*icon[^'""]*['""])[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HrefRegex = new(
        @"\bhref\s*=\s*['""](?<url>[^'""]+)['""]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BaseHrefRegex = new(
        @"<base[^>]*\bhref\s*=\s*['""](?<url>[^'""]+)['""]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly Action<string>? _log;

    private readonly Dictionary<string, string> _faviconUrlPatterns = new()
    {
        ["Default"] = "{0}://{1}/favicon.ico",
        ["DuckDuckGo"] = "https://icons.duckduckgo.com/ip2/{0}.ico",
        ["Google"] = "https://www.google.com/s2/favicons?domain_url={0}"
    };

    public FaviconDownloader(HttpClient httpClient, Action<string>? log = null)
    {
        _httpClient = httpClient;
        _log = log;
    }

    /// <summary>
    /// Tries multiple favicon sources in order: known service patterns, HTML link tags, root /favicon.ico.
    /// Returns the downloaded bytes on success, or null on failure.
    /// </summary>
    public async Task<byte[]?> TryDownloadAsync(Uri siteUri, CancellationToken ct)
    {
        return await TryFromPatternsAsync(siteUri, ct)
            ?? await TryFromHtmlAsync(siteUri, ct)
            ?? await TryFromRootAsync(siteUri, ct);
    }

    private async Task<byte[]?> TryFromPatternsAsync(Uri siteUri, CancellationToken ct)
    {
        foreach (var kvp in _faviconUrlPatterns)
        {
            IEnumerable<string> urls = kvp.Key == "Default"
                ? new[]
                {
                    $"https://{siteUri.Host}/favicon.ico",
                    $"http://{siteUri.Host}/favicon.ico"
                }
                : new[] { string.Format(kvp.Value, siteUri.Host) };

            foreach (string requestUrl in urls)
            {
                if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var requestUri))
                    continue;

                try
                {
                    var data = await DownloadBytesAsync(requestUri, ct);
                    if (data != null)
                        return data;
                }
                catch (Exception ex) when (!ex.IsCritical())
                {
                    _log?.Invoke($"Favicon service \"{kvp.Key}\" failed for \"{siteUri.Host}\" - {ex.Message}");
                }
            }
        }

        return null;
    }

    private async Task<byte[]?> TryFromHtmlAsync(Uri siteUri, CancellationToken ct)
    {
        try
        {
            string html = await _httpClient.GetStringAsync(siteUri, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(html))
                return null;

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
                try { resolved = new Uri(baseUri, rawHref); }
                catch { continue; }

                var data = await DownloadBytesAsync(resolved, ct);
                if (data != null)
                    return data;
            }

            return null;
        }
        catch (Exception ex) when (!ex.IsCritical())
        {
            _log?.Invoke($"HTML favicon extraction failed for \"{siteUri}\" - {ex.Message}");
            return null;
        }
    }

    private async Task<byte[]?> TryFromRootAsync(Uri siteUri, CancellationToken ct)
    {
        try
        {
            var uri = new Uri($"{siteUri.Scheme}://{siteUri.Host}/favicon.ico");
            return await DownloadBytesAsync(uri, ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task<byte[]?> DownloadBytesAsync(Uri uri, CancellationToken ct)
    {
        using var resp = await _httpClient.GetAsync(uri, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return null;

        var data = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return data.Length > 0 ? data : null;
    }
}
