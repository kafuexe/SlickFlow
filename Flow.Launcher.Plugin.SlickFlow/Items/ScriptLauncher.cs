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
    /// installation folder. When empty, .py files fall back to the shell association.
    /// </summary>
    public static string PythonPath { get; set; } = string.Empty;

    public record LaunchTarget(string FileName, string Arguments, bool IsScript);

    public static LaunchTarget Resolve(string fileName, string arguments) =>
        Resolve(fileName, arguments, PythonPath);

    public static LaunchTarget Resolve(string fileName, string arguments, string? pythonPath)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        switch (extension)
        {
            case ".ps1":
                return new LaunchTarget(
                    "powershell.exe",
                    Join($"-NoProfile -ExecutionPolicy Bypass -File {Quote(fileName)}", arguments),
                    true);

            case ".py":
            case ".pyw":
                var interpreter = ResolvePythonExecutable(pythonPath, windowless: extension == ".pyw");
                if (interpreter == null)
                    return new LaunchTarget(fileName, arguments, false);

                return new LaunchTarget(interpreter, Join(Quote(fileName), arguments), true);

            default:
                return new LaunchTarget(fileName, arguments, false);
        }
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

    private static string Quote(string path) => $"\"{path.Trim('"')}\"";

    private static string Join(string head, string tail) =>
        string.IsNullOrWhiteSpace(tail) ? head : $"{head} {tail}";
}
