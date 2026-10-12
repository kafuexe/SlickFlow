using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Import;

namespace SlickFlow.Tests.Unit.Import;

/// <summary>
/// SlickRun's built-in commands (<c>Filename="@NAME@"</c>), samples taken from a real SlickRun.srl.
/// </summary>
public class QrsBuiltInMapperTests
{
    private static QrsEntry Entry(string section, params (string Key, string Value)[] values)
        => new(section, values.ToDictionary(v => v.Key, v => v.Value));

    private static QrsMapResult Map(QrsEntry entry) => QrsItemMapper.Map(entry, _ => false);

    [Fact]
    public void Multi_BecomesMetaItemMixingAliasesAndDirectTargets()
    {
        var result = Map(Entry("MORNING",
            ("Filename", "@MULTI@"),
            ("Params", "https://news.google.com@MAIL@notepad.exe")));

        result.Error.Should().BeNull();
        var item = result.Item!;
        item.FileName.Should().Be("@https://news.google.com@@mail@@notepad.exe@");
        item.Arguments.Should().BeEmpty();
        item.IsMetaItem.Should().BeTrue();
        item.Aliases.Should().Equal("morning");
    }

    [Fact]
    public void Multi_IsCaseInsensitive_AndTrimsAndDropsEmptyParts()
    {
        var item = Map(Entry("m", ("Filename", " @multi@ "), ("Params", " a @@ C:\\x.exe @"))).Item!;

        item.FileName.Should().Be("@a@@C:\\x.exe@");
    }

    [Fact]
    public void Multi_TranslatesVariablesInsideParts()
    {
        var item = Map(Entry("m", ("Filename", "@MULTI@"),
            ("Params", "https://google.com/search?q=$W$@https://bing.com/?q=$I$"))).Item!;

        item.FileName.Should().Be("@https://google.com/search?q=<<query>>@@https://bing.com/?q=<<query>>@");
    }

    [Fact]
    public void Multi_WithoutParts_IsInvalid()
    {
        var result = Map(Entry("m", ("Filename", "@MULTI@"), ("Params", "")));

        result.Item.Should().BeNull();
        result.Unsupported.Should().BeFalse();
        result.Error.Should().Contain("@MULTI@");
    }

    [Theory]
    [InlineData("@SHUTDOWN@", "shutdown.exe", "/s /t 0")]
    [InlineData("@REBOOT@", "shutdown.exe", "/r /t 0")]
    [InlineData("@LOGOFF@", "shutdown.exe", "/l")]
    [InlineData("@HIBERNATE@", "shutdown.exe", "/h")]
    [InlineData("@STANDBY@", "rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0")]
    [InlineData("@LOCK@", "rundll32.exe", "user32.dll,LockWorkStation")]
    [InlineData("@SCREENSAVE@", "scrnsave.scr", "/s")]
    [InlineData("@shutdown@", "shutdown.exe", "/s /t 0")]
    public void PowerCommands_MapToWindowsEquivalents(string filename, string exe, string args)
    {
        var result = Map(Entry("x", ("Filename", filename), ("Params", "ignored"), ("Notes", "note")));

        result.Error.Should().BeNull();
        result.Item!.FileName.Should().Be(exe);
        result.Item.Arguments.Should().Be(args);
        result.Item.SubTitle.Should().Be("note");
        result.Item.IsMetaItem.Should().BeFalse();
    }

    [Theory]
    [InlineData("@calendar@")]
    [InlineData("@SETUP@")]
    [InlineData("@SOMETHINGNEW@")]
    public void OtherBuiltIns_AreUnsupported(string filename)
    {
        var result = Map(Entry("cal", ("Filename", filename)));

        result.Item.Should().BeNull();
        result.Unsupported.Should().BeTrue();
        result.Error.Should().Contain(filename);
    }

    [Fact]
    public void RepeatedVariable_IsTranslatedEverywhere()
    {
        var item = Map(Entry("both",
            ("Filename", "https://x.com/?a=$W$&b=$W$"),
            ("Params", "$W$ $w$"))).Item!;

        item.FileName.Should().Be("https://x.com/?a=<<query>>&b=<<query>>");
        item.Arguments.Should().Be("<<query>> <<query>>");
    }
}
