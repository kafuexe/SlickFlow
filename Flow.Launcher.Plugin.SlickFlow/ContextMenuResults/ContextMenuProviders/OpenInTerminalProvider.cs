using System.Diagnostics;
using System.IO;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Results;

public static class OpenInTerminalProvider
{
    public static Result? Provide(Result selectedResult, Item item)
    {
        var folder = GetFolder(item.FileName);
        if (folder == null)
            return null;

        return new Result
        {
            Title = "Open in Terminal",
            SubTitle = folder,
            IcoPath = "Assets/Terminal.png",
            Action = _ =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "wt.exe",
                    Arguments = $"-d \"{folder}\"",
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

}
