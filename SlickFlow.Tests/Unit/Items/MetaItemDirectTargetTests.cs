using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;
using Moq;

namespace SlickFlow.Tests.Unit.Items;

/// <summary>
/// Meta chains may mix alias references with direct targets (URLs, paths, programs).
/// </summary>
public class MetaItemDirectTargetTests
{
    private static Mock<IItemRepository> RepoWith(params Item[] items)
    {
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetItemByAlias(It.IsAny<string>()))
            .Returns((string alias) => items.FirstOrDefault(i =>
                i.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase)));
        return repo;
    }

    private static List<Item> Leaves(Item meta, IItemRepository repo)
    {
        var leaves = new List<Item>();
        meta.WalkMetaChain(repo, new HashSet<Item>(), leaves.Add);
        return leaves;
    }

    [Theory]
    [InlineData("@https://news.google.com@@notepad.exe@", true)]
    [InlineData("@C:\\tools\\app.exe@", true)]
    [InlineData("@mail@@https://x.com@", true)]
    public void IsMetaItem_AcceptsDirectTargets(string fileName, bool expected)
    {
        new Item("1", fileName).IsMetaItem.Should().Be(expected);
    }

    [Fact]
    public void Walk_MixesAliasesAndDirectTargets_InOrder()
    {
        var mail = new Item("2", "https://outlook.live.com/", new[] { "mail" });
        var meta = new Item("1", "@https://news.google.com@@mail@@notepad.exe@");

        var leaves = Leaves(meta, RepoWith(mail).Object);

        leaves.Select(l => l.FileName).Should().Equal(
            "https://news.google.com", "https://outlook.live.com/", "notepad.exe");
        leaves[1].Should().BeSameAs(mail);
    }

    [Fact]
    public void Walk_AliasWinsOverDirectTarget_WhenNamesCollide()
    {
        // An item aliased "notepad.exe" takes precedence over launching the file directly.
        var aliased = new Item("2", "C:\\custom\\notepad.exe", new[] { "notepad.exe" });
        var meta = new Item("1", "@notepad.exe@");

        Leaves(meta, RepoWith(aliased).Object).Should().ContainSingle()
            .Which.Should().BeSameAs(aliased);
    }

    [Theory]
    [InlineData("https://news.google.com")]
    [InlineData("mailto:me")]
    [InlineData("C:\\tools\\app.exe")]
    [InlineData("tools/app.exe")]
    [InlineData("notepad.exe")]
    [InlineData("C:\\scripts\\backup.ps1")]
    [InlineData("<<$env:USERPROFILE>>\\Downloads")]
    public void LooksLikeDirectTarget_RecognisesTargets(string part)
    {
        Item.LooksLikeDirectTarget(part).Should().BeTrue();
    }

    [Theory]
    [InlineData("mail")]
    [InlineData("my-alias")]
    [InlineData("typo")]
    public void LooksLikeDirectTarget_RejectsPlainWords(string part)
    {
        Item.LooksLikeDirectTarget(part).Should().BeFalse();
    }

    [Fact]
    public void Walk_UnknownPlainWord_StillThrowsUnknownAlias()
    {
        // A typo'd alias must stay an error, not be launched as a file.
        var meta = new Item("1", "@https://x.com@@mial@");

        var act = () => Leaves(meta, RepoWith().Object);

        act.Should().Throw<InvalidOperationException>().WithMessage("*\"mial\"*");
    }

    [Fact]
    public void Walk_DirectTargetLeaf_HasNoAliasesAndIsNotMeta()
    {
        var leaf = Leaves(new Item("1", "@notepad.exe@"), RepoWith().Object).Single();

        leaf.Aliases.Should().BeEmpty();
        leaf.IsMetaItem.Should().BeFalse();
        leaf.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void Execute_ChainWithOnlyDirectTargets_CountsMetaExecution()
    {
        var meta = new Item("1", "@fake_nonexistent_app.xyz@@other_fake.xyz@");

        meta.Execute(itemRepo: RepoWith().Object);

        meta.ExecCount.Should().Be(1);
    }

    [Fact]
    public void Schema_CollectsPlaceholdersFromDirectTargets()
    {
        var meta = new Item("1", "@https://google.com/search?q=<<query>>@@notepad.exe@");

        PlaceholderSchema.From(meta, RepoWith().Object)
            .Select(p => p.Name).Should().Equal("query");
    }
}
