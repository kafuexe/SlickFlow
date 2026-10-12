using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.ViewModels.Settings;

namespace SlickFlow.Tests.Unit.ViewModels;

public class SettingsViewModelImportTests : IDisposable
{
    private sealed class FakeDialogs : ISettingsDialogService
    {
        public string? FileToReturn { get; set; }
        public string? RequestedInitialDirectory { get; private set; }
        public int PickCalls { get; private set; }
        public List<(string Title, string Message, bool IsError)> Messages { get; } = new();

        public string? PickSlickRunFile(string? initialDirectory)
        {
            PickCalls++;
            RequestedInitialDirectory = initialDirectory;
            return FileToReturn;
        }

        public void ShowMessage(string title, string message, bool isError = false)
            => Messages.Add((title, message, isError));

        public List<string> OpenedUrls { get; } = new();

        public void OpenUrl(string url) => OpenedUrls.Add(url);
    }

    private readonly string _tempDir;
    private readonly ItemRepository _repo;
    private readonly FakeDialogs _dialogs = new();
    private readonly string _slickRunDir;

    public SettingsViewModelImportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SlickFlowVmImport_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _repo = new ItemRepository(Path.Combine(_tempDir, "db.json"));
        _slickRunDir = Path.Combine(_tempDir, "SlickRun");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private SettingsViewModel CreateVm() => new(_repo, _dialogs, _slickRunDir);

    private string WriteQrs(string content)
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid()}.qrs");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void OpenHelpCommand_OpensReadme()
    {
        CreateVm().OpenHelpCommand.Execute(null);

        _dialogs.OpenedUrls.Should().Equal(SettingsViewModel.ReadmeUrl);
    }

    [Fact]
    public void ImportSlickRunCommand_IsExposed()
    {
        CreateVm().ImportSlickRunCommand.Should().NotBeNull();
    }

    [Fact]
    public void Import_PointsDialogAtSlickRunFolder_WhenItExists()
    {
        Directory.CreateDirectory(_slickRunDir);

        CreateVm().ImportSlickRunCommand.Execute(null);

        _dialogs.RequestedInitialDirectory.Should().Be(_slickRunDir);
    }

    [Fact]
    public void Import_NoInitialDirectory_WhenSlickRunFolderMissing()
    {
        CreateVm().ImportSlickRunCommand.Execute(null);

        _dialogs.PickCalls.Should().Be(1);
        _dialogs.RequestedInitialDirectory.Should().BeNull();
    }

    [Fact]
    public void Import_Cancelled_DoesNothing()
    {
        _dialogs.FileToReturn = null;
        var vm = CreateVm();

        vm.ImportSlickRunCommand.Execute(null);

        _dialogs.Messages.Should().BeEmpty();
        vm.Items.Should().BeEmpty();
    }

    [Fact]
    public void Import_AddsItems_RefreshesListAndShowsSummary()
    {
        _repo.AddItem(new Item { FileName = "mine", Aliases = new List<string> { "g" } });
        _dialogs.FileToReturn = WriteQrs("[g]\nFilename=x\n[calc]\nFilename=calc.exe\n[np]\nFilename=notepad.exe\n");
        var vm = CreateVm();
        var itemsBefore = vm.Items;

        vm.ImportSlickRunCommand.Execute(null);

        vm.Items.Should().HaveCount(3);
        vm.Items.Should().NotBeSameAs(itemsBefore, "the list is swapped once rather than mutated per item");
        _dialogs.Messages.Should().ContainSingle();
        var msg = _dialogs.Messages[0];
        msg.IsError.Should().BeFalse();
        msg.Message.Should().StartWith("Imported 2, skipped 1");
        msg.Message.Should().Contain("alias already exists: g");
    }

    [Fact]
    public void Import_RaisesItemsChangedOnce()
    {
        _dialogs.FileToReturn = WriteQrs("[a]\nFilename=a\n[b]\nFilename=b\n[c]\nFilename=c\n");
        var vm = CreateVm();
        var itemsChanged = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Items)) itemsChanged++; };

        vm.ImportSlickRunCommand.Execute(null);

        itemsChanged.Should().Be(1);
    }

    [Fact]
    public void Import_UnreadableFile_ShowsError()
    {
        _dialogs.FileToReturn = Path.Combine(_tempDir, "missing.qrs");

        CreateVm().ImportSlickRunCommand.Execute(null);

        _dialogs.Messages.Should().ContainSingle().Which.IsError.Should().BeTrue();
    }
}
