using FluentAssertions;
using Moq;
using Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.items;

namespace SlickFlow.Tests.Unit.Commands;

public class UpdateCommandHandlerTests
{
    private readonly Mock<IItemRepository> _mockRepo;
    private readonly UpdateCommandHandler _handler;

    public UpdateCommandHandlerTests()
    {
        _mockRepo = new Mock<IItemRepository>();
        var validator = new ItemValidator(_mockRepo.Object, "icon.ico");
        _handler = new UpdateCommandHandler(_mockRepo.Object, validator, "icon.ico");
    }

    [Fact]
    public void Handle_TooFewArgs_ReturnsUsage()
    {
        var results = _handler.Handle(new[] { "np", "args" });
        results[0].Title.Should().Contain("Usage");
    }

    [Fact]
    public void Handle_ItemNotFound_ReturnsNotFound()
    {
        _mockRepo.Setup(r => r.GetItemById("xyz")).Returns((Item?)null);
        _mockRepo.Setup(r => r.GetItemByAlias("xyz")).Returns((Item?)null);
        var results = _handler.Handle(new[] { "xyz", "args", "value" });
        results[0].Title.Should().Contain("No item found");
    }

    [Fact]
    public void Handle_InvalidProperty_ReturnsError()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        _mockRepo.Setup(r => r.GetItemByAlias("np")).Returns(item);
        var results = _handler.Handle(new[] { "np", "badprop", "value" });
        results[0].Title.Should().Contain("Invalid properties");
    }

    [Fact]
    public void Handle_ValidUpdate_ReturnsUpdateResult()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        _mockRepo.Setup(r => r.GetItemByAlias("np")).Returns(item);
        var results = _handler.Handle(new[] { "np", "args", "/A" });
        results[0].Title.Should().Contain("Update item");
    }
}
