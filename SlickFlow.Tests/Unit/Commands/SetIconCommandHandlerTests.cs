using FluentAssertions;
using Moq;
using Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.Utils;

namespace SlickFlow.Tests.Unit.Commands;

public class SetIconCommandHandlerTests
{
    private readonly Mock<IItemRepository> _mockRepo;
    private readonly SetIconCommandHandler _handler;

    public SetIconCommandHandlerTests()
    {
        _mockRepo = new Mock<IItemRepository>();
        var tempDir = Path.Combine(Path.GetTempPath(), "SlickFlowSetIconTests_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var iconHelper = new IconHelper(tempDir);
        _handler = new SetIconCommandHandler(_mockRepo.Object, iconHelper, "icon.ico");
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
        var results = _handler.Handle(new[] { "xyz", "icon.png" });
        results[0].Title.Should().Contain("No item found");
    }

    [Fact]
    public void Handle_ValidArgs_ReturnsSetIconResult()
    {
        var item = new Item("1", "notepad.exe", new[] { "np" });
        _mockRepo.Setup(r => r.GetItemByAlias("np")).Returns(item);
        var results = _handler.Handle(new[] { "np", "icon.png" });
        results[0].Title.Should().Contain("Set custom icon");
    }
}
