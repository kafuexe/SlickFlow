using System.Diagnostics;
using Microsoft.Win32;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Results;

public static class OpenIncognitoProvider
{
    public static Result? Provide(Result selectedResult, Item item, IItemRepository itemRepo)
    {
        if (string.IsNullOrWhiteSpace(item.FileName) || !item.IsUrl(item.FileName))
            return null;

        return new Result
        {
            Title = "Open In Incognito",
            SubTitle = "Open this link in your system's default browser private window",
            IcoPath =  "Assets/IncognitoIcon.png",
            Action = _ =>
            {
                try
                {
                    OpenIncognitoDefault(item.FileName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Failed to open link incognito: {ex.Message}");
                }
                return true; 
            }
        };
    }

    private static void OpenIncognitoDefault(string url)
    {
        var defaultBrowser = GetDefaultBrowserExecutable();
        if (string.IsNullOrEmpty(defaultBrowser))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return;
        }

        var browserArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "chrome", "--incognito" },
            { "msedge", "--inprivate" },
            { "firefox", "-private-window" }
        };

        string browserLower = defaultBrowser.ToLowerInvariant();
        string flag = browserArgs.FirstOrDefault(kv => browserLower.Contains(kv.Key)).Value ?? "";
        string args = string.IsNullOrEmpty(flag) ? $"\"{url}\"" : $"{flag} \"{url}\"";

        var psi = new ProcessStartInfo
        {
            FileName = defaultBrowser,
            Arguments = args,
            UseShellExecute = true
        };

        Process.Start(psi);
    }

    private static string? GetDefaultBrowserExecutable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice"
            );
            var progId = key?.GetValue("ProgId") as string;
            if (string.IsNullOrEmpty(progId))
                return null;

            return progId switch
            {
                "ChromeHTML" => "chrome.exe",
                "MSEdgeHTM" => "msedge.exe",
                "FirefoxURL" => "firefox.exe",
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
