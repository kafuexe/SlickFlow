using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Moq;
using SlickFlow.Tests.Unit.Items.Parameters;

namespace SlickFlow.Tests.Unit.Items;

public class ItemTokenTests
{
    [Fact]
    public void IsParameterized_False_WhenOnlyTokens()
    {
        var item = new Item("1", "https://t/?text=<<$clip>>") { Arguments = "<<$date>>" };
        item.IsParameterized.Should().BeFalse();
    }

    [Fact]
    public void IsParameterized_True_WhenTokensMixedWithRealPlaceholder()
    {
        var item = new Item("1", "https://t/?text=<<$clip>>") { Arguments = "<<lang>>" };
        item.IsParameterized.Should().BeTrue();
    }

    [Fact]
    public void IsParameterized_True_WhenPlaceholderHasTokenDefault()
    {
        new Item("1", "g?q=<<query=$clip>>").IsParameterized.Should().BeTrue();
    }

    [Fact]
    public void Substitute_ResolvesTokensFromSources()
    {
        var sources = new FakeTokenSources { Clipboard = "hola" };
        var item = new Item("1", "https://translate.google.com/?text=<<$clip>>")
        {
            Arguments = "C:\\logs\\<<$date>>.txt <<who>>"
        };

        var clone = item.Substitute(new Dictionary<string, string> { ["who"] = "me" }, sources);

        clone.FileName.Should().Be("https://translate.google.com/?text=hola");
        clone.Arguments.Should().Be("C:\\logs\\2024-03-07.txt me");
        item.FileName.Should().Be("https://translate.google.com/?text=<<$clip>>");
    }

    [Fact]
    public void Execute_TokenOnlyItem_ResolvesWithoutValuesAndPreservesStoredFields()
    {
        var sources = new FakeTokenSources { Clipboard = "x" };
        var item = new Item("1", "fake_app_<<$clip>>.xyz") { Arguments = "<<$date>>" };

        var act = () => item.Execute(tokens: sources);

        act.Should().NotThrow();
        sources.ClipboardReads.Should().Be(1);
        item.FileName.Should().Be("fake_app_<<$clip>>.xyz");
        item.Arguments.Should().Be("<<$date>>");
    }

    [Fact]
    public void Execute_MetaItem_ResolvesTokensInEveryLeaf_ReadingClipboardOnce()
    {
        var a = new Item("A", "fake_a_<<$clip>>.xyz", new[] { "a" });
        var b = new Item("B", "fake_b.xyz", new[] { "b" }) { Arguments = "<<$clip>> <<$clip>>" };
        var meta = new Item("M", "@a@@b@");

        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias("a")).Returns(a);
        repo.Setup(r => r.GetItemByAlias("b")).Returns(b);

        var sources = new FakeTokenSources { Clipboard = "c" };
        meta.Execute(itemRepo: repo.Object, tokens: sources);

        // One execution = one consistent clipboard snapshot across all leaves.
        sources.ClipboardReads.Should().Be(1);
        meta.ExecCount.Should().Be(1);
    }
}
