using FluentAssertions;
using Moq;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.items;

namespace SlickFlow.Tests.Unit.Items;

public class ItemValidatorTests
{
    private readonly Mock<IItemRepository> _mockRepo;
    private readonly ItemValidator _validator;

    public ItemValidatorTests()
    {
        _mockRepo = new Mock<IItemRepository>();
        _validator = new ItemValidator(_mockRepo.Object, "icon.ico");
    }

    [Fact]
    public void ValidateAliases_EmptyList_ReturnsError()
    {
        var results = _validator.ValidateAliases(new List<string>());
        results.Should().HaveCount(1);
        results[0].Title.Should().Contain("No valid aliases");
    }

    [Fact]
    public void ValidateAliases_DuplicateAlias_ReturnsError()
    {
        var existingItems = new List<Item>
        {
            new Item("1", "notepad.exe", new[] { "np" })
        };
        _mockRepo.Setup(r => r.GetAllItems()).Returns(existingItems);
        var results = _validator.ValidateAliases(new List<string> { "np" });
        results.Should().HaveCount(1);
        results[0].Title.Should().Contain("Alias already exists");
    }

    [Fact]
    public void ValidateAliases_DuplicateAlias_CaseInsensitive()
    {
        var existingItems = new List<Item>
        {
            new Item("1", "notepad.exe", new[] { "NP" })
        };
        _mockRepo.Setup(r => r.GetAllItems()).Returns(existingItems);
        var results = _validator.ValidateAliases(new List<string> { "np" });
        results.Should().HaveCount(1);
    }

    [Fact]
    public void ValidateAliases_UniqueAlias_ReturnsEmpty()
    {
        var existingItems = new List<Item>
        {
            new Item("1", "notepad.exe", new[] { "np" })
        };
        _mockRepo.Setup(r => r.GetAllItems()).Returns(existingItems);
        var results = _validator.ValidateAliases(new List<string> { "vim" });
        results.Should().BeEmpty();
    }

    [Theory]
    [InlineData("args", true)]
    [InlineData("arguments", true)]
    [InlineData("runas", true)]
    [InlineData("startmode", true)]
    [InlineData("subtitle", true)]
    [InlineData("workingdir", true)]
    [InlineData("workdir", true)]
    [InlineData("invalid", false)]
    [InlineData("name", false)]
    [InlineData("filename", false)]
    public void IsValidProperty_ReturnsCorrectResult(string prop, bool expected)
    {
        _validator.IsValidProperty(prop).Should().Be(expected);
    }
}
