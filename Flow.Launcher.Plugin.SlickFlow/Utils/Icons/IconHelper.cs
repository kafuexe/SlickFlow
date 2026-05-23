using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;

namespace Flow.Launcher.Plugin.SlickFlow.Utils.Icons;

/// <summary>
/// Orchestrates icon retrieval from local files/directories and remote URLs,
/// saving results as PNG files. Delegates to specialized classes for extraction,
/// downloading, and conversion.
/// </summary>
public sealed class IconHelper
{
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
    private readonly FaviconDownloader _faviconDownloader;
    private readonly string _iconFolder;
    private readonly Action<string>? _log;

    static IconHelper()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; IconHelper/1.0)");
    }

    /// <summary>
    /// Creates a new <see cref="IconHelper"/>.
    /// </summary>
    /// <param name="iconFolder">Folder where PNG files will be stored. Created if it does not exist.</param>
    /// <param name="log">Optional logger for debugging.</param>
    public IconHelper(string iconFolder, Action<string>? log = null)
    {
        _iconFolder = Path.GetFullPath(iconFolder);
        Directory.CreateDirectory(_iconFolder);
        _log = log;
        _faviconDownloader = new FaviconDownloader(HttpClient, log);
    }

    /// <summary>
    /// Tries to retrieve an icon for <paramref name="pathOrUrl"/> and saves it as <c>{itemId}.png</c>.
    /// </summary>
    public async Task<(bool Success, string SavedPath)> TrySaveIconAsync(
        string pathOrUrl,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string safeFileName = FileNameSanitizer.MakeSafe(itemId) + ".png";
        string targetPath = Path.Combine(_iconFolder, safeFileName);

        if (File.Exists(targetPath))
            return (true, targetPath);

        string attemptKey = $"{pathOrUrl}|{safeFileName}";

        try
        {
            if (IsHttpUrl(pathOrUrl, out var uri))
            {
                if (await TrySaveFromUrlAsync(uri!, targetPath, cancellationToken))
                {
                    _attempts.TryRemove(attemptKey, out _);
                    return (true, targetPath);
                }
            }
            else
            {
                string localPath = pathOrUrl;
                if (!File.Exists(localPath) && !Directory.Exists(localPath)
                    && ExecutablePathResolver.TryResolve(pathOrUrl, out var resolved))
                {
                    localPath = resolved;
                }

                if ((File.Exists(localPath) || Directory.Exists(localPath))
                    && TrySaveFromLocalPath(localPath, targetPath))
                {
                    _attempts.TryRemove(attemptKey, out _);
                    return (true, targetPath);
                }
            }

            int attempts = _attempts.AddOrUpdate(attemptKey, _ => 1, (_, cur) => cur + 1);
            if (attempts > 3)
                _log?.Invoke($"IconHelper: giving up after {attempts} attempts for \"{pathOrUrl}\"");
        }
        catch (Exception ex) when (!ex.IsCritical())
        {
            _log?.Invoke($"IconHelper: unexpected error for \"{pathOrUrl}\" - {ex.Message}");
        }

        return (false, string.Empty);
    }

    /// <summary>
    /// Set a custom icon for an item. The source can be a local image file or a remote URL.
    /// </summary>
    public async Task<string> SetCustomIconAsync(string iconSource, string itemId, CancellationToken ct = default)
    {
        string safeFileName = FileNameSanitizer.MakeSafe(itemId) + ".png";
        string destPath = Path.Combine(_iconFolder, safeFileName);

        try
        {
            if (File.Exists(iconSource))
            {
                File.Copy(iconSource, destPath, overwrite: true);
                return destPath;
            }

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

    #region Private helpers

    private async Task<bool> TrySaveFromUrlAsync(Uri uri, string targetPath, CancellationToken ct)
    {
        byte[]? data = await _faviconDownloader.TryDownloadAsync(uri, ct);
        return data != null && ImageConverter.TrySaveBytesAsPng(data, targetPath);
    }

    private bool TrySaveFromLocalPath(string path, string pngPath)
    {
        try
        {
            var icon = ShellIconExtractor.Extract(path);
            if (icon == null)
                return false;

            using (icon)
            {
                ImageConverter.SaveIconToPng(icon, pngPath);
            }
            return true;
        }
        catch (Exception ex) when (!ex.IsCritical())
        {
            _log?.Invoke($"ExtractIconFromPath failed for \"{path}\" - {ex.Message}");
            return false;
        }
    }

    private static bool IsHttpUrl(string pathOrUrl, out Uri? uri)
    {
        uri = null;
        return Uri.TryCreate(pathOrUrl, UriKind.Absolute, out uri) &&
               (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
