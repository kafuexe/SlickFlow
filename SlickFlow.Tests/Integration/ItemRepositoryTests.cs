using System.IO;
using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace SlickFlow.Tests.Integration;

public class ItemRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public ItemRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SlickFlowTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "test.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void AddItem_AssignsIdAndPersists()
    {
        var repo = new ItemRepository(_dbPath);
        var item = new Item { FileName = "notepad.exe", Aliases = new List<string> { "np" } };
        var id = repo.AddItem(item);
        id.Should().NotBeNullOrEmpty();
        File.Exists(_dbPath).Should().BeTrue();
    }

    [Fact]
    public void GetItemById_ReturnsAddedItem()
    {
        var repo = new ItemRepository(_dbPath);
        var item = new Item { FileName = "notepad.exe", Aliases = new List<string> { "np" } };
        var id = repo.AddItem(item);
        var retrieved = repo.GetItemById(id);
        retrieved.Should().NotBeNull();
        retrieved!.FileName.Should().Be("notepad.exe");
    }

    [Fact]
    public void GetAllItems_ReturnsAllItems()
    {
        var repo = new ItemRepository(_dbPath);
        repo.AddItem(new Item { FileName = "a.exe", Aliases = new List<string> { "a" } });
        repo.AddItem(new Item { FileName = "b.exe", Aliases = new List<string> { "b" } });
        repo.GetAllItems().Should().HaveCount(2);
    }

    [Fact]
    public void UpdateItem_PersistsChanges()
    {
        var repo = new ItemRepository(_dbPath);
        var item = new Item { FileName = "notepad.exe", Aliases = new List<string> { "np" } };
        var id = repo.AddItem(item);
        var toUpdate = repo.GetItemById(id)!;
        toUpdate.Arguments = "/A";
        repo.UpdateItem(toUpdate);
        var reloaded = new ItemRepository(_dbPath);
        reloaded.GetItemById(id)!.Arguments.Should().Be("/A");
    }

    [Fact]
    public void DeleteItem_RemovesFromPersistence()
    {
        var repo = new ItemRepository(_dbPath);
        var item = new Item { FileName = "notepad.exe", Aliases = new List<string> { "np" } };
        var id = repo.AddItem(item);
        repo.DeleteItem(id);
        repo.GetItemById(id).Should().BeNull();
        var reloaded = new ItemRepository(_dbPath);
        reloaded.GetItemById(id).Should().BeNull();
    }

    [Fact]
    public void AddAlias_PersistsNewAlias()
    {
        var repo = new ItemRepository(_dbPath);
        var item = new Item { FileName = "notepad.exe", Aliases = new List<string> { "np" } };
        var id = repo.AddItem(item);
        repo.AddAlias(id, "note");
        repo.GetItemById(id)!.Aliases.Should().Contain("note");
    }

    [Fact]
    public void RemoveAlias_PersistsRemoval()
    {
        var repo = new ItemRepository(_dbPath);
        var item = new Item { FileName = "notepad.exe", Aliases = new List<string> { "np", "note" } };
        var id = repo.AddItem(item);
        repo.RemoveAlias(id, "np");
        repo.GetItemById(id)!.Aliases.Should().NotContain("np");
    }

    [Fact]
    public void GetItemByAlias_FindsItem()
    {
        var repo = new ItemRepository(_dbPath);
        repo.AddItem(new Item { FileName = "notepad.exe", Aliases = new List<string> { "np" } });
        var found = repo.GetItemByAlias("np");
        found.Should().NotBeNull();
        found!.FileName.Should().Be("notepad.exe");
    }

    [Fact]
    public void GetItemByAlias_CaseInsensitive()
    {
        var repo = new ItemRepository(_dbPath);
        repo.AddItem(new Item { FileName = "notepad.exe", Aliases = new List<string> { "NP" } });
        repo.GetItemByAlias("np").Should().NotBeNull();
    }

    [Fact]
    public void Constructor_MissingFile_StartsEmpty()
    {
        var repo = new ItemRepository(Path.Combine(_tempDir, "nonexistent.json"));
        repo.GetAllItems().Should().BeEmpty();
    }

    [Fact]
    public void DataSurvivesReload()
    {
        var repo = new ItemRepository(_dbPath);
        repo.AddItem(new Item { FileName = "notepad.exe", Aliases = new List<string> { "np" } });
        var reloaded = new ItemRepository(_dbPath);
        reloaded.GetAllItems().Should().HaveCount(1);
    }
}
