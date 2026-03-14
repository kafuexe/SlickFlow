using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Commands;

namespace SlickFlow.Tests.Unit.Commands;

public class CommandParserTests
{
    [Fact]
    public void SplitArgs_SimpleArguments()
    {
        var result = CommandParser.SplitArgs("add np notepad.exe");
        result.Should().Equal("add", "np", "notepad.exe");
    }

    [Fact]
    public void SplitArgs_QuotedString_PreservesContent()
    {
        var result = CommandParser.SplitArgs("add np \"C:\\Program Files\\app.exe\"");
        result.Should().Equal("add", "np", "C:\\Program Files\\app.exe");
    }

    [Fact]
    public void SplitArgs_SingleArgument()
    {
        var result = CommandParser.SplitArgs("add");
        result.Should().Equal("add");
    }

    [Fact]
    public void SplitArgs_EmptyString_ReturnsEmpty()
    {
        var result = CommandParser.SplitArgs("");
        result.Should().BeEmpty();
    }

    [Fact]
    public void SplitArgs_MultipleSpaces_IgnoresExtra()
    {
        var result = CommandParser.SplitArgs("add  np  notepad.exe");
        result.Should().Equal("add", "np", "notepad.exe");
    }
}
