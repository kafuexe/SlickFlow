using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;
using Moq;

namespace SlickFlow.Tests.Unit.Items.Parameters;

public class PlaceholderSchemaTests
{
    [Fact]
    public void From_LeafItemWithoutPlaceholders_ReturnsEmpty()
    {
        var item = new Item("1", "notepad.exe");
        PlaceholderSchema.From(item).Should().BeEmpty();
    }

    [Fact]
    public void From_LeafItemWithFileNamePlaceholder_ReturnsIt()
    {
        var item = new Item("1", "http://localhost:<<port>>");
        var result = PlaceholderSchema.From(item);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("port");
    }

    [Fact]
    public void From_LeafItem_ScansFileNameBeforeArguments()
    {
        var item = new Item("1", "<<host>>") { Arguments = "<<port>>" };

        var result = PlaceholderSchema.From(item);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("host");
        result[1].Name.Should().Be("port");
    }

    [Fact]
    public void From_LeafItem_DeduplicatesByName_KeepingFirstOccurrence()
    {
        // Same name in FileName (with default) and Arguments (no default).
        // First occurrence (with default) wins.
        var item = new Item("1", "<<port=8080>>") { Arguments = "--port <<port>>" };

        var result = PlaceholderSchema.From(item);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("port");
        result[0].Default.Should().Be("8080");
    }

    [Fact]
    public void From_MetaItemRequiresRepository()
    {
        var meta = new Item("1", "@a@");

        var act = () => PlaceholderSchema.From(meta);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*repository*");
    }

    [Fact]
    public void From_MetaChain_CollectsFromAllParameterizedLeaves()
    {
        var leafA = new Item("A", "<<host>>", new[] { "a" });
        var leafB = new Item("B", "script.exe", new[] { "b" }) { Arguments = "--port <<port>>" };
        var meta = new Item("M", "@a@@b@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("a")).Returns(leafA);
        repo.Setup(r => r.GetItemByAlias("b")).Returns(leafB);

        var result = PlaceholderSchema.From(meta, repo.Object);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("host");
        result[1].Name.Should().Be("port");
    }

    [Fact]
    public void From_MetaChain_DeduplicatesAcrossLeaves()
    {
        // Two leaves both use <<port>>. Schema returns one entry, from first encounter.
        var leafA = new Item("A", "http://x:<<port=8080>>", new[] { "a" });
        var leafB = new Item("B", "http://y:<<port>>", new[] { "b" });
        var meta = new Item("M", "@a@@b@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("a")).Returns(leafA);
        repo.Setup(r => r.GetItemByAlias("b")).Returns(leafB);

        var result = PlaceholderSchema.From(meta, repo.Object);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("port");
        result[0].Default.Should().Be("8080");
    }

    [Fact]
    public void From_MetaChain_NestedMetaItems_WalksThrough()
    {
        var leaf = new Item("3", "<<port>>", new[] { "np" });
        var inner = new Item("2", "@np@", new[] { "inner" });
        var outer = new Item("1", "@inner@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("inner")).Returns(inner);
        repo.Setup(r => r.GetItemByAlias("np")).Returns(leaf);

        var result = PlaceholderSchema.From(outer, repo.Object);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("port");
    }

    [Fact]
    public void From_MetaChain_ThrowsOnCycle()
    {
        var a = new Item("A", "@b@", new[] { "a" });
        var b = new Item("B", "@a@", new[] { "b" });

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("a")).Returns(a);
        repo.Setup(r => r.GetItemByAlias("b")).Returns(b);

        var act = () => PlaceholderSchema.From(a, repo.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cycle*");
    }

    [Fact]
    public void From_MetaChain_ThrowsOnMissingAlias()
    {
        var meta = new Item("1", "@missing@");
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("missing")).Returns((Item?)null);

        var act = () => PlaceholderSchema.From(meta, repo.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown aliases*");
    }
}
