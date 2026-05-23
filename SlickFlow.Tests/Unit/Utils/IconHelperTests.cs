using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Utils.Icons;

namespace SlickFlow.Tests.Unit.Utils;

public class IconHelperTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private string NewTempDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), prefix + "_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task TrySaveIconAsync_BareCommandResolvedOnPath_SavesIcon()
    {
        // Regression: items added with bare command names (e.g. "notepad") were
        // never given icons because File.Exists("notepad") is false. The helper
        // must resolve PATH+PATHEXT before giving up.
        var iconDir = NewTempDir("SlickFlowIconHelperTests");
        var helper = new IconHelper(iconDir);

        var (ok, savedPath) = await helper.TrySaveIconAsync("cmd", "test-id-cmd");

        ok.Should().BeTrue();
        savedPath.Should().NotBeEmpty();
        File.Exists(savedPath).Should().BeTrue();
    }

    [Fact]
    public async Task TrySaveIconAsync_NonExistentCommand_ReturnsFalse()
    {
        var iconDir = NewTempDir("SlickFlowIconHelperTests");
        var helper = new IconHelper(iconDir);

        var (ok, savedPath) = await helper.TrySaveIconAsync("definitely_not_a_real_command_xyzzy", "test-id-missing");

        ok.Should().BeFalse();
        savedPath.Should().BeEmpty();
    }
}
