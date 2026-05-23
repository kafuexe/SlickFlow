using System.IO;

namespace Flow.Launcher.Plugin.SlickFlow.Utils.Icons;

/// <summary>
/// Resolves a bare command (e.g. <c>notepad</c>) to an absolute file path by walking
/// <c>PATH</c> and trying each <c>PATHEXT</c> extension. Mirrors how <c>where.exe</c>
/// and <c>Process.Start</c> locate executables, so the icon code can find files that
/// <see cref="File.Exists(string)"/> alone would miss.
/// </summary>
internal static class ExecutablePathResolver
{
    private const string DefaultPathExt = ".COM;.EXE;.BAT;.CMD";

    public static bool TryResolve(string command, out string fullPath)
        => TryResolve(
            command,
            out fullPath,
            Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
            Environment.GetEnvironmentVariable("PATHEXT") ?? DefaultPathExt);

    public static bool TryResolve(string command, out string fullPath, string pathEnv, string pathExtEnv)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(command))
            return false;

        // Anything that looks like a path is the caller's job to validate.
        if (Path.IsPathRooted(command) ||
            command.Contains(Path.DirectorySeparatorChar) ||
            command.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        var dirs = (pathEnv ?? string.Empty).Split(Path.PathSeparator);
        var exts = (pathExtEnv ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries);
        var hasExt = Path.HasExtension(command);

        foreach (var rawDir in dirs)
        {
            var dir = rawDir?.Trim().Trim('"') ?? string.Empty;
            if (string.IsNullOrEmpty(dir))
                continue;

            if (hasExt)
            {
                if (TryCandidate(dir, command, out fullPath))
                    return true;
            }
            else
            {
                foreach (var ext in exts)
                {
                    if (TryCandidate(dir, command + ext, out fullPath))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool TryCandidate(string dir, string fileName, out string fullPath)
    {
        fullPath = string.Empty;
        try
        {
            var candidate = Path.Combine(dir, fileName);
            if (!File.Exists(candidate))
                return false;

            // Windows is case-insensitive; recover the on-disk casing so callers
            // that display the resolved path get the real filename.
            var matches = Directory.GetFiles(dir, fileName);
            fullPath = matches.Length > 0 ? matches[0] : candidate;
            return true;
        }
        catch (ArgumentException)
        {
            // Illegal characters in the PATH entry; skip it.
        }
        return false;
    }
}
