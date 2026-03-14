using FluentAssertions;
using Moq;
using Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.items;

namespace SlickFlow.Tests.Unit.Commands;

public class AliasCommandHandlerTests
{
    private readonly Mock<IItemRepository> _mockRepo;
    private readonly AliasCommandHandler _handler;

    public AliasCommandHandlerTests()
    {
        _mockRepo = new Mock<IItemRepository>();
        _mockRepo.Setup(r => r.GetAllItems()).Returns(new List<Item>());
        var validator = new ItemValidator(_mockRepo.Object, "icon.ico");
        _handler = new AliasCommandHandler(_mockRepo.Object, validator, "icon.ico");
    }

    [Fact]
    public void Handle_TooFewArgs_ReturnsUsage()
    {
        var results = _handler.Handle(new[] { "np" });
        results[0].Title.Should().Contain("Usage");
    }

    [Fact]
    public void Handle_ItemNotFound_ReturnsNotFound()
    {
        _mockRepo.Setup(r => r.GetItemById("xyz")).Returns((Item?)null);
        _mockRepo.Setup(r => r.GetItemByAlias("xyz")).Returns((Item?)null);
        var results = _handler.Handle(new[] { "xyz", "newalias" });
        results[0].Title.Should().Contain("No item found");
    }

    [Fact]
    public void Handle_ValidAlias_ReturnsAddResult()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        _mockRepo.Setup(r => r.GetItemByAlias("np")).Returns(item);
        var results = _handler.Handle(new[] { "np", "note" });
        results[0].Title.Should().Contain("alias");
    }
}
