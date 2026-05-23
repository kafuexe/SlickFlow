using FluentAssertions;
using Moq;
using Flow.Launcher.Plugin.SlickFlow.Commands;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.items;
using Flow.Launcher.Plugin.SlickFlow.Utils;
using Flow.Launcher.Plugin.SlickFlow.Utils.Icons;

namespace SlickFlow.Tests.Unit.Commands;

public class CommandProcessorTests
{
    private readonly CommandProcessor _processor;

    public CommandProcessorTests()
    {
        var mockRepo = new Mock<IItemRepository>();
        mockRepo.Setup(r => r.GetAllItems()).Returns(new List<Item>());
        var validator = new ItemValidator(mockRepo.Object, "icon.ico");
        var tempDir = Path.Combine(Path.GetTempPath(), "SlickFlowCPTests_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var iconHelper = new IconHelper(tempDir);
        _processor = new CommandProcessor(mockRepo.Object, validator, iconHelper, "icon.ico");
    }

    [Theory]
    [InlineData("add")]
    [InlineData("alias")]
    [InlineData("remove")]
    [InlineData("delete")]
    [InlineData("update")]
    [InlineData("seticon")]
    public void Process_KnownCommand_ReturnsResults(string command)
    {
        var results = _processor.Process(command, Array.Empty<string>());
        results.Should().NotBeEmpty();
    }

    [Fact]
    public void Process_UnknownCommand_ReturnsEmpty()
    {
        var results = _processor.Process("unknown", Array.Empty<string>());
        results.Should().BeEmpty();
    }

    [Fact]
    public void Process_CaseInsensitive()
    {
        var results = _processor.Process("ADD", Array.Empty<string>());
        results.Should().NotBeEmpty();
    }
}
