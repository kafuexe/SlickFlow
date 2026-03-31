using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.ViewModels.Settings;

namespace SlickFlow.Tests.Unit.ViewModels;

public class SettingsViewModelTests : IDisposable
{
    private readonly string _tempFile;
    private readonly ItemRepository _repo;
    private readonly SettingsViewModel _vm;

    public SettingsViewModelTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"slickflow_vm_test_{Guid.NewGuid()}.json");
        _repo = new ItemRepository(_tempFile);
        _vm = new SettingsViewModel(_repo);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public void Constructor_LoadsItemsFromRepo()
    {
        _repo.AddItem(new Item { Aliases = new List<string> { "alpha" } });
        _repo.AddItem(new Item { Aliases = new List<string> { "beta" } });

        var vm = new SettingsViewModel(_repo);

        vm.Items.Should().HaveCount(2);
    }

    [Fact]
    public void SearchText_FiltersItems()
    {
        _repo.AddItem(new Item { Aliases = new List<string> { "notepad" } });
        _repo.AddItem(new Item { Aliases = new List<string> { "calculator" } });
        _repo.AddItem(new Item { Aliases = new List<string> { "note-taker" } });

        var vm = new SettingsViewModel(_repo);
        vm.SearchText = "note";

        vm.Items.Should().HaveCount(2);
        vm.Items.Should().AllSatisfy(i =>
            i.AliasesString.Should().ContainEquivalentOf("note"));
    }

    [Fact]
    public void SearchText_CaseInsensitive()
    {
        _repo.AddItem(new Item { Aliases = new List<string> { "Notepad" } });
        _repo.AddItem(new Item { Aliases = new List<string> { "calculator" } });

        var vm = new SettingsViewModel(_repo);
        vm.SearchText = "NOTEPAD";

        vm.Items.Should().HaveCount(1);
    }

    [Fact]
    public void SearchText_EmptyString_ShowsAll()
    {
        _repo.AddItem(new Item { Aliases = new List<string> { "alpha" } });
        _repo.AddItem(new Item { Aliases = new List<string> { "beta" } });

        var vm = new SettingsViewModel(_repo);
        vm.SearchText = "alpha";
        vm.Items.Should().HaveCount(1);

        vm.SearchText = "";
        vm.Items.Should().HaveCount(2);
    }

    [Fact]
    public void SearchText_RaisesPropertyChanged()
    {
        var raised = new List<string>();
        _vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        _vm.SearchText = "test";

        raised.Should().Contain("SearchText");
    }

    [Fact]
    public void DbFilePath_RaisesPropertyChanged()
    {
        var raised = new List<string>();
        _vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        _vm.DbFilePath = "new/path.json";

        raised.Should().Contain("DbFilePath");
    }

    [Fact]
    public void IconDirPath_RaisesPropertyChanged()
    {
        var raised = new List<string>();
        _vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        _vm.IconDirPath = "new/icon/dir";

        raised.Should().Contain("IconDirPath");
    }

    [Fact]
    public void DbFilePath_SameValue_DoesNotRaisePropertyChanged()
    {
        _vm.DbFilePath = "initial";
        var raised = false;
        _vm.PropertyChanged += (_, _) => raised = true;

        _vm.DbFilePath = "initial";

        raised.Should().BeFalse();
    }

    [Fact]
    public void AddItemCommand_AddsNewItem()
    {
        var initialCount = _vm.Items.Count;

        _vm.AddItemCommand.Execute(null);

        _vm.Items.Should().HaveCount(initialCount + 1);
    }

    [Fact]
    public void AddItemCommand_GeneratesUniqueAliases()
    {
        _vm.AddItemCommand.Execute(null);
        _vm.AddItemCommand.Execute(null);

        var aliases = _vm.Items.Select(i => i.AliasesString).ToList();
        aliases.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ReloadItemsCommand_RefreshesItems()
    {
        _repo.AddItem(new Item { Aliases = new List<string> { "external" } });

        _vm.ReloadItemsCommand.Execute(null);

        _vm.Items.Should().Contain(i => i.AliasesString.Contains("external"));
    }

    [Fact]
    public void Commands_AreNotNull()
    {
        _vm.SaveCommand.Should().NotBeNull();
        _vm.BrowseDbFolderCommand.Should().NotBeNull();
        _vm.BrowseIconFolderCommand.Should().NotBeNull();
        _vm.ReloadItemsCommand.Should().NotBeNull();
        _vm.AddItemCommand.Should().NotBeNull();
    }
}
