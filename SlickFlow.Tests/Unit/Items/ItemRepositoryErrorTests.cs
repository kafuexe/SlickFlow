using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace SlickFlow.Tests.Unit.Items;

public class ItemRepositoryErrorTests : IDisposable
{
    private readonly string _tempFile;
    private readonly ItemRepository _repo;

    public ItemRepositoryErrorTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"slickflow_test_{Guid.NewGuid()}.json");
        _repo = new ItemRepository(_tempFile);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public void UpdateItem_NonExistentId_ThrowsInvalidOperation()
    {
        var item = new Item("nonexistent", "test.exe");

        var act = () => _repo.UpdateItem(item);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public void AddAlias_NonExistentId_ThrowsInvalidOperation()
    {
        var act = () => _repo.AddAlias("nonexistent", "alias");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public void RemoveAlias_NonExistentId_ThrowsInvalidOperation()
    {
        var act = () => _repo.RemoveAlias("nonexistent", "alias");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public void DeleteItem_NonExistentId_DoesNotThrow()
    {
        var act = () => _repo.DeleteItem("nonexistent");

        act.Should().NotThrow();
    }

    [Fact]
    public void GetItemById_NonExistentId_ReturnsNull()
    {
        _repo.GetItemById("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetItemByAlias_NonExistentAlias_ReturnsNull()
    {
        _repo.GetItemByAlias("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Load_CorruptedJson_DoesNotThrow()
    {
        File.WriteAllText(_tempFile, "not valid json {{{");
        var act = () => new ItemRepository(_tempFile);

        act.Should().NotThrow();
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsEmptyList()
    {
        File.WriteAllText(_tempFile, "not valid json {{{");
        var repo = new ItemRepository(_tempFile);

        repo.GetAllItems().Should().BeEmpty();
    }

    [Fact]
    public void AddAlias_DuplicateAlias_DoesNotAddAgain()
    {
        var item = new Item { Aliases = new List<string> { "existing" } };
        var id = _repo.AddItem(item);

        _repo.AddAlias(id, "existing");

        var retrieved = _repo.GetItemById(id)!;
        retrieved.Aliases.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveAlias_NonExistentAlias_DoesNotThrow()
    {
        var item = new Item { Aliases = new List<string> { "keep" } };
        var id = _repo.AddItem(item);

        var act = () => _repo.RemoveAlias(id, "nonexistent");

        act.Should().NotThrow();
        _repo.GetItemById(id)!.Aliases.Should().Contain("keep");
    }
}
