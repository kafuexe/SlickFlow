using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace SlickFlow.Tests.Unit.Items;

public class ItemTests
{
    [Fact]
    public void AddAlias_AddsNewAlias()
    {
        var item = new Item("1", "notepad.exe");
        item.AddAlias("np");
        item.Aliases.Should().Contain("np");
    }

    [Fact]
    public void AddAlias_IgnoresDuplicateCaseInsensitive()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        item.AddAlias("NP");
        item.Aliases.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveAlias_RemovesExistingAlias()
    {
        var item = new Item("1", "notepad.exe", new[] { "np", "note" });
        var removed = item.RemoveAlias("np");
        removed.Should().Be(1);
        item.Aliases.Should().NotContain("np");
    }

    [Fact]
    public void RemoveAlias_CaseInsensitive()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        var removed = item.RemoveAlias("NP");
        removed.Should().Be(1);
    }

    [Fact]
    public void RemoveAlias_ReturnsZeroForNonExistent()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        var removed = item.RemoveAlias("xyz");
        removed.Should().Be(0);
    }

    [Fact]
    public void MatchesQuery_MatchesFileName()
    {
        var item = new Item("1", "notepad.exe");
        item.MatchesQuery("notepad").Should().BeTrue();
    }

    [Fact]
    public void MatchesQuery_MatchesSubTitle()
    {
        var item = new Item("1", "notepad.exe") { SubTitle = "Text Editor" };
        item.MatchesQuery("editor").Should().BeTrue();
    }

    [Fact]
    public void MatchesQuery_MatchesAlias()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        item.MatchesQuery("np").Should().BeTrue();
    }

    [Fact]
    public void MatchesQuery_CaseInsensitive()
    {
        var item = new Item("1", "notepad.exe", new[] { "NP" });
        item.MatchesQuery("np").Should().BeTrue();
    }

    [Fact]
    public void MatchesQuery_ReturnsFalseForNull()
    {
        var item = new Item("1", "notepad.exe");
        item.MatchesQuery(null!).Should().BeFalse();
    }

    [Fact]
    public void MatchesQuery_ReturnsFalseForWhitespace()
    {
        var item = new Item("1", "notepad.exe");
        item.MatchesQuery("  ").Should().BeFalse();
    }

    [Fact]
    public void MatchesQuery_ReturnsFalseForNoMatch()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        item.MatchesQuery("xyz").Should().BeFalse();
    }

    [Fact]
    public void IsUrl_ReturnsTrueForHttp()
    {
        var item = new Item("1", "https://google.com");
        item.IsUrl("https://google.com").Should().BeTrue();
    }

    [Fact]
    public void IsUrl_ReturnsFalseForFilePath()
    {
        var item = new Item("1", "notepad.exe");
        item.IsUrl("notepad.exe").Should().BeFalse();
    }
}
