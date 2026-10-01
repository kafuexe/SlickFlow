using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace SlickFlow.Tests.Unit.Items;

public class ScriptLauncherTests : IDisposable
{
    private readonly string _pythonDir;

    public ScriptLauncherTests()
    {
        _pythonDir = Path.Combine(Path.GetTempPath(), "SlickFlowPy_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pythonDir);
        File.WriteAllText(Path.Combine(_pythonDir, "python.exe"), "");
    }

    public void Dispose()
    {
        if (Directory.Exists(_pythonDir))
            Directory.Delete(_pythonDir, true);
    }

    [Fact]
    public void Resolve_Ps1_RunsThroughPowerShellFile()
    {
        var target = ScriptLauncher.Resolve(@"C:\scripts\do it.ps1", "-Name x", pythonPath: null);

        target.FileName.Should().Be("powershell.exe");
        target.Arguments.Should().Be("-NoProfile -ExecutionPolicy Bypass -File \"C:\\scripts\\do it.ps1\" -Name x");
        target.IsScript.Should().BeTrue();
    }

    [Fact]
    public void Resolve_Ps1_IsCaseInsensitive_AndOmitsTrailingSpaceWithoutArgs()
    {
        var target = ScriptLauncher.Resolve(@"C:\s\Run.PS1", "", pythonPath: null);

        target.FileName.Should().Be("powershell.exe");
        target.Arguments.Should().Be("-NoProfile -ExecutionPolicy Bypass -File \"C:\\s\\Run.PS1\"");
    }

    [Fact]
    public void Resolve_Py_WithInstallFolder_UsesPythonExe()
    {
        var target = ScriptLauncher.Resolve(@"C:\s\tool.py", "--v", _pythonDir);

        target.FileName.Should().Be(Path.Combine(_pythonDir, "python.exe"));
        target.Arguments.Should().Be("\"C:\\s\\tool.py\" --v");
        target.IsScript.Should().BeTrue();
    }

    [Fact]
    public void Resolve_Py_WithExePath_UsesThatExe()
    {
        var exe = Path.Combine(_pythonDir, "python.exe");

        var target = ScriptLauncher.Resolve(@"C:\s\tool.py", "", exe);

        target.FileName.Should().Be(exe);
        target.Arguments.Should().Be("\"C:\\s\\tool.py\"");
    }

    [Fact]
    public void Resolve_Pyw_PrefersPythonwWhenPresent()
    {
        var pythonw = Path.Combine(_pythonDir, "pythonw.exe");
        File.WriteAllText(pythonw, "");

        ScriptLauncher.Resolve(@"C:\s\gui.pyw", "", _pythonDir).FileName.Should().Be(pythonw);
        ScriptLauncher.Resolve(@"C:\s\gui.pyw", "", Path.Combine(_pythonDir, "python.exe"))
            .FileName.Should().Be(pythonw);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\does\not\exist\python.exe")]
    public void Resolve_Py_WithoutUsableInterpreter_FallsBackToShell(string? pythonPath)
    {
        var target = ScriptLauncher.Resolve(@"C:\s\tool.py", "a", pythonPath, findExecutable: NothingInstalled);

        target.FileName.Should().Be(@"C:\s\tool.py");
        target.Arguments.Should().Be("a");
        target.IsScript.Should().BeFalse();
    }

    [Fact]
    public void Resolve_OtherFiles_AreUnchanged()
    {
        var target = ScriptLauncher.Resolve("notepad.exe", "file.txt", _pythonDir);

        target.Should().Be(new ScriptLauncher.LaunchTarget("notepad.exe", "file.txt", false));
    }

    private static string? NothingInstalled(string _) => null;

    private static Func<string, string?> Installed(params string[] exeNames) =>
        name => exeNames.Contains(name, StringComparer.OrdinalIgnoreCase) ? $@"C:\bin\{name}" : null;

    #region PowerShell selection

    [Fact]
    public void Resolve_Ps1_DefaultsToWindowsPowerShell_EvenWhenPwshInstalled()
    {
        var target = ScriptLauncher.Resolve(@"C:\s\a.ps1", "", null, usePwsh: false, Installed("pwsh.exe"));

        target.FileName.Should().Be("powershell.exe");
    }

    [Fact]
    public void Resolve_Ps1_WithPwshEnabled_UsesPwsh()
    {
        var target = ScriptLauncher.Resolve(@"C:\s\a.ps1", "x", null, usePwsh: true, Installed("pwsh.exe"));

        target.FileName.Should().Be(@"C:\bin\pwsh.exe");
        target.Arguments.Should().Be("-NoProfile -ExecutionPolicy Bypass -File \"C:\\s\\a.ps1\" x");
    }

    [Fact]
    public void Resolve_Ps1_WithPwshEnabledButMissing_FallsBackToWindowsPowerShell()
    {
        var target = ScriptLauncher.Resolve(@"C:\s\a.ps1", "", null, usePwsh: true, NothingInstalled);

        target.FileName.Should().Be("powershell.exe");
    }

    #endregion

    #region Python auto-detection

    [Fact]
    public void Resolve_Py_NoConfiguredPath_PrefersPyLauncher()
    {
        var target = ScriptLauncher.Resolve(@"C:\s\tool.py", "", null, findExecutable: Installed("py.exe", "python.exe"));

        target.FileName.Should().Be(@"C:\bin\py.exe");
        target.Arguments.Should().Be("\"C:\\s\\tool.py\"");
        target.IsScript.Should().BeTrue();
    }

    [Fact]
    public void Resolve_Py_NoConfiguredPath_FallsBackToPythonOnPath()
    {
        var target = ScriptLauncher.Resolve(@"C:\s\tool.py", "", null, findExecutable: Installed("python.exe"));

        target.FileName.Should().Be(@"C:\bin\python.exe");
    }

    [Fact]
    public void Resolve_Pyw_NoConfiguredPath_PrefersWindowlessInterpreter()
    {
        ScriptLauncher.Resolve(@"C:\s\gui.pyw", "", null, findExecutable: Installed("pyw.exe", "py.exe"))
            .FileName.Should().Be(@"C:\bin\pyw.exe");
        ScriptLauncher.Resolve(@"C:\s\gui.pyw", "", null, findExecutable: Installed("pythonw.exe", "python.exe"))
            .FileName.Should().Be(@"C:\bin\pythonw.exe");
    }

    [Fact]
    public void Resolve_Py_ConfiguredPathWinsOverDetection()
    {
        var target = ScriptLauncher.Resolve(@"C:\s\tool.py", "", _pythonDir, findExecutable: Installed("py.exe"));

        target.FileName.Should().Be(Path.Combine(_pythonDir, "python.exe"));
    }

    [Fact]
    public void Resolve_Py_InvalidConfiguredPath_FallsBackToDetection()
    {
        var target = ScriptLauncher.Resolve(
            @"C:\s\tool.py", "", @"C:\does\not\exist", findExecutable: Installed("py.exe"));

        target.FileName.Should().Be(@"C:\bin\py.exe");
    }

    [Fact]
    public void DetectPython_NothingInstalled_ReturnsNull()
    {
        ScriptLauncher.DetectPython(windowless: false, NothingInstalled).Should().BeNull();
    }

    [Fact]
    public void FindExecutable_UnknownName_ReturnsNull()
    {
        ScriptLauncher.FindExecutable($"slickflow_missing_{Guid.NewGuid():N}.exe").Should().BeNull();
    }

    #endregion
}
