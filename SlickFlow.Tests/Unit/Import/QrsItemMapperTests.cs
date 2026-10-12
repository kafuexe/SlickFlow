using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Import;

namespace SlickFlow.Tests.Unit.Import;

public class QrsItemMapperTests
{
    private static QrsEntry Entry(string section, params (string Key, string Value)[] values)
        => new(section, values.ToDictionary(v => v.Key, v => v.Value));

    private static QrsMapResult Map(QrsEntry entry, Func<string, bool>? fileExists = null)
        => QrsItemMapper.Map(entry, fileExists ?? (_ => false));

    [Fact]
    public void Map_CopiesBasicFields()
    {
        var result = Map(Entry("Notepad",
            ("Filename", @"C:\Windows\notepad.exe"),
            ("Params", "/A file.txt"),
            ("Path", @"C:\Temp"),
            ("Notes", "Plain text editor")));

        result.Error.Should().BeNull();
        var item = result.Item!;
        item.FileName.Should().Be(@"C:\Windows\notepad.exe");
        item.Arguments.Should().Be("/A file.txt");
        item.WorkingDir.Should().Be(@"C:\Temp");
        item.SubTitle.Should().Be("Plain text editor");
        item.Aliases.Should().Equal("notepad");
        item.RunAs.Should().Be(0);
        item.StartMode.Should().Be(0);
        item.ExecCount.Should().Be(0);
        item.IconPath.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Map_CommaSeparatedSectionName_BecomesMultipleAliases()
    {
        var item = Map(Entry("AMAZON, WWW.AMAZON.COM,,amazon", ("Filename", "https://amazon.com"))).Item!;

        item.Aliases.Should().Equal("amazon", "www.amazon.com");
    }

    [Fact]
    public void Map_MissingFilename_IsInvalid()
    {
        var result = Map(Entry("broken", ("Params", "-x")));

        result.Item.Should().BeNull();
        result.Error.Should().Contain("Filename");
    }

    [Fact]
    public void Map_BlankSectionName_IsInvalid()
    {
        var result = Map(Entry(" , ", ("Filename", "calc.exe")));

        result.Item.Should().BeNull();
        result.Error.Should().Contain("alias");
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("garbage", 0)]
    [InlineData("1", 0)]   // SW_SHOWNORMAL
    [InlineData("5", 0)]   // SW_SHOW (SlickRun's default)
    [InlineData("10", 0)]  // SW_SHOWDEFAULT
    [InlineData("2", 1)]   // SW_SHOWMINIMIZED
    [InlineData("6", 1)]   // SW_MINIMIZE
    [InlineData("7", 1)]   // SW_SHOWMINNOACTIVE
    [InlineData("11", 1)]  // SW_FORCEMINIMIZE
    [InlineData("3", 2)]   // SW_SHOWMAXIMIZED
    public void MapStartMode_UsesWin32ShowWindowValues(string? value, int expected)
    {
        QrsItemMapper.MapStartMode(value).Should().Be(expected);
    }

    [Fact]
    public void Map_StartMode_IsApplied()
    {
        Map(Entry("x", ("Filename", "a.exe"), ("StartMode", "3"))).Item!.StartMode.Should().Be(2);
    }

    [Theory]
    [InlineData("UseRunAs", "1", 1)]
    [InlineData("RunAs", "1", 1)]
    [InlineData("UseRunAs", "0", 0)]
    public void Map_RunAs_IsApplied(string key, string value, int expected)
    {
        Map(Entry("x", ("Filename", "a.exe"), (key, value))).Item!.RunAs.Should().Be(expected);
    }

    [Fact]
    public void Map_UnsupportedRunAsValue_FallsBackToCurrentUserWithWarning()
    {
        var result = Map(Entry("x", ("Filename", "a.exe"), ("UseRunAs", "2")));

        result.Item!.RunAs.Should().Be(0);
        result.Warnings.Should().ContainSingle().Which.Should().Contain("RunAs");
    }

    [Fact]
    public void Map_TranslatesWAndIAndCVariables()
    {
        var result = Map(Entry("g",
            ("Filename", "https://google.com/search?q=$W$"),
            ("Params", "--text \"$I$\" --clip $C$")));

        result.Item!.FileName.Should().Be("https://google.com/search?q=<<query>>");
        result.Item.Arguments.Should().Be("--text \"<<query>>\" --clip <<$clip>>");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Map_VariablesAreCaseInsensitive()
    {
        Map(Entry("g", ("Filename", "x$w$y"))).Item!.FileName.Should().Be("x<<query>>y");
    }

    [Fact]
    public void Map_UnknownVariable_IsLeftUntouchedAndWarned()
    {
        var result = Map(Entry("odd", ("Filename", "run.exe"), ("Params", "$D$ $W$ $FOO$")));

        result.Item!.Arguments.Should().Be("$D$ <<query>> $FOO$");
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("$D$").And.Contain("$FOO$").And.Contain("odd");
    }

    [Fact]
    public void Map_VariablesInWorkingDir_AreNotTranslatedButWarned()
    {
        var result = Map(Entry("odd", ("Filename", "run.exe"), ("Path", @"C:\$W$")));

        result.Item!.WorkingDir.Should().Be(@"C:\$W$");
        result.Warnings.Should().ContainSingle().Which.Should().Contain("$W$");
    }

    [Fact]
    public void Map_DollarSignsThatAreNotVariables_AreUntouched()
    {
        var result = Map(Entry("ps", ("Filename", "pwsh.exe"), ("Params", "-c \"$env:PATH; echo $5\"")));

        result.Item!.Arguments.Should().Be("-c \"$env:PATH; echo $5\"");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Map_ExistingIconFile_IsUsed()
    {
        var result = Map(Entry("x", ("Filename", "a.exe"), ("Icon", @"C:\icons\a.ico")),
            p => p == @"C:\icons\a.ico");

        result.Item!.IconPath.Should().Be(@"C:\icons\a.ico");
    }

    [Fact]
    public void Map_MissingIconFile_IsIgnored()
    {
        var result = Map(Entry("x", ("Filename", "a.exe"), ("Icon", @"C:\icons\gone.ico")));

        result.Item!.IconPath.Should().BeEmpty();
    }

    [Fact]
    public void Map_IgnoresUnknownKeys()
    {
        var result = Map(Entry("x", ("Filename", "a.exe"), ("GUID", "{123}"), ("Disable32BitRedir", "1")));

        result.Error.Should().BeNull();
        result.Warnings.Should().BeEmpty();
    }
}
