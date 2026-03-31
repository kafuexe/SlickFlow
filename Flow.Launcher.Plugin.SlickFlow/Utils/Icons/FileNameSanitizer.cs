using System.IO;

namespace Flow.Launcher.Plugin.SlickFlow.Utils.Icons;

/// <summary>
/// Sanitizes strings for use as file names.
/// </summary>
internal static class FileNameSanitizer
{
    /// <summary>
    /// Strips illegal file name characters and trims leading/trailing dots.
    /// </summary>
    public static string MakeSafe(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (char ch in input)
        {
            if (Array.IndexOf(invalid, ch) < 0
                && ch != Path.DirectorySeparatorChar
                && ch != Path.AltDirectorySeparatorChar)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Trim('.').Trim();
    }
}
