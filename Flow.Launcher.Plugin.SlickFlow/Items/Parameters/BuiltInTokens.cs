using System.Globalization;

namespace Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

/// <summary>
/// Built-in placeholder tokens that resolve automatically at launch time and
/// are never prompted for. A token is a placeholder whose name starts with
/// <c>$</c>; the keyword is case-insensitive, and anything after the first
/// <c>:</c> is the argument (passed through verbatim).
/// <list type="bullet">
///   <item><c>$clip</c> - clipboard text (empty when none).</item>
///   <item><c>$date</c> / <c>$date:FORMAT</c> - <see cref="DateTime.Now"/>, default <c>yyyy-MM-dd</c>.</item>
///   <item><c>$time</c> / <c>$time:FORMAT</c> - <see cref="DateTime.Now"/>, default <c>HH:mm</c>.</item>
///   <item><c>$env:NAME</c> - environment variable (empty when unset).</item>
/// </list>
/// Every <c>$</c> name is reserved: unknown or malformed tokens (and invalid
/// date formats) are not resolved and stay in the text literally, so a typo is
/// visible instead of silently becoming an empty string.
/// </summary>
public static class BuiltInTokens
{
    public const char Prefix = '$';

    private const string DefaultDateFormat = "yyyy-MM-dd";
    private const string DefaultTimeFormat = "HH:mm";

    public static bool IsToken(string? name) =>
        !string.IsNullOrEmpty(name) && name[0] == Prefix;

    public static bool TryResolve(string? token, ITokenSources sources, out string value)
    {
        value = string.Empty;
        if (!IsToken(token))
            return false;

        var body = token![1..];
        var colon = body.IndexOf(':');
        var keyword = colon < 0 ? body : body[..colon];
        var argument = colon < 0 ? null : body[(colon + 1)..];

        switch (keyword.ToLowerInvariant())
        {
            case "clip" when argument == null:
                value = sources.GetClipboardText() ?? string.Empty;
                return true;

            case "date":
                return TryFormat(sources.Now, argument, DefaultDateFormat, out value);

            case "time":
                return TryFormat(sources.Now, argument, DefaultTimeFormat, out value);

            case "env" when !string.IsNullOrEmpty(argument):
                value = sources.GetEnvironmentVariable(argument) ?? string.Empty;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves a placeholder default that is itself a token (e.g. the
    /// <c>$clip</c> in <c>&lt;&lt;query=$clip&gt;&gt;</c>). Plain defaults and
    /// unknown tokens are returned unchanged.
    /// </summary>
    public static string? ResolveDefault(string? defaultValue, ITokenSources sources)
    {
        return TryResolve(defaultValue, sources, out var resolved) ? resolved : defaultValue;
    }

    private static bool TryFormat(DateTime now, string? format, string fallback, out string value)
    {
        try
        {
            value = now.ToString(string.IsNullOrEmpty(format) ? fallback : format, CultureInfo.InvariantCulture);
            return true;
        }
        catch (FormatException)
        {
            value = string.Empty;
            return false;
        }
    }
}
