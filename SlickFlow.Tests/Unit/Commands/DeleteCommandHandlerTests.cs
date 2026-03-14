using FluentAssertions;
using Moq;
using Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace SlickFlow.Tests.Unit.Commands;

public class DeleteCommandHandlerTests
{
    private readonly Mock<IItemRepository> _mockRepo;
    private readonly DeleteCommandHandler _handler;

    public DeleteCommandHandlerTests()
    {
        _mockRepo = new Mock<IItemRepository>();
        _handler = new DeleteCommandHandler(_mockRepo.Object, "icon.ico");
    }

    [Fact]
    public void Handle_NoArgs_ReturnsUsage()
    {
        var results = _handler.Handle(Array.Empty<string>());
        results[0].Title.Should().Contain("Usage");
    }

    [Fact]
    public void Handle_NotFound_ReturnsNotFound()
    {
        _mockRepo.Setup(r => r.GetItemById("xyz")).Returns((Item?)null);
        _mockRepo.Setup(r => r.GetItemByAlias("xyz")).Returns((Item?)null);
        var results = _handler.Handle(new[] { "xyz" });
        results[0].Title.Should().Contain("No item found");
    }

    [Fact]
    public void Handle_Found_ReturnsConfirmDelete()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        _mockRepo.Setup(r => r.GetItemByAlias("np")).Returns(item);
        var results = _handler.Handle(new[] { "np" });
        results[0].Title.Should().Contain("Confirm delete");
    }
}
