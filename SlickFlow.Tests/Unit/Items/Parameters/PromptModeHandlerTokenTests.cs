using FluentAssertions;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;
using Moq;

namespace SlickFlow.Tests.Unit.Items.Parameters;

public class PromptModeHandlerTokenTests
{
    private static (Mock<IItemRepository> repo, Mock<IPublicAPI> api, PromptModeHandler handler)
        BuildHandler(FakeTokenSources sources)
    {
        var repo = new Mock<IItemRepository>();
        var api = new Mock<IPublicAPI>();
        var handler = new PromptModeHandler(repo.Object, api.Object, sources);
        return (repo, api, handler);
    }

    [Fact]
    public void BuildResults_TokenOnlyItem_ReturnsEmpty()
    {
        var (repo, _, handler) = BuildHandler(new FakeTokenSources());
        repo.Setup(r => r.GetItemByAlias("tr")).Returns(new Item("1", "https://t/?q=<<$clip>>"));

        var state = new PromptModeState("tr", Array.Empty<(string, string)>(), "$clip", "");
        handler.BuildResults(state).Should().BeEmpty();
    }

    [Fact]
    public void BuildResults_PreviewTitle_ResolvesTokens()
    {
        var (repo, _, handler) = BuildHandler(new FakeTokenSources());
        repo.Setup(r => r.GetItemByAlias("log")).Returns(
            new Item("1", "C:\\logs\\<<$date>>-<<name>>.txt"));

        var state = new PromptModeState("log", Array.Empty<(string, string)>(), "name", "build");
        handler.BuildResults(state).Single().Title.Should().Be("C:\\logs\\2024-03-07-build.txt");
    }

    [Fact]
    public void Action_Advance_PrefillsNextPromptWithResolvedTokenDefault()
    {
        var (repo, api, handler) = BuildHandler(new FakeTokenSources { Clipboard = "bonjour" });
        repo.Setup(r => r.GetItemByAlias("tr")).Returns(
            new Item("1", "https://t/?sl=<<lang>>&text=<<text=$clip>>"));

        var state = new PromptModeState("tr", Array.Empty<(string, string)>(), "lang", "fr");
        handler.BuildResults(state).Single().Action(new ActionContext());

        api.Verify(a => a.ChangeQuery("tr | lang=fr | text: bonjour", true), Times.Once);
    }

    [Fact]
    public void FormatEntryQuery_PrefillsFirstPromptWithResolvedTokenDefault()
    {
        var sources = new FakeTokenSources { Clipboard = "cats" };

        PromptModeHandler.FormatEntryQuery("g", new Placeholder("query", "$clip", null), sources)
            .Should().Be("g | query: cats");
    }

    [Fact]
    public void FormatEntryQuery_PlainDefault_IsUsedAsIs()
    {
        PromptModeHandler.FormatEntryQuery("s", new Placeholder("port", "8080", null), new FakeTokenSources())
            .Should().Be("s | port: 8080");
    }

    [Fact]
    public void FormatEntryQuery_MultiLineClipboard_IsFlattenedToOneLine()
    {
        var sources = new FakeTokenSources { Clipboard = "line1\r\nline2\nline3" };

        PromptModeHandler.FormatEntryQuery("g", new Placeholder("q", "$clip", null), sources)
            .Should().Be("g | q: line1 line2 line3");
    }
}
