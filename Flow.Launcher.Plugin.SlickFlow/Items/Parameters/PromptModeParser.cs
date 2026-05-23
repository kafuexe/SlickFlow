using System.Text;

namespace Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

/// <summary>
/// Snapshot of the prompt-mode query in the Flow Launcher bar.
/// <para>
/// Format: <c>&lt;alias&gt; | name1=value1 | ... | currentName: currentInput</c>.
/// The trailing <c>name: input</c> segment is the active prompt; the user is
/// typing <c>input</c> and pressing Enter advances to the next placeholder
/// or executes the item if all placeholders are filled.
/// </para>
/// </summary>
public record PromptModeState(
    string Alias,
    IReadOnlyList<(string Name, string Value)> Filled,
    string CurrentName,
    string CurrentInput);

public static class PromptModeParser
{
    private const string Separator = " | ";

    public static PromptModeState? TryParse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var segments = query.Split('|').Select(s => s.Trim()).ToList();
        if (segments.Count < 2)
            return null;

        var alias = segments[0];
        if (string.IsNullOrEmpty(alias))
            return null;

        var filled = new List<(string, string)>();
        for (int i = 1; i < segments.Count - 1; i++)
        {
            var seg = segments[i];
            var eq = seg.IndexOf('=');
            if (eq < 0)
                return null;
            var name = seg[..eq].Trim();
            var value = seg[(eq + 1)..].Trim();
            if (string.IsNullOrEmpty(name))
                return null;
            filled.Add((name, value));
        }

        var last = segments[^1];
        var colon = last.IndexOf(':');
        if (colon < 0)
            return null;
        var currentName = last[..colon].Trim();
        if (string.IsNullOrEmpty(currentName))
            return null;
        var currentInput = colon + 1 < last.Length ? last[(colon + 1)..].TrimStart() : "";

        return new PromptModeState(alias, filled, currentName, currentInput);
    }

    public static string Format(
        string alias,
        IEnumerable<(string Name, string Value)> filled,
        string nextName,
        string nextInitial)
    {
        var sb = new StringBuilder(alias);
        foreach (var (n, v) in filled)
            sb.Append(Separator).Append(n).Append('=').Append(v);
        sb.Append(Separator).Append(nextName).Append(": ").Append(nextInitial);
        return sb.ToString();
    }
}
