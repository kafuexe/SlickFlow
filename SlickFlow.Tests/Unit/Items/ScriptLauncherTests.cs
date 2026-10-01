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
        var target = ScriptLauncher.Resolve(@"C:\s\tool.py", "a", pythonPath);

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
}
