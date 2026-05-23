using FluentAssertions;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;
using Moq;

namespace SlickFlow.Tests.Unit.Items.Parameters;

public class PromptModeHandlerTests
{
    private static (Mock<IItemRepository> repo, Mock<IPublicAPI> api, PromptModeHandler handler)
        BuildHandler()
    {
        var repo = new Mock<IItemRepository>();
        var api = new Mock<IPublicAPI>();
        var handler = new PromptModeHandler(repo.Object, api.Object);
        return (repo, api, handler);
    }

    [Fact]
    public void BuildResults_UnknownAlias_ReturnsEmpty()
    {
        var (repo, _, handler) = BuildHandler();
        repo.Setup(r => r.GetItemByAlias("missing")).Returns((Item?)null);

        var state = new PromptModeState("missing", Array.Empty<(string, string)>(), "port", "");
        handler.BuildResults(state).Should().BeEmpty();
    }

    [Fact]
    public void BuildResults_AliasMatchesNonParameterizedItem_ReturnsEmpty()
    {
        var (repo, _, handler) = BuildHandler();
        repo.Setup(r => r.GetItemByAlias("plain")).Returns(new Item("1", "notepad.exe"));

        var state = new PromptModeState("plain", Array.Empty<(string, string)>(), "port", "");
        handler.BuildResults(state).Should().BeEmpty();
    }

    [Fact]
    public void BuildResults_CurrentNameNotInSchema_ReturnsEmpty()
    {
        // The query references a placeholder name that doesn't exist on the item.
        // Most likely the user edited the bar to a stale name; reject gracefully.
        var (repo, _, handler) = BuildHandler();
        repo.Setup(r => r.GetItemByAlias("server")).Returns(new Item("1", "http://x:<<port>>"));

        var state = new PromptModeState("server", Array.Empty<(string, string)>(), "nope", "8080");
        handler.BuildResults(state).Should().BeEmpty();
    }

    [Fact]
    public void BuildResults_FirstPromptShowsPreviewAndNextHint()
    {
        var (repo, _, handler) = BuildHandler();
        repo.Setup(r => r.GetItemByAlias("server")).Returns(
            new Item("1", "http://<<host>>:<<port=8080|web port>>"));

        var state = new PromptModeState("server", Array.Empty<(string, string)>(), "host", "0.0.0.0");
        var results = handler.BuildResults(state);

        results.Should().ContainSingle();
        // Preview uses the typed value for the current placeholder and falls back
        // to each remaining placeholder's default so the user sees the real launch target.
        results[0].Title.Should().Be("http://0.0.0.0:8080");
        results[0].SubTitle.Should().Contain("host");
        results[0].SubTitle.Should().Contain("port"); // hints at next placeholder
    }

    [Fact]
    public void BuildResults_ResultHasMaxScore_SoItDominatesOtherPluginResults()
    {
        // Flow Launcher merges results across all plugins. Without a high Score,
        // unrelated plugins (web search, etc.) outrank the active prompt and the
        // user ends up triggering them instead of advancing the prompt.
        var (repo, _, handler) = BuildHandler();
        repo.Setup(r => r.GetItemByAlias("server")).Returns(
            new Item("1", "http://localhost:<<port>>"));

        var state = new PromptModeState("server", Array.Empty<(string, string)>(), "port", "8080");
        var results = handler.BuildResults(state);

        results[0].Score.Should().Be(int.MaxValue);
    }

    [Fact]
    public void BuildResults_LastPromptShowsLaunchPrompt()
    {
        var (repo, _, handler) = BuildHandler();
        repo.Setup(r => r.GetItemByAlias("server")).Returns(
            new Item("1", "http://localhost:<<port>>"));

        var state = new PromptModeState("server", Array.Empty<(string, string)>(), "port", "8080");
        var results = handler.BuildResults(state);

        results.Should().ContainSingle();
        results[0].Title.Should().Be("http://localhost:8080");
        results[0].SubTitle.Should().Contain("Enter to launch");
    }

    [Fact]
    public void BuildResults_ShowsHintInSubtitle()
    {
        var (repo, _, handler) = BuildHandler();
        repo.Setup(r => r.GetItemByAlias("server")).Returns(
            new Item("1", "<<port|web server port>>"));

        var state = new PromptModeState("server", Array.Empty<(string, string)>(), "port", "");
        var results = handler.BuildResults(state);

        results[0].SubTitle.Should().Contain("web server port");
    }

    [Fact]
    public void Action_NotLastPlaceholder_AdvancesViaChangeQuery_KeepsFlowOpen()
    {
        var (repo, api, handler) = BuildHandler();
        repo.Setup(r => r.GetItemByAlias("server")).Returns(
            new Item("1", "http://<<host>>:<<port=8080>>"));

        var state = new PromptModeState("server", Array.Empty<(string, string)>(), "host", "0.0.0.0");
        var result = handler.BuildResults(state).Single();

        var closeFlow = result.Action(new ActionContext());

        closeFlow.Should().BeFalse();
        api.Verify(a => a.ChangeQuery(
            "server | host=0.0.0.0 | port: 8080", // default 8080 pre-filled
            true), Times.Once);
    }

    [Fact]
    public void Action_LastPlaceholder_ExecutesItemAndClosesFlow()
    {
        var (repo, api, handler) = BuildHandler();
        var item = new Item("1", "fake_app_<<port>>.xyz");
        repo.Setup(r => r.GetItemByAlias("server")).Returns(item);

        var state = new PromptModeState("server", Array.Empty<(string, string)>(), "port", "8080");
        var result = handler.BuildResults(state).Single();

        var closeFlow = result.Action(new ActionContext());

        closeFlow.Should().BeTrue();
        api.Verify(a => a.ChangeQuery(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        repo.Verify(r => r.UpdateItem(item), Times.Once);
        item.FileName.Should().Be("fake_app_<<port>>.xyz"); // never mutated
    }

    [Fact]
    public void Action_MetaItemWithParameterizedLeaf_ExecutesChainOnLastPrompt()
    {
        var (repo, _, handler) = BuildHandler();
        var leaf = new Item("L", "fake_app_<<port>>.xyz", new[] { "l" });
        var meta = new Item("M", "@l@");
        repo.Setup(r => r.GetItemByAlias("meta")).Returns(meta);
        repo.Setup(r => r.GetItemByAlias("l")).Returns(leaf);

        var state = new PromptModeState("meta", Array.Empty<(string, string)>(), "port", "8080");
        var result = handler.BuildResults(state).Single();

        var closeFlow = result.Action(new ActionContext());

        closeFlow.Should().BeTrue();
        meta.ExecCount.Should().Be(1);
        leaf.FileName.Should().Be("fake_app_<<port>>.xyz"); // original preserved
    }
}
