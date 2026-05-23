using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace SlickFlow.Tests.Unit.Items;

public class ItemSearcherTests
{
    private readonly ItemSearcher _searcher = new();

    private static Item CreateItem(string id, string fileName, params string[] aliases)
    {
        return new Item(id, fileName, aliases);
    }

    [Fact]
    public void Search_ExactMatch_ScoresHighest()
    {
        var items = new List<Item> { CreateItem("1", "notepad.exe", "np") };
        var results = _searcher.Search("np", items);
        results.Should().HaveCount(1);
        results[0].score.Should().BeGreaterThan(2000);
    }

    [Fact]
    public void Search_PrefixMatch_ScoresHigh()
    {
        var items = new List<Item> { CreateItem("1", "notepad.exe", "notepad") };
        var results = _searcher.Search("note", items);
        results.Should().HaveCount(1);
        // startsWith(800) + lengthBonus(44) = 844
        results[0].score.Should().BeGreaterThan(800);
    }

    [Fact]
    public void Search_ContainsMatch_QueryContainsAlias()
    {
        var items = new List<Item> { CreateItem("1", "x.exe", "foo") };
        var results = _searcher.Search("foobar", items);
        results.Should().HaveCount(1);
        results[0].score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Search_SuffixMatch()
    {
        var items = new List<Item> { CreateItem("1", "x.exe", "notepad") };
        var results = _searcher.Search("pad", items);
        results.Should().HaveCount(1);
        results[0].score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Search_LevenshteinDistance1_GetsBonus()
    {
        var items = new List<Item> { CreateItem("1", "x.exe", "np") };
        var results = _searcher.Search("mp", items);
        results.Should().HaveCount(1);
        results[0].score.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var items = new List<Item> { CreateItem("1", "notepad.exe", "np") };
        var results = _searcher.Search("zzzzz", items);
        results.Should().BeEmpty();
    }

    [Fact]
    public void Search_CaseInsensitive()
    {
        var items = new List<Item> { CreateItem("1", "notepad.exe", "NP") };
        var results = _searcher.Search("np", items);
        results.Should().HaveCount(1);
    }

    [Fact]
    public void Search_ResultsSortedByScoreDescending()
    {
        // Use aliases where both match: "np" exact matches "np", "nps" starts with "np"
        var items = new List<Item>
        {
            CreateItem("1", "x.exe", "nps"),
            CreateItem("2", "y.exe", "np")
        };
        var results = _searcher.Search("np", items);
        results.Should().HaveCountGreaterThanOrEqualTo(2);
        // "np" exact match should score higher than "nps" prefix match
        results[0].name.Should().Be("np");
    }

    [Fact]
    public void Search_MultipleAliasesOnSameItem()
    {
        var items = new List<Item> { CreateItem("1", "x.exe", "foo", "bar") };
        var results = _searcher.Search("foo", items);
        results.Should().Contain(r => r.name == "foo");
    }

    [Fact]
    public void Search_AdditiveScoring_ExactMatchGetsAllBonuses()
    {
        var items = new List<Item> { CreateItem("1", "x.exe", "np") };
        var results = _searcher.Search("np", items);
        // exact(1000) + startsWith(800) + contains(400) + endsWith(50) + lengthBonus(50) = 2300
        results[0].score.Should().Be(2300);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        var items = new List<Item> { CreateItem("1", "x.exe", "np") };
        var results = _searcher.Search("", items);
        results.Should().BeEmpty();
    }
}
