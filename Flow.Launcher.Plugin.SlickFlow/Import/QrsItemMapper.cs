using System.IO;
using System.Text.RegularExpressions;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.Import;

/// <summary>Outcome of mapping one <see cref="QrsEntry"/>: either an <see cref="Item"/> or an error.</summary>
public sealed record QrsMapResult(Item? Item, IReadOnlyList<string> Warnings, string? Error);

/// <summary>
/// Maps a SlickRun MagicWord to a SlickFlow <see cref="Item"/>.
/// <para>Field mapping:</para>
/// <list type="bullet">
/// <item><c>[Name]</c> section → <see cref="Item.Aliases"/>. SlickRun stores several MagicWords for one
/// command as a comma-separated section name (<c>[AMAZON,WWW.AMAZON.COM]</c>), so each part becomes an
/// alias; aliases are trimmed and lower-cased like the <c>add</c> command does.</item>
/// <item><c>Filename</c> → <see cref="Item.FileName"/> (required; variables translated).</item>
/// <item><c>Params</c> → <see cref="Item.Arguments"/> (variables translated).</item>
/// <item><c>Path</c> (SlickRun's "Startup Path") → <see cref="Item.WorkingDir"/>. Not placeholder-aware
/// in SlickFlow, so variables there are left as-is and reported.</item>
/// <item><c>Notes</c> → <see cref="Item.SubTitle"/>.</item>
/// <item><c>UseRunAs</c> (what SlickRun writes) or <c>RunAs</c> → <see cref="Item.RunAs"/>: 1 = elevated.
/// SlickRun can also run as a different account; SlickFlow can't, so other values fall back to 0 with a warning.</item>
/// <item><c>StartMode</c> → <see cref="Item.StartMode"/>, see <see cref="MapStartMode"/>.</item>
/// <item><c>Icon</c> → <see cref="Item.IconPath"/>, only when the file exists.</item>
/// <item><c>GUID</c>, <c>Disable32BitRedir</c> and anything else are ignored.</item>
/// </list>
/// <para>Variable translation (in Filename and Params):</para>
/// <list type="bullet">
/// <item><c>$W$</c> (text typed after the MagicWord, URL-encoded by SlickRun) → <c>&lt;&lt;query&gt;&gt;</c></item>
/// <item><c>$I$</c> (the same typed text, unencoded) → <c>&lt;&lt;query&gt;&gt;</c>, so an entry using both prompts once</item>
/// <item><c>$C$</c> (clipboard contents) → <c>&lt;&lt;$clip&gt;&gt;</c> (built-in token)</item>
/// <item>Any other <c>$X$</c> is left untouched and reported as a warning.</item>
/// </list>
/// </summary>
public static class QrsItemMapper
{
    public const string QueryPlaceholder = "<<query>>";
    public const string ClipboardPlaceholder = "<<$clip>>";

    // $NAME$ where NAME starts with a letter, so "$5" or "$env:PATH" are not treated as variables.
    private static readonly Regex VariablePattern = new(@"\$([A-Za-z][A-Za-z0-9_]*)\$", RegexOptions.Compiled);

    /// <summary>Maps an entry; <paramref name="fileExists"/> defaults to <see cref="File.Exists(string)"/>.</summary>
    public static QrsMapResult Map(QrsEntry entry, Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        var warnings = new List<string>();
        var name = entry.SectionName;

        var aliases = SplitAliases(name);
        if (aliases.Count == 0)
            return new QrsMapResult(null, warnings, "no usable alias in section name");

        var fileName = entry.Get("Filename")?.Trim();
        if (string.IsNullOrEmpty(fileName))
            return new QrsMapResult(null, warnings, "missing Filename");

        var unknown = new List<string>();
        var item = new Item
        {
            Aliases = aliases,
            FileName = TranslateVariables(fileName, unknown),
            Arguments = TranslateVariables(entry.Get("Params") ?? string.Empty, unknown),
            WorkingDir = entry.Get("Path")?.Trim() ?? string.Empty,
            SubTitle = entry.Get("Notes") ?? string.Empty,
            StartMode = MapStartMode(entry.Get("StartMode")),
            RunAs = MapRunAs(entry.Get("UseRunAs") ?? entry.Get("RunAs"), name, warnings),
        };

        foreach (Match m in VariablePattern.Matches(item.WorkingDir))
            unknown.Add(m.Value);

        if (unknown.Count > 0)
        {
            var list = string.Join(", ", unknown.Distinct(StringComparer.OrdinalIgnoreCase));
            warnings.Add($"{name}: unsupported SlickRun variable(s) left as-is: {list}");
        }

        var icon = entry.Get("Icon")?.Trim();
        if (!string.IsNullOrEmpty(icon) && fileExists(icon))
            item.IconPath = icon;

        return new QrsMapResult(item, warnings, null);
    }

    /// <summary>
    /// SlickRun stores the Win32 <c>ShowWindow</c> constant (its default is 5, SW_SHOW).
    /// Minimized: 2 SW_SHOWMINIMIZED, 6 SW_MINIMIZE, 7 SW_SHOWMINNOACTIVE, 11 SW_FORCEMINIMIZE → 1.
    /// Maximized: 3 SW_SHOWMAXIMIZED → 2. Everything else (1, 4, 5, 8, 9, 10, missing, garbage) → 0 normal.
    /// </summary>
    public static int MapStartMode(string? value)
    {
        if (!int.TryParse(value?.Trim(), out var mode))
            return 0;

        return mode switch
        {
            2 or 6 or 7 or 11 => 1,
            3 => 2,
            _ => 0,
        };
    }

    /// <summary>Translates $W$, $I$ and $C$; collects any other $X$ variable into <paramref name="unknown"/>.</summary>
    public static string TranslateVariables(string text, ICollection<string> unknown)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return VariablePattern.Replace(text, m =>
        {
            switch (m.Groups[1].Value.ToUpperInvariant())
            {
                case "W":
                case "I": return QueryPlaceholder;
                case "C": return ClipboardPlaceholder;
                default:
                    unknown.Add(m.Value);
                    return m.Value;
            }
        });
    }

    private static List<string> SplitAliases(string sectionName)
        => sectionName.Split(',')
            .Select(a => a.Trim().ToLowerInvariant())
            .Where(a => a.Length > 0)
            .Distinct()
            .ToList();

    private static int MapRunAs(string? value, string name, List<string> warnings)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == "0")
            return 0;
        if (trimmed == "1")
            return 1;

        warnings.Add($"{name}: RunAs value '{trimmed}' is not supported (only elevated/admin is); imported as normal user");
        return 0;
    }
}
