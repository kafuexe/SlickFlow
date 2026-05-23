using FluentAssertions;
using Moq;
using Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace SlickFlow.Tests.Unit.Commands;

public class RemoveCommandHandlerTests
{
    private readonly Mock<IItemRepository> _mockRepo;
    private readonly RemoveCommandHandler _handler;

    public RemoveCommandHandlerTests()
    {
        _mockRepo = new Mock<IItemRepository>();
        _handler = new RemoveCommandHandler(_mockRepo.Object, "icon.ico");
    }

    [Fact]
    public void Handle_NoArgs_ReturnsUsage()
    {
        var results = _handler.Handle(Array.Empty<string>());
        results.Should().HaveCount(1);
        results[0].Title.Should().Contain("Usage");
    }

    [Fact]
    public void Handle_AliasNotFound_ReturnsNotFound()
    {
        _mockRepo.Setup(r => r.GetItemByAlias("np")).Returns((Item?)null);
        var results = _handler.Handle(new[] { "np" });
        results[0].Title.Should().Contain("No item found");
    }

    [Fact]
    public void Handle_OnlyOneAlias_SuggestsDelete()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        _mockRepo.Setup(r => r.GetItemByAlias("np")).Returns(item);
        var results = _handler.Handle(new[] { "np" });
        results[0].Title.Should().Contain("only has one alias");
    }

    [Fact]
    public void Handle_MultipleAliases_ReturnsRemoveResult()
    {
        var item = new Item("1", "notepad.exe", new[] { "np", "note" });
        _mockRepo.Setup(r => r.GetItemByAlias("np")).Returns(item);
        var results = _handler.Handle(new[] { "np" });
        results[0].Title.Should().Contain("Remove alias");
    }
}
