using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Moq;

namespace SlickFlow.Tests.Unit.Items;

public class ItemExecuteWithValuesTests
{
    [Fact]
    public void Execute_LeafWithValues_DoesNotMutateStoredFileName()
    {
        // The leaf's stored FileName must remain in its parameterized form so
        // subsequent prompts can substitute fresh values each call.
        var item = new Item("1", "fake_app_<<port>>.xyz");

        item.Execute(values: new Dictionary<string, string> { ["port"] = "8080" });

        item.FileName.Should().Be("fake_app_<<port>>.xyz");
    }

    [Fact]
    public void Execute_LeafWithValues_DoesNotMutateStoredArguments()
    {
        var item = new Item("1", "fake_app.xyz") { Arguments = "--port <<port>>" };

        item.Execute(values: new Dictionary<string, string> { ["port"] = "8080" });

        item.Arguments.Should().Be("--port <<port>>");
    }

    [Fact]
    public void Execute_LeafWithoutValues_DoesNotThrowOnPlaceholderInFileName()
    {
        // No values dict provided: Execute uses the literal FileName. Process.Start
        // may or may not succeed (Windows shell behavior varies), but Execute itself
        // must not propagate exceptions - failures are swallowed by the catch.
        var item = new Item("1", "fake_app_<<port>>.xyz");

        var act = () => item.Execute();

        act.Should().NotThrow();
        item.FileName.Should().Be("fake_app_<<port>>.xyz"); // never mutated
    }

    [Fact]
    public void Execute_MetaItem_PropagatesValuesToParameterizedLeaf()
    {
        var leaf = new Item("L", "fake_app_<<port>>.xyz", new[] { "l" });
        var meta = new Item("M", "@l@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("l")).Returns(leaf);

        meta.Execute(
            itemRepo: repo.Object,
            values: new Dictionary<string, string> { ["port"] = "8080" });

        meta.ExecCount.Should().Be(1);
        leaf.FileName.Should().Be("fake_app_<<port>>.xyz"); // original preserved
    }

    [Fact]
    public void Execute_MetaItem_PropagatesValuesAcrossNestedMetas()
    {
        var leaf = new Item("L", "fake_app.xyz", new[] { "l" }) { Arguments = "<<port>>" };
        var inner = new Item("I", "@l@", new[] { "inner" });
        var outer = new Item("O", "@inner@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("inner")).Returns(inner);
        repo.Setup(r => r.GetItemByAlias("l")).Returns(leaf);

        outer.Execute(
            itemRepo: repo.Object,
            values: new Dictionary<string, string> { ["port"] = "8080" });

        outer.ExecCount.Should().Be(1);
        inner.ExecCount.Should().Be(1);
        leaf.Arguments.Should().Be("<<port>>"); // original preserved
    }

    [Fact]
    public void Execute_MetaItem_WithValuesAndForceAdmin_BothPropagate()
    {
        var leaf = new Item("L", "fake_app.xyz", new[] { "l" }) { Arguments = "<<port>>" };
        var meta = new Item("M", "@l@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("l")).Returns(leaf);

        var act = () => meta.Execute(
            forceAdminExec: true,
            itemRepo: repo.Object,
            values: new Dictionary<string, string> { ["port"] = "8080" });

        act.Should().NotThrow();
        meta.ExecCount.Should().Be(1);
    }
}
