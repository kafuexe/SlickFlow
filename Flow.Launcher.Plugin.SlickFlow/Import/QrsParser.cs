using System.Globalization;
using System.IO;
using System.Text;

namespace Flow.Launcher.Plugin.SlickFlow.Import;

/// <summary>
/// One <c>[Section]</c> of a SlickRun MagicWord file, with its raw key/value pairs.
/// </summary>
public sealed class QrsEntry
{
    /// <summary>The raw section name, e.g. <c>AMAZON,WWW.AMAZON.COM</c>.</summary>
    public string SectionName { get; }

    /// <summary>The 1-based line of the section header (0 when built in code).</summary>
    public int LineNumber { get; }

    /// <summary>Key/value pairs with case-insensitive keys and surrounding quotes removed.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    public QrsEntry(string sectionName, IDictionary<string, string> values, int lineNumber = 0)
    {
        SectionName = sectionName;
        LineNumber = lineNumber;
        Values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns the value for <paramref name="key"/> (case-insensitive), or null when absent.</summary>
    public string? Get(string key) => Values.TryGetValue(key, out var v) ? v : null;
}

/// <summary>
/// Parses SlickRun's MagicWord database (<c>%APPDATA%\SlickRun\SlickRun.srl</c>) and its exported
/// shortcut packs (<c>*.qrs</c>). Both use the same INI-like layout, e.g. (from a real SlickRun 4.x file):
/// <code>
/// [AMAZON,WWW.AMAZON.COM]
/// Filename="https://www.amazon.com/"
/// Path=""
/// Params=""
/// Notes="Support Bayden Systems by shopping at Amazon"
/// GUID={38DB0E2A-7727-46C9-8DA3-331E0AD2801F}
/// StartMode=5
/// UseRunAs=0
/// </code>
/// The parser is deliberately tolerant: keys are case-insensitive, unknown keys are kept (the mapper
/// ignores them), blank lines / <c>;</c> and <c>#</c> comments / lines without <c>=</c> / keys before the
/// first section are skipped, duplicate section names are returned as separate entries (the importer
/// decides what to do with them) and, for duplicate keys inside a section, the last one wins.
/// It does no I/O besides <see cref="ParseFile"/> and touches no UI.
/// </summary>
public static class QrsParser
{
    /// <summary>Parses the text of a .qrs / .srl file.</summary>
    public static IReadOnlyList<QrsEntry> Parse(string? text)
    {
        var entries = new List<QrsEntry>();
        if (string.IsNullOrEmpty(text))
            return entries;

        string? section = null;
        int sectionLine = 0;
        Dictionary<string, string>? values = null;

        void Flush()
        {
            if (section != null && values != null)
                entries.Add(new QrsEntry(section, values, sectionLine));
        }

        using var reader = new StringReader(text);
        string? raw;
        int lineNo = 0;
        while ((raw = reader.ReadLine()) != null)
        {
            lineNo++;
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                Flush();
                section = line[1..^1].Trim();
                sectionLine = lineNo;
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (values == null)
                continue; // key outside any section

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = line[..eq].Trim();
            var value = Unquote(line[(eq + 1)..].Trim());
            values[key] = value;
        }

        Flush();
        return entries;
    }

    /// <summary>Reads and parses a .qrs / .srl file from disk.</summary>
    public static IReadOnlyList<QrsEntry> ParseFile(string path)
        => Parse(Decode(File.ReadAllBytes(path)));

    /// <summary>
    /// Decodes file bytes. SlickRun writes either UTF-8 (its <c>UseUTF8</c> option) or the system ANSI
    /// code page, so: honour a BOM if present (UTF-8 / UTF-16), otherwise try strict UTF-8 and fall back
    /// to the ANSI code page of the current culture when the bytes are not valid UTF-8.
    /// </summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return GetAnsiEncoding().GetString(bytes);
        }
    }

    private static Encoding GetAnsiEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            // Cultures without a legacy ANSI code page report UTF-8 (65001); use Windows-1252 then.
            return Encoding.GetEncoding(codePage == 65001 ? 1252 : codePage);
        }
        catch
        {
            return Encoding.Latin1;
        }
    }

    /// <summary>Removes one pair of surrounding double quotes; inner quotes are preserved.</summary>
    private static string Unquote(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
}
