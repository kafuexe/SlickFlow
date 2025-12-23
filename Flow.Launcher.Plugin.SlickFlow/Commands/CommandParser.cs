using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.SlickFlow;

public static class CommandParser
{
    public static string[] SplitArgs(string command)
    {
        var pattern = @"[\""].+?[\""]|[^ ]+";
        return Regex.Matches(command, pattern)
                .Select(m => m.Value.Trim('"'))
                .ToArray();
    }
}