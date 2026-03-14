using FluentAssertions;
using Moq;
using Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.items;

namespace SlickFlow.Tests.Unit.Commands;

public class AddCommandHandlerTests
{
    private readonly Mock<IItemRepository> _mockRepo;
    private readonly AddCommandHandler _handler;

    public AddCommandHandlerTests()
    {
        _mockRepo = new Mock<IItemRepository>();
        _mockRepo.Setup(r => r.GetAllItems()).Returns(new List<Item>());
        var validator = new ItemValidator(_mockRepo.Object, "icon.ico");
        _handler = new AddCommandHandler(_mockRepo.Object, validator, "icon.ico");
    }

    [Fact]
    public void Handle_TooFewArgs_ReturnsUsage()
    {
        var results = _handler.Handle(new[] { "np" });
        results.Should().HaveCount(1);
        results[0].Title.Should().Contain("Usage");
    }

    [Fact]
    public void Handle_ValidArgs_ReturnsAddResult()
    {
        var results = _handler.Handle(new[] { "np", "notepad.exe" });
        results.Should().HaveCount(1);
        results[0].Title.Should().Contain("Add item");
    }

    [Fact]
    public void Handle_MultipleAliases_SplitsByPipe()
    {
        var results = _handler.Handle(new[] { "np|note", "notepad.exe" });
        results.Should().HaveCount(1);
        results[0].Title.Should().Contain("np").And.Contain("note");
    }

    [Fact]
    public void Handle_DuplicateAlias_ReturnsValidationError()
    {
        _mockRepo.Setup(r => r.GetAllItems()).Returns(new List<Item>
        {
            new Item("1", "notepad.exe", new[] { "np" })
        });
        var validator = new ItemValidator(_mockRepo.Object, "icon.ico");
        var handler = new AddCommandHandler(_mockRepo.Object, validator, "icon.ico");
        var results = handler.Handle(new[] { "np", "other.exe" });
        results.Should().HaveCount(1);
        results[0].Title.Should().Contain("already exists");
    }

    [Fact]
    public void Handle_WithRunAs_ParsesLastArgAsInt()
    {
        var results = _handler.Handle(new[] { "np", "notepad.exe", "1" });
        results.Should().HaveCount(1);
        results[0].Title.Should().Contain("Add item");
    }
}
