using System.Diagnostics;
using System.IO;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Results;

public static class OpenInPowerShellProvider
{
    public static Result? Provide(Result selectedResult, Item item, IItemRepository itemRepo)
    {
        var folder = GetFolder(item.FileName);
        if (folder == null || !HasPowerShell())
            return null;

        return new Result
        {
            Title = "Open in PowerShell",
            SubTitle = folder,
            IcoPath = "Assets/Powershell.png",
            Action = _ =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -Command \"Set-Location '{folder}'\"",
                    UseShellExecute = true
                });

                return true;
            }
        };
    }

    private static string? GetFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (Directory.Exists(path))
            return path;

        if (File.Exists(path))
            return Path.GetDirectoryName(path);

        return null;
    }

    private static bool HasPowerShell()
    {
        return File.Exists(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe"
            )
        );
    }
}
