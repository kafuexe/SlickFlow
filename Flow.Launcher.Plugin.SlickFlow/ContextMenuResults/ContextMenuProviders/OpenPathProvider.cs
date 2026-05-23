using System.Diagnostics;
using System.IO;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Results;


public static class OpenPathProvider 
{
    public static Result? Provide(Result selectedResult, Item item, IItemRepository itemRepo)
    {
        var path = item.FileName;

        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (item.IsUrl(path))
            return null;

        if (!File.Exists(path) && !Directory.Exists(path))
            return null;

        return new Result
        {
            Title = "Open File Location",
            SubTitle = "Reveal in Explorer",
            IcoPath = "Assets/Folder.png",
            Action = _ =>
            {
                OpenPath(path);
                return true;
            }
        };
    }

    private static void OpenPath(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }
}

