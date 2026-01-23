using System.Diagnostics;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Results;

public static class OpenPathProvider
{
    // ✅ This function matches the delegate signature
    public static Result? Provide(Result selectedResult, Item item)
    {
        if (string.IsNullOrWhiteSpace(item.FileName))
            return null;

        if (item.IsUrl(item.FileName))
            return null;

        if (!File.Exists(item.FileName) && !Directory.Exists(item.FileName))
            return null;

        return new Result
        {
            Title = "Open Path",
            SubTitle = "Open file or containing folder",
            IcoPath = "Assets/Folder.png",
            Action = _ =>
            {
                OpenPath(item.FileName);
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
                Process.Start("explorer.exe", $"/select,\"{path}\"");
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
        catch
        {
            // Fail silently
        }
    }
}
