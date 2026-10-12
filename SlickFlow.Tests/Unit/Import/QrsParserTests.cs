using System.Text;
using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Import;

namespace SlickFlow.Tests.Unit.Import;

public class QrsParserTests
{
    // Shape taken from a real SlickRun 4.x database (SlickRun.srl / exported .qrs pack).
    private const string RealSample =
        "[AMAZON,WWW.AMAZON.COM]\r\n" +
        "Filename=\"https://www.amazon.com/\"\r\n" +
        "Path=\"\"\r\n" +
        "Params=\"\"\r\n" +
        "Notes=\"Shop at Amazon\"\r\n" +
        "GUID={38DB0E2A-7727-46C9-8DA3-331E0AD2801F}\r\n" +
        "StartMode=5\r\n" +
        "[GOOGLE]\r\n" +
        "Filename=\"https://www.google.com/search?hl=en&q=$W$\"\r\n" +
        "Path=\"\"\r\n" +
        "Params=\"\"\r\n" +
        "Notes=\"Search Google\"\r\n" +
        "GUID=\r\n" +
        "StartMode=5\r\n" +
        "UseRunAs=0\r\n";

    [Fact]
    public void Parse_RealSample_ReturnsOneEntryPerSection()
    {
        var entries = QrsParser.Parse(RealSample);

        entries.Should().HaveCount(2);
        entries[0].SectionName.Should().Be("AMAZON,WWW.AMAZON.COM");
        entries[1].SectionName.Should().Be("GOOGLE");
    }

    [Fact]
    public void Parse_StripsSurroundingQuotes()
    {
        var entry = QrsParser.Parse(RealSample)[1];

        entry.Get("Filename").Should().Be("https://www.google.com/search?hl=en&q=$W$");
        entry.Get("Notes").Should().Be("Search Google");
        entry.Get("Path").Should().BeEmpty();
    }

    [Fact]
    public void Parse_KeepsInnerQuotes()
    {
        var entries = QrsParser.Parse("[x]\nParams=\"\"C:\\My Dir\\a.txt\" -v\"\n");

        entries[0].Get("Params").Should().Be("\"C:\\My Dir\\a.txt\" -v");
    }

    [Fact]
    public void Parse_UnquotedValues_AreTrimmed()
    {
        var entries = QrsParser.Parse("[x]\nStartMode = 3 \nFilename=  notepad.exe\n");

        entries[0].Get("StartMode").Should().Be("3");
        entries[0].Get("Filename").Should().Be("notepad.exe");
    }

    [Fact]
    public void Parse_KeysAreCaseInsensitive()
    {
        var entries = QrsParser.Parse("[x]\nFILENAME=\"calc.exe\"\nparams=\"-a\"\n");

        entries[0].Get("Filename").Should().Be("calc.exe");
        entries[0].Get("Params").Should().Be("-a");
    }

    [Fact]
    public void Parse_MissingKey_ReturnsNull()
    {
        QrsParser.Parse("[x]\nFilename=a\n")[0].Get("Icon").Should().BeNull();
    }

    [Fact]
    public void Parse_IgnoresBlankLinesCommentsAndJunk()
    {
        const string text =
            "; leading comment\n" +
            "Orphan=ignored before any section\n" +
            "\n" +
            "[calc]\n" +
            "# hash comment\n" +
            "this line has no equals sign\n" +
            "   \n" +
            "Filename=calc.exe\n";

        var entries = QrsParser.Parse(text);

        entries.Should().ContainSingle();
        entries[0].Get("Filename").Should().Be("calc.exe");
        entries[0].Get("Orphan").Should().BeNull();
    }

    [Fact]
    public void Parse_ValueMayContainEqualsSign()
    {
        var entries = QrsParser.Parse("[g]\nFilename=\"https://x.com/?a=1&b=$W$\"\n");

        entries[0].Get("Filename").Should().Be("https://x.com/?a=1&b=$W$");
    }

    [Fact]
    public void Parse_DuplicateSectionNames_AreKeptAsSeparateEntries()
    {
        var entries = QrsParser.Parse("[g]\nFilename=a\n[G]\nFilename=b\n");

        entries.Should().HaveCount(2);
        entries[0].Get("Filename").Should().Be("a");
        entries[1].Get("Filename").Should().Be("b");
    }

    [Fact]
    public void Parse_DuplicateKeyInSection_LastWins()
    {
        QrsParser.Parse("[g]\nFilename=a\nFilename=b\n")[0].Get("Filename").Should().Be("b");
    }

    [Fact]
    public void Parse_EmptySection_IsStillReturned()
    {
        var entries = QrsParser.Parse("[empty]\n[next]\nFilename=x\n");

        entries.Should().HaveCount(2);
        entries[0].Values.Should().BeEmpty();
    }

    [Fact]
    public void Parse_RecordsSectionLineNumber()
    {
        var entries = QrsParser.Parse("; c\n\n[a]\nFilename=x\n[b]\n");

        entries[0].LineNumber.Should().Be(3);
        entries[1].LineNumber.Should().Be(5);
    }

    [Fact]
    public void Parse_EmptyOrNull_ReturnsNoEntries()
    {
        QrsParser.Parse("").Should().BeEmpty();
        QrsParser.Parse(null).Should().BeEmpty();
    }

    [Fact]
    public void Decode_Utf8WithBom_StripsBom()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("[café]\n")).ToArray();

        var text = QrsParser.Decode(bytes);

        text.Should().Be("[café]\n");
        QrsParser.Parse(text)[0].SectionName.Should().Be("café");
    }

    [Fact]
    public void Decode_Utf8WithoutBom_Works()
    {
        QrsParser.Decode(Encoding.UTF8.GetBytes("[naïve]")).Should().Be("[naïve]");
    }

    [Fact]
    public void Decode_Utf16LeWithBom_Works()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("[x]\nFilename=a")).ToArray();

        QrsParser.Decode(bytes).Should().Be("[x]\nFilename=a");
    }

    [Fact]
    public void Decode_InvalidUtf8_FallsBackToAnsiCodePage()
    {
        // 0xE9 alone is invalid UTF-8 but is 'é' in Windows-1252 (and most ANSI code pages).
        var bytes = new byte[] { (byte)'[', (byte)'c', (byte)'a', (byte)'f', 0xE9, (byte)']' };

        var text = QrsParser.Decode(bytes);

        text.Should().StartWith("[caf").And.EndWith("]");
        text.Should().NotContain("\uFFFD");
        text.Length.Should().Be(6);
    }

    [Fact]
    public void ParseFile_ReadsFromDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"slickflow_qrs_{Guid.NewGuid()}.qrs");
        try
        {
            File.WriteAllText(path, RealSample);

            QrsParser.ParseFile(path).Should().HaveCount(2);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
