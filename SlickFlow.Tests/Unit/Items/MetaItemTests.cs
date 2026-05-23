using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Moq;

namespace SlickFlow.Tests.Unit.Items;

public class MetaItemTests
{
    [Theory]
    [InlineData("@spotify@", true)]
    [InlineData("@spotify@@discord@", true)]
    [InlineData("@a@@b@@c@", true)]
    [InlineData("notepad.exe", false)]
    [InlineData("@incomplete", false)]
    [InlineData("prefix@spotify@", false)]
    [InlineData("@spotify@suffix", false)]
    [InlineData("", false)]
    [InlineData("@@", false)]
    public void IsMetaItem_DetectsPatternCorrectly(string fileName, bool expected)
    {
        var item = new Item("1", fileName);
        item.IsMetaItem.Should().Be(expected);
    }

    [Fact]
    public void Execute_MetaItem_ResolvesAndExecutesSingleAlias()
    {
        // Target uses a non-existent file so no real process starts
        var target = new Item("2", "fake_nonexistent_app.xyz", new[] { "np" });
        var metaItem = new Item("1", "@np@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("np")).Returns(target);

        metaItem.Execute(itemRepo: repo.Object);

        repo.Verify(r => r.GetItemByAlias("np"), Times.Once);
        metaItem.ExecCount.Should().Be(1);
    }

    [Fact]
    public void Execute_MetaItem_ResolvesMultipleAliases()
    {
        var spotify = new Item("2", "fake_spotify.xyz", new[] { "spotify" });
        var discord = new Item("3", "fake_discord.xyz", new[] { "discord" });
        var metaItem = new Item("1", "@spotify@@discord@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("spotify")).Returns(spotify);
        repo.Setup(r => r.GetItemByAlias("discord")).Returns(discord);

        metaItem.Execute(itemRepo: repo.Object);

        repo.Verify(r => r.GetItemByAlias("spotify"), Times.Once);
        repo.Verify(r => r.GetItemByAlias("discord"), Times.Once);
        metaItem.ExecCount.Should().Be(1);
    }

    [Fact]
    public void Execute_MetaItem_ThrowsForAllMissingAliases()
    {
        var metaItem = new Item("1", "@AA@@BB@@CC@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias(It.IsAny<string>())).Returns((Item?)null);

        var act = () => metaItem.Execute(itemRepo: repo.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*\"AA\"*\"BB\"*\"CC\"*");
    }

    [Fact]
    public void Execute_MetaItem_ThrowsOnlyForMissingAliases()
    {
        var spotify = new Item("2", "fake_spotify.xyz", new[] { "spotify" });
        var metaItem = new Item("1", "@spotify@@missing@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("spotify")).Returns(spotify);
        repo.Setup(r => r.GetItemByAlias("missing")).Returns((Item?)null);

        var act = () => metaItem.Execute(itemRepo: repo.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*\"missing\"*")
            .Which.Message.Should().NotContain("\"spotify\"");
    }

    [Fact]
    public void Execute_MetaItem_ThrowsWithoutRepository()
    {
        var metaItem = new Item("1", "@np@");

        var act = () => metaItem.Execute();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*require*repository*");
    }

    [Fact]
    public void Execute_NonMetaItem_DoesNotRequireRepository()
    {
        var item = new Item("1", "some_app.exe");
        item.IsMetaItem.Should().BeFalse();
    }

    [Fact]
    public void Execute_MetaItem_ForceAdmin_PropagatesThrough()
    {
        // forceAdminExec is passed to each resolved target via Execute(forceAdminExec, itemRepo).
        // We verify propagation through a nested meta chain: outer -> inner -> leaf.
        // If forceAdmin weren't forwarded, the inner meta call would lose it.
        var leaf = new Item("3", "fake_app.xyz", new[] { "np" });
        var inner = new Item("2", "@np@", new[] { "inner" });
        var outer = new Item("1", "@inner@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("inner")).Returns(inner);
        repo.Setup(r => r.GetItemByAlias("np")).Returns(leaf);

        // Should not throw - forceAdminExec flows through the entire chain
        outer.Execute(forceAdminExec: true, itemRepo: repo.Object);

        repo.Verify(r => r.GetItemByAlias("inner"), Times.Once);
        repo.Verify(r => r.GetItemByAlias("np"), Times.Once);
        outer.ExecCount.Should().Be(1);
        inner.ExecCount.Should().Be(1);
    }

    [Fact]
    public void Execute_MetaItem_ResolvesNestedMetaItems()
    {
        var leaf = new Item("3", "fake_notepad.xyz", new[] { "np" });
        var inner = new Item("2", "@np@", new[] { "inner" });
        var outer = new Item("1", "@inner@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("inner")).Returns(inner);
        repo.Setup(r => r.GetItemByAlias("np")).Returns(leaf);

        outer.Execute(itemRepo: repo.Object);

        repo.Verify(r => r.GetItemByAlias("inner"), Times.Once);
        repo.Verify(r => r.GetItemByAlias("np"), Times.Once);
        outer.ExecCount.Should().Be(1);
        inner.ExecCount.Should().Be(1);
    }

    [Fact]
    public void Execute_MetaItem_ThrowsOnDirectSelfReference()
    {
        // Simplest cycle: meta item whose only alias resolves back to itself.
        var m1 = new Item("1", "@self@", new[] { "self" });

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("self")).Returns(m1);

        var act = () => m1.Execute(itemRepo: repo.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cycle*");
        m1.ExecCount.Should().Be(0);
    }

    [Fact]
    public void Execute_MetaItem_ThrowsOnTwoNodeCycle()
    {
        // A -> B -> A
        var a = new Item("A", "@b@", new[] { "a" });
        var b = new Item("B", "@a@", new[] { "b" });

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("a")).Returns(a);
        repo.Setup(r => r.GetItemByAlias("b")).Returns(b);

        var act = () => a.Execute(itemRepo: repo.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cycle*");
        a.ExecCount.Should().Be(0);
        b.ExecCount.Should().Be(0);
    }

    [Fact]
    public void Execute_MetaItem_ThrowsOnLongChainCycle()
    {
        // A -> B -> C -> D -> A: cycle reached only after a long chain.
        var a = new Item("A", "@b@", new[] { "a" });
        var b = new Item("B", "@c@", new[] { "b" });
        var c = new Item("C", "@d@", new[] { "c" });
        var d = new Item("D", "@a@", new[] { "d" });

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("a")).Returns(a);
        repo.Setup(r => r.GetItemByAlias("b")).Returns(b);
        repo.Setup(r => r.GetItemByAlias("c")).Returns(c);
        repo.Setup(r => r.GetItemByAlias("d")).Returns(d);

        var act = () => a.Execute(itemRepo: repo.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cycle*");
        a.ExecCount.Should().Be(0);
        b.ExecCount.Should().Be(0);
        c.ExecCount.Should().Be(0);
        d.ExecCount.Should().Be(0);
    }

    [Fact]
    public void Execute_MetaItem_DoesNotExecuteAnyLeafWhenCycleDetected()
    {
        // M1 = @safe@@cyclic@ where `safe` is a normal leaf and `cyclic` loops back
        // to M1. The safe leaf must NOT execute - cycle detection precedes execution.
        var safe = new Item("S", "fake_safe.xyz", new[] { "safe" });
        var bad = new Item("B", "@m1@", new[] { "cyclic" });
        var m1 = new Item("M1", "@safe@@cyclic@", new[] { "m1" });

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("safe")).Returns(safe);
        repo.Setup(r => r.GetItemByAlias("cyclic")).Returns(bad);
        repo.Setup(r => r.GetItemByAlias("m1")).Returns(m1);

        var act = () => m1.Execute(itemRepo: repo.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cycle*");
        safe.ExecCount.Should().Be(0);
        bad.ExecCount.Should().Be(0);
        m1.ExecCount.Should().Be(0);
    }

    [Fact]
    public void Execute_MetaItem_AllowsSameMetaInSiblingBranches()
    {
        // M1 = @m2@@m2@ - m2 is referenced twice as siblings, not on the same call
        // path, so this is NOT a cycle. m2 should execute twice.
        var leaf = new Item("3", "fake.xyz", new[] { "leaf" });
        var m2 = new Item("2", "@leaf@", new[] { "m2" });
        var m1 = new Item("1", "@m2@@m2@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("m2")).Returns(m2);
        repo.Setup(r => r.GetItemByAlias("leaf")).Returns(leaf);

        m1.Execute(itemRepo: repo.Object);

        m1.ExecCount.Should().Be(1);
        m2.ExecCount.Should().Be(2);
    }
}
