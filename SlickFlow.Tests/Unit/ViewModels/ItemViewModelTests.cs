using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.ViewModels.Item;

namespace SlickFlow.Tests.Unit.ViewModels;

public class ItemViewModelTests : IDisposable
{
    private readonly string _tempFile;
    private readonly ItemRepository _repo;

    public ItemViewModelTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"slickflow_ivm_test_{Guid.NewGuid()}.json");
        _repo = new ItemRepository(_tempFile);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    private ItemViewModel CreateVm(Item? item = null)
    {
        item ??= new Item("1", "notepad.exe", new[] { "np", "note" })
        {
            Arguments = "-f file.txt",
            SubTitle = "Text Editor",
            WorkingDir = @"C:\temp",
            RunAs = 0,
            StartMode = 1,
            ExecCount = 3
        };
        _repo.AddItem(item);
        return new ItemViewModel(item, _repo);
    }

    #region Display Properties

    [Fact]
    public void AliasesString_JoinsAliases()
    {
        var vm = CreateVm();
        vm.AliasesString.Should().Be("np, note");
    }

    [Fact]
    public void ArgsDisplay_ShowsArgsPrefix()
    {
        var vm = CreateVm();
        vm.ArgsDisplay.Should().Be("Args: -f file.txt");
    }

    [Fact]
    public void ArgsDisplay_EmptyArgs_ReturnsEmpty()
    {
        var item = new Item("1", "test.exe") { Arguments = "" };
        _repo.AddItem(item);
        var vm = new ItemViewModel(item, _repo);

        vm.ArgsDisplay.Should().BeEmpty();
    }

    [Fact]
    public void WorkingDirDisplay_ShowsPrefix()
    {
        var vm = CreateVm();
        vm.WorkingDirDisplay.Should().Be(@"working dir: C:\temp");
    }

    [Fact]
    public void WorkingDirDisplay_EmptyDir_ReturnsEmpty()
    {
        var item = new Item("1", "test.exe") { WorkingDir = "" };
        _repo.AddItem(item);
        var vm = new ItemViewModel(item, _repo);

        vm.WorkingDirDisplay.Should().BeEmpty();
    }

    [Fact]
    public void ExecCount_ReflectsItemValue()
    {
        var vm = CreateVm();
        vm.ExecCount.Should().Be(3);
    }

    [Fact]
    public void RunAsOptions_ContainsBothOptions()
    {
        ItemViewModel.RunAsOptions.Should().HaveCount(2);
        ItemViewModel.RunAsOptions.Should().Contain(kvp => kvp.Key == 0 && kvp.Value == "Normal");
        ItemViewModel.RunAsOptions.Should().Contain(kvp => kvp.Key == 1 && kvp.Value == "Administrator");
    }

    #endregion

    #region Edit Flow

    [Fact]
    public void IsEditing_InitiallyFalse()
    {
        var vm = CreateVm();
        vm.IsEditing.Should().BeFalse();
    }

    [Fact]
    public void EditCommand_SetsIsEditingTrue()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);
        vm.IsEditing.Should().BeTrue();
    }

    [Fact]
    public void EditCommand_CannotExecute_WhenAlreadyEditing()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);
        vm.EditCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SaveCommand_CannotExecute_WhenNotEditing()
    {
        var vm = CreateVm();
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CancelCommand_CannotExecute_WhenNotEditing()
    {
        var vm = CreateVm();
        vm.CancelCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SaveCommand_PersistsChanges()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);

        vm.FileName = "newfile.exe";
        vm.Arguments = "--new-arg";
        vm.SubTitle = "New Title";
        vm.RunAs = 1;
        vm.StartMode = 2;
        vm.WorkingDir = @"C:\new";

        vm.SaveCommand.Execute(null);

        vm.IsEditing.Should().BeFalse();
        vm.FileName.Should().Be("newfile.exe");
        vm.Arguments.Should().Be("--new-arg");
        vm.SubTitle.Should().Be("New Title");
        vm.RunAs.Should().Be(1);
        vm.StartMode.Should().Be(2);
        vm.WorkingDir.Should().Be(@"C:\new");
    }

    [Fact]
    public void CancelCommand_RevertsChanges()
    {
        var vm = CreateVm();
        var originalFileName = vm.FileName;

        vm.EditCommand.Execute(null);
        vm.FileName = "changed.exe";
        vm.CancelCommand.Execute(null);

        vm.IsEditing.Should().BeFalse();
        vm.FileName.Should().Be(originalFileName);
    }

    [Fact]
    public void CancelCommand_ClearsNewAliasInput()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);
        vm.NewAliasInput = "draft";
        vm.CancelCommand.Execute(null);

        vm.NewAliasInput.Should().BeEmpty();
    }

    #endregion

    #region Alias Management

    [Fact]
    public void AddAliasCommand_AddsAlias()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);
        vm.NewAliasInput = "new-alias";
        vm.AddAliasCommand.Execute(null);

        vm.Aliases.Should().Contain("new-alias");
    }

    [Fact]
    public void AddAliasCommand_ClearsInput()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);
        vm.NewAliasInput = "new-alias";
        vm.AddAliasCommand.Execute(null);

        vm.NewAliasInput.Should().BeEmpty();
    }

    [Fact]
    public void AddAliasCommand_SkipsDuplicateCaseInsensitive()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);
        var countBefore = vm.Aliases.Count;

        vm.NewAliasInput = "NP";
        vm.AddAliasCommand.Execute(null);

        vm.Aliases.Should().HaveCount(countBefore);
    }

    [Fact]
    public void AddAliasCommand_CannotExecute_WhenInputEmpty()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);
        vm.NewAliasInput = "";

        vm.AddAliasCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void AddAliasCommand_TrimsWhitespace()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);
        vm.NewAliasInput = "  trimmed  ";
        vm.AddAliasCommand.Execute(null);

        vm.Aliases.Should().Contain("trimmed");
    }

    [Fact]
    public void RemoveAliasCommand_RemovesAlias()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);
        vm.RemoveAliasCommand.Execute("np");

        vm.Aliases.Should().NotContain("np");
    }

    #endregion

    #region Property Changed Notifications

    [Fact]
    public void FileName_RaisesPropertyChanged()
    {
        var vm = CreateVm();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.FileName = "changed.exe";

        raised.Should().Contain("FileName");
    }

    [Fact]
    public void IsEditing_RaisesPropertyChanged()
    {
        var vm = CreateVm();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.EditCommand.Execute(null);

        raised.Should().Contain("IsEditing");
    }

    [Fact]
    public void NewAliasInput_RaisesPropertyChanged()
    {
        var vm = CreateVm();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.NewAliasInput = "test";

        raised.Should().Contain("NewAliasInput");
    }

    #endregion

    #region Delete

    [Fact]
    public void DeleteItemCommand_RaisesDeletedEvent()
    {
        var vm = CreateVm();
        var deleted = false;
        vm.Deleted += () => deleted = true;

        vm.DeleteItemCommand.Execute(null);

        deleted.Should().BeTrue();
    }

    [Fact]
    public void DeleteItemCommand_SetsIsEditingFalse()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute(null);
        vm.DeleteItemCommand.Execute(null);

        vm.IsEditing.Should().BeFalse();
    }

    #endregion
}
