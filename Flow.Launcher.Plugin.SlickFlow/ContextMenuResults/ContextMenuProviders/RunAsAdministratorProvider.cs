using System.IO;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Results;

public static class RunAsAdministratorProvider
{
    public static Result? Provide(Result selectedResult, Item item)
    {
        if (!ShouldShow(item))
            return null;

        return new Result
        {
            Title = "Run as Administrator",
            SubTitle = "Launch this program with elevated privileges",
            IcoPath = "Assets/Shield.png",
            Action = _ =>
            {
                Task.Run(() =>
                {
                    try
                    {
                        item.Execute(forceAdminExec: true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[Error] Failed to execute item '{item.FileName}': {ex.Message}");
                    }
                });

                return true; 
            }
        };
    }

    private static bool ShouldShow(Item item)
    {
        return
            !string.IsNullOrWhiteSpace(item.FileName) &&
            !item.IsUrl(item.FileName) &&
            File.Exists(item.FileName) &&
            !Directory.Exists(item.FileName) &&
            item.RunAs != 1 &&
            IsExecutable(item.FileName);
    }

    private static bool IsExecutable(string path)
    {
        return string.Equals(
            Path.GetExtension(path),
            ".exe",
            StringComparison.OrdinalIgnoreCase
        );
    }
}
