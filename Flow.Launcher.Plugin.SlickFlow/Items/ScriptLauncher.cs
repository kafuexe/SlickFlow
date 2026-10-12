using System.IO;

namespace Flow.Launcher.Plugin.SlickFlow.Items;

/// <summary>
/// Rewrites script targets (.ps1, .py, .pyw) into an interpreter invocation so they
/// run instead of opening in whatever editor the file association points at.
/// </summary>
public static class ScriptLauncher
{
    /// <summary>
    /// Python installation configured in settings. May be a python.exe path or the
    /// installation folder. When empty, an interpreter is detected on PATH.
    /// </summary>
    public static string PythonPath { get; set; } = string.Empty;

    /// <summary>
    /// Run .ps1 scripts with PowerShell 7 (pwsh.exe) instead of Windows PowerShell.
    /// Falls back to powershell.exe when pwsh isn't installed.
    /// </summary>
    public static bool UsePwsh { get; set; }

    public record LaunchTarget(string FileName, string Arguments, bool IsScript);

    public static LaunchTarget Resolve(string fileName, string arguments) =>
        Resolve(fileName, arguments, PythonPath, UsePwsh);

    /// <param name="findExecutable">
    /// Locates an executable by file name, returning its full path or null.
    /// Defaults to a PATH search; injectable for tests.
    /// </param>
    public static LaunchTarget Resolve(
        string fileName,
        string arguments,
        string? pythonPath,
        bool usePwsh = false,
        Func<string, string?>? findExecutable = null)
    {
        findExecutable ??= FindExecutable;
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        switch (extension)
        {
            case ".ps1":
                return new LaunchTarget(
                    ResolvePowerShell(usePwsh, findExecutable),
                    Join($"-NoProfile -ExecutionPolicy Bypass -File {Quote(fileName)}", arguments),
                    true);

            case ".py":
            case ".pyw":
                var interpreter = ResolvePythonExecutable(pythonPath, windowless: extension == ".pyw")
                    ?? DetectPython(windowless: extension == ".pyw", findExecutable);
                if (interpreter == null)
                    return new LaunchTarget(fileName, arguments, false);

                return new LaunchTarget(interpreter, Join(Quote(fileName), arguments), true);

            default:
                return new LaunchTarget(fileName, arguments, false);
        }
    }

    public static string ResolvePowerShell(bool usePwsh, Func<string, string?>? findExecutable = null)
    {
        findExecutable ??= FindExecutable;
        if (usePwsh)
        {
            var pwsh = findExecutable("pwsh.exe");
            if (pwsh != null)
                return pwsh;
        }

        return "powershell.exe";
    }

    /// <summary>
    /// Returns the interpreter executable for the configured installation, or null
    /// when nothing usable is configured.
    /// </summary>
    public static string? ResolvePythonExecutable(string? pythonPath, bool windowless = false)
    {
        if (string.IsNullOrWhiteSpace(pythonPath))
            return null;

        var path = pythonPath.Trim().Trim('"');
        var exeName = windowless ? "pythonw.exe" : "python.exe";

        if (Directory.Exists(path))
        {
            var candidate = Path.Combine(path, exeName);
            if (File.Exists(candidate))
                return candidate;

            // Fall back to python.exe when the installation has no pythonw.exe.
            candidate = Path.Combine(path, "python.exe");
            return File.Exists(candidate) ? candidate : null;
        }

        if (!File.Exists(path))
            return null;

        // An explicit python.exe was chosen; prefer its sibling pythonw.exe for .pyw.
        if (windowless)
        {
            var sibling = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, "pythonw.exe");
            if (File.Exists(sibling))
                return sibling;
        }

        return path;
    }

    /// <summary>
    /// Finds an installed interpreter when none is configured. Prefers the official
    /// py launcher (it picks the newest installed Python), then python on PATH.
    /// </summary>
    public static string? DetectPython(bool windowless, Func<string, string?>? findExecutable = null)
    {
        findExecutable ??= FindExecutable;

        var candidates = windowless
            ? new[] { "pyw.exe", "py.exe", "pythonw.exe", "python.exe" }
            : new[] { "py.exe", "python.exe" };

        foreach (var candidate in candidates)
        {
            var found = findExecutable(candidate);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Searches PATH plus well-known install locations for <paramref name="exeName"/>.
    /// Skips the WindowsApps folder: its python.exe is a Store stub that opens the
    /// Microsoft Store instead of running anything when Python isn't installed.
    /// </summary>
    public static string? FindExecutable(string exeName)
    {
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(WellKnownDirectories());

        foreach (var dir in dirs)
        {
            var trimmed = dir.Trim().Trim('"');
            if (trimmed.Length == 0 || trimmed.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var candidate = Path.Combine(trimmed, exeName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry - ignore it.
            }
        }

        return null;
    }

    private static IEnumerable<string> WellKnownDirectories()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return Path.Combine(programFiles, "PowerShell", "7");
        yield return Path.Combine(localAppData, "Programs", "Python", "Launcher");
    }

    private static string Quote(string path) => $"\"{path.Trim('"')}\"";

    private static string Join(string head, string tail) =>
        string.IsNullOrWhiteSpace(tail) ? head : $"{head} {tail}";
}
