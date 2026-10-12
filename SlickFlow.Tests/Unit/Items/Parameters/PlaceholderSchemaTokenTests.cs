using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;
using Moq;

namespace SlickFlow.Tests.Unit.Items.Parameters;

public class PlaceholderSchemaTokenTests
{
    [Fact]
    public void From_TokenOnlyItem_ReturnsEmpty()
    {
        var item = new Item("1", "https://translate.google.com/?text=<<$clip>>")
        {
            Arguments = "<<$date>> <<$time:HH>> <<$env:PATH>>"
        };

        PlaceholderSchema.From(item).Should().BeEmpty();
    }

    [Fact]
    public void From_UnknownToken_IsExcluded()
    {
        PlaceholderSchema.From(new Item("1", "x <<$nope>>")).Should().BeEmpty();
    }

    [Fact]
    public void From_MixedItem_ReturnsOnlyRealPlaceholders()
    {
        var item = new Item("1", "notepad C:\\logs\\<<$date>>-<<name>>.txt")
        {
            Arguments = "<<$clip>> <<level=info>>"
        };

        var result = PlaceholderSchema.From(item);

        result.Select(p => p.Name).Should().Equal("name", "level");
    }

    [Fact]
    public void From_TokenAsDefault_KeepsPlaceholderWithRawTokenDefault()
    {
        var result = PlaceholderSchema.From(new Item("1", "g?q=<<query=$clip>>"));

        result.Should().ContainSingle();
        result[0].Name.Should().Be("query");
        result[0].Default.Should().Be("$clip");
    }

    [Fact]
    public void From_MetaItemWhoseLeavesUseOnlyTokens_ReturnsEmpty()
    {
        var a = new Item("A", "app.exe", new[] { "a" }) { Arguments = "<<$clip>>" };
        var b = new Item("B", "https://x/<<$date>>", new[] { "b" });
        var meta = new Item("M", "@a@@b@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("a")).Returns(a);
        repo.Setup(r => r.GetItemByAlias("b")).Returns(b);

        PlaceholderSchema.From(meta, repo.Object).Should().BeEmpty();
    }

    [Fact]
    public void From_MetaItemWithMixedLeaves_ReturnsOnlyRealPlaceholders()
    {
        var a = new Item("A", "app.exe", new[] { "a" }) { Arguments = "<<$clip>> <<port>>" };
        var b = new Item("B", "https://x/<<$date>>/<<page>>", new[] { "b" });
        var meta = new Item("M", "@a@@b@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("a")).Returns(a);
        repo.Setup(r => r.GetItemByAlias("b")).Returns(b);

        PlaceholderSchema.From(meta, repo.Object)
            .Select(p => p.Name).Should().Equal("port", "page");
    }
}
