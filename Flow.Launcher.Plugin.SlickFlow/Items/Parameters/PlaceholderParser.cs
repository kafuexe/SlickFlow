using System.Text;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

public static class PlaceholderParser
{
    // <<NAME(=DEFAULT)?(|HINT)?>>
    // Name disallows < > = |. Default disallows < > |. Hint disallows < >.
    private static readonly Regex Pattern = new(
        @"<<([^<>=|]+)(?:=([^<>|]*))?(?:\|([^<>]*))?>>",
        RegexOptions.Compiled);

    public static IEnumerable<Placeholder> Extract(string? text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        foreach (Match m in Pattern.Matches(text))
        {
            yield return new Placeholder(
                Name: m.Groups[1].Value,
                Default: m.Groups[2].Success ? m.Groups[2].Value : null,
                Hint: m.Groups[3].Success ? m.Groups[3].Value : null);
        }
    }

    public static bool ContainsPlaceholders(string? text)
    {
        return !string.IsNullOrEmpty(text) && Pattern.IsMatch(text);
    }

    /// <summary>
    /// Replaces each placeholder with its supplied value. Falls back to the
    /// placeholder's default when missing from <paramref name="values"/>, and
    /// leaves the placeholder literal in place when neither is available.
    /// <para>
    /// When <paramref name="tokens"/> is supplied, built-in tokens
    /// (<c>&lt;&lt;$clip&gt;&gt;</c>, <c>&lt;&lt;$date&gt;&gt;</c>, ...) are resolved
    /// against it, as are defaults that are tokens (<c>&lt;&lt;q=$clip&gt;&gt;</c>).
    /// Without it, and for unknown tokens, the token text is left literal.
    /// </para>
    /// </summary>
    public static string Substitute(
        string? text,
        IReadOnlyDictionary<string, string> values,
        ITokenSources? tokens = null)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        var sb = new StringBuilder(text.Length);
        int last = 0;

        foreach (Match m in Pattern.Matches(text))
        {
            sb.Append(text, last, m.Index - last);

            var name = m.Groups[1].Value;
            if (BuiltInTokens.IsToken(name))
            {
                // Tokens are never prompted, so supplied values never apply to them.
                if (tokens != null && BuiltInTokens.TryResolve(name, tokens, out var resolved))
                    sb.Append(resolved);
                else
                    sb.Append(m.Value);
            }
            else if (values.TryGetValue(name, out var supplied))
                sb.Append(supplied);
            else if (m.Groups[2].Success)
                sb.Append(tokens != null
                    ? BuiltInTokens.ResolveDefault(m.Groups[2].Value, tokens)
                    : m.Groups[2].Value);
            else
                sb.Append(m.Value);

            last = m.Index + m.Length;
        }

        sb.Append(text, last, text.Length - last);
        return sb.ToString();
    }
}
