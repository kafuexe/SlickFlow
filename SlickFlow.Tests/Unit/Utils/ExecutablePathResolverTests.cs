using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Utils.Icons;

namespace SlickFlow.Tests.Unit.Utils;

public class ExecutablePathResolverTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SlickFlowResolverTests_" + Guid.NewGuid());
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolve_NullOrWhitespace_ReturnsFalse(string? input)
    {
        ExecutablePathResolver.TryResolve(input!, out var resolved, "any", ".EXE")
            .Should().BeFalse();
        resolved.Should().BeEmpty();
    }

    [Fact]
    public void TryResolve_RootedPath_ReturnsFalse()
    {
        // Resolver intentionally defers rooted paths to the caller's File.Exists check.
        ExecutablePathResolver.TryResolve(@"C:\Windows\notepad.exe", out var resolved, "any", ".EXE")
            .Should().BeFalse();
        resolved.Should().BeEmpty();
    }

    [Fact]
    public void TryResolve_PathWithSeparator_ReturnsFalse()
    {
        ExecutablePathResolver.TryResolve(@"subdir\notepad.exe", out _, "any", ".EXE")
            .Should().BeFalse();
        ExecutablePathResolver.TryResolve("subdir/notepad.exe", out _, "any", ".EXE")
            .Should().BeFalse();
    }

    [Fact]
    public void TryResolve_BareCommand_FindsExecutableViaPathExt()
    {
        var dir = NewTempDir();
        var exePath = Path.Combine(dir, "fakebin.exe");
        File.WriteAllBytes(exePath, Array.Empty<byte>());

        var ok = ExecutablePathResolver.TryResolve("fakebin", out var resolved, dir, ".EXE;.CMD");
        ok.Should().BeTrue();
        resolved.Should().Be(exePath);
    }

    [Fact]
    public void TryResolve_CommandWithExtension_DoesNotDoubleAppend()
    {
        var dir = NewTempDir();
        var exePath = Path.Combine(dir, "fakebin.exe");
        File.WriteAllBytes(exePath, Array.Empty<byte>());

        var ok = ExecutablePathResolver.TryResolve("fakebin.exe", out var resolved, dir, ".EXE;.CMD");
        ok.Should().BeTrue();
        resolved.Should().Be(exePath);
    }

    [Fact]
    public void TryResolve_NotFound_ReturnsFalse()
    {
        var dir = NewTempDir();
        var ok = ExecutablePathResolver.TryResolve("doesnotexist_xyzzy", out var resolved, dir, ".EXE;.CMD");
        ok.Should().BeFalse();
        resolved.Should().BeEmpty();
    }

    [Fact]
    public void TryResolve_MultipleDirs_ReturnsFirstHit()
    {
        var d1 = NewTempDir();
        var d2 = NewTempDir();

        var firstHit = Path.Combine(d1, "tool.cmd");
        File.WriteAllBytes(firstHit, Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(d2, "tool.cmd"), Array.Empty<byte>());

        var path = d1 + Path.PathSeparator + d2;
        var ok = ExecutablePathResolver.TryResolve("tool", out var resolved, path, ".EXE;.CMD");

        ok.Should().BeTrue();
        resolved.Should().Be(firstHit);
    }

    [Fact]
    public void TryResolve_PathExtOrderControlsExtensionPreference()
    {
        var dir = NewTempDir();
        // Both .bat and .exe exist; PATHEXT lists .EXE first, so .exe must win.
        File.WriteAllBytes(Path.Combine(dir, "tool.bat"), Array.Empty<byte>());
        var exePath = Path.Combine(dir, "tool.exe");
        File.WriteAllBytes(exePath, Array.Empty<byte>());

        var ok = ExecutablePathResolver.TryResolve("tool", out var resolved, dir, ".EXE;.BAT");

        ok.Should().BeTrue();
        resolved.Should().Be(exePath);
    }

    [Fact]
    public void TryResolve_QuotedPathEntries_AreUnquoted()
    {
        var dir = NewTempDir();
        var exePath = Path.Combine(dir, "fakebin.exe");
        File.WriteAllBytes(exePath, Array.Empty<byte>());

        var path = "\"" + dir + "\"";
        var ok = ExecutablePathResolver.TryResolve("fakebin", out var resolved, path, ".EXE");

        ok.Should().BeTrue();
        resolved.Should().Be(exePath);
    }

    [Fact]
    public void TryResolve_EmptyOrIllegalEntries_AreSkipped()
    {
        var dir = NewTempDir();
        var exePath = Path.Combine(dir, "fakebin.exe");
        File.WriteAllBytes(exePath, Array.Empty<byte>());

        var path = Path.PathSeparator + "   " + Path.PathSeparator + "<bad|dir>" + Path.PathSeparator + dir;
        var ok = ExecutablePathResolver.TryResolve("fakebin", out var resolved, path, ".EXE");

        ok.Should().BeTrue();
        resolved.Should().Be(exePath);
    }
}
