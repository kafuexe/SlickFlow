using System.IO;
using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Settings;

namespace SlickFlow.Tests.Integration;

public class SettingsManagerTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SlickFlowSettingsTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_NoFile_CreatesDefaults()
    {
        var settings = SettingsManager.Load(_tempDir);
        settings.Should().NotBeNull();
        settings.DbFilePath.Should().NotBeNullOrEmpty();
        settings.IconDirPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Load_CreatesDbFile()
    {
        var settings = SettingsManager.Load(_tempDir);
        File.Exists(settings.DbFilePath).Should().BeTrue();
    }

    [Fact]
    public void Load_CreatesIconDirectory()
    {
        var settings = SettingsManager.Load(_tempDir);
        Directory.Exists(settings.IconDirPath).Should().BeTrue();
    }

    [Fact]
    public void SaveAndReload_PreservesSettings()
    {
        var settings = new Settings
        {
            DbFilePath = Path.Combine(_tempDir, "custom.json"),
            IconDirPath = Path.Combine(_tempDir, "customicons")
        };
        SettingsManager.Save(settings, _tempDir);
        var reloaded = SettingsManager.Load(_tempDir);
        reloaded.DbFilePath.Should().Be(settings.DbFilePath);
        reloaded.IconDirPath.Should().Be(settings.IconDirPath);
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsDefaults()
    {
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsPath, "not valid json{{{");
        var settings = SettingsManager.Load(_tempDir);
        settings.Should().NotBeNull();
        settings.DbFilePath.Should().NotBeNullOrEmpty();
    }
}
