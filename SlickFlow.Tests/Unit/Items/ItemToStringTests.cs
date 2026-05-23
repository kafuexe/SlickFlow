using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace SlickFlow.Tests.Unit.Items;

public class ItemToStringTests
{
    [Fact]
    public void ToString_WithAliases_FormatsCorrectly()
    {
        var item = new Item("42", "notepad.exe", new[] { "np", "note" })
        {
            Arguments = "-f test.txt",
            RunAs = 1,
            StartMode = 2,
            ExecCount = 5
        };

        var result = item.ToString();

        result.Should().Contain("[#42]");
        result.Should().Contain("notepad.exe");
        result.Should().Contain("-f test.txt");
        result.Should().Contain("np, note");
        result.Should().Contain("RunAs=1");
        result.Should().Contain("StartMode=2");
        result.Should().Contain("ExecCount=5");
    }

    [Fact]
    public void ToString_NoAliases_ShowsNone()
    {
        var item = new Item("1", "calc.exe");

        var result = item.ToString();

        result.Should().Contain("Aliases=[none]");
    }

    [Fact]
    public void DefaultConstructor_SetsDefaults()
    {
        var item = new Item();

        item.Id.Should().Be(string.Empty);
        item.FileName.Should().Be(string.Empty);
        item.Arguments.Should().Be(string.Empty);
        item.SubTitle.Should().Be(string.Empty);
        item.WorkingDir.Should().Be(string.Empty);
        item.IconPath.Should().Be(string.Empty);
        item.RunAs.Should().Be(0);
        item.StartMode.Should().Be(0);
        item.ExecCount.Should().Be(0);
        item.Aliases.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullAliases_UsesEmptyList()
    {
        var item = new Item("1", "test.exe", null);

        item.Aliases.Should().BeEmpty();
    }
}
