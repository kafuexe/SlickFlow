using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace SlickFlow.Tests.Unit.Items;

public class ItemSubstituteTests
{
    [Fact]
    public void Substitute_NoPlaceholders_ReturnsCloneWithIdenticalContent()
    {
        var item = new Item("1", "notepad.exe") { Arguments = "file.txt" };

        var clone = item.Substitute(new Dictionary<string, string>());

        clone.Should().NotBeSameAs(item);
        clone.FileName.Should().Be("notepad.exe");
        clone.Arguments.Should().Be("file.txt");
    }

    [Fact]
    public void Substitute_ReplacesPlaceholderInFileName()
    {
        var item = new Item("1", "http://localhost:<<port>>");

        var clone = item.Substitute(new Dictionary<string, string> { ["port"] = "8080" });

        clone.FileName.Should().Be("http://localhost:8080");
    }

    [Fact]
    public void Substitute_ReplacesPlaceholderInArguments()
    {
        var item = new Item("1", "script.exe") { Arguments = "--port <<port>>" };

        var clone = item.Substitute(new Dictionary<string, string> { ["port"] = "8080" });

        clone.Arguments.Should().Be("--port 8080");
    }

    [Fact]
    public void Substitute_ReplacesMultipleDistinctPlaceholders()
    {
        var item = new Item("1", "<<host>>:<<port>>") { Arguments = "--user <<user>>" };

        var clone = item.Substitute(new Dictionary<string, string>
        {
            ["host"] = "example.com",
            ["port"] = "443",
            ["user"] = "admin"
        });

        clone.FileName.Should().Be("example.com:443");
        clone.Arguments.Should().Be("--user admin");
    }

    [Fact]
    public void Substitute_ReplacesRepeatedPlaceholderEverywhere()
    {
        var item = new Item("1", "<<port>>") { Arguments = "--also <<port>> <<port>>" };

        var clone = item.Substitute(new Dictionary<string, string> { ["port"] = "8080" });

        clone.FileName.Should().Be("8080");
        clone.Arguments.Should().Be("--also 8080 8080");
    }

    [Fact]
    public void Substitute_HandlesPlaceholderWithDefault()
    {
        var item = new Item("1", "http://localhost:<<port=8080>>");

        var clone = item.Substitute(new Dictionary<string, string> { ["port"] = "9090" });

        clone.FileName.Should().Be("http://localhost:9090");
    }

    [Fact]
    public void Substitute_HandlesPlaceholderWithHint()
    {
        var item = new Item("1", "http://<<host|target host>>");

        var clone = item.Substitute(new Dictionary<string, string> { ["host"] = "example.com" });

        clone.FileName.Should().Be("http://example.com");
    }

    [Fact]
    public void Substitute_HandlesPlaceholderWithDefaultAndHint()
    {
        var item = new Item("1", "http://<<host=localhost|target host>>:<<port=8080|web port>>");

        var clone = item.Substitute(new Dictionary<string, string>
        {
            ["host"] = "example.com",
            ["port"] = "443"
        });

        clone.FileName.Should().Be("http://example.com:443");
    }

    [Fact]
    public void Substitute_FallsBackToDefault_WhenValueMissingFromDict()
    {
        var item = new Item("1", "http://localhost:<<port=8080>>");

        var clone = item.Substitute(new Dictionary<string, string>());

        clone.FileName.Should().Be("http://localhost:8080");
    }

    [Fact]
    public void Substitute_LeavesPlaceholderLiteral_WhenNoValueAndNoDefault()
    {
        // Best-effort safety: an unfilled placeholder with no default stays visible
        // in the output rather than becoming empty, so failure modes are debuggable.
        var item = new Item("1", "http://localhost:<<port>>");

        var clone = item.Substitute(new Dictionary<string, string>());

        clone.FileName.Should().Be("http://localhost:<<port>>");
    }

    [Fact]
    public void Substitute_DoesNotMutateOriginal()
    {
        var item = new Item("1", "<<port>>");

        var _ = item.Substitute(new Dictionary<string, string> { ["port"] = "8080" });

        item.FileName.Should().Be("<<port>>");
    }

    [Fact]
    public void Substitute_PreservesNonStringFields()
    {
        var item = new Item("42", "<<host>>", new[] { "alias1", "alias2" })
        {
            Arguments = "<<arg>>",
            SubTitle = "sub",
            RunAs = 1,
            StartMode = 2,
            WorkingDir = "C:/work",
            ExecCount = 17,
            IconPath = "icon.png"
        };

        var clone = item.Substitute(new Dictionary<string, string>
        {
            ["host"] = "h",
            ["arg"] = "a"
        });

        clone.Id.Should().Be("42");
        clone.Aliases.Should().BeEquivalentTo(new[] { "alias1", "alias2" });
        clone.SubTitle.Should().Be("sub");
        clone.RunAs.Should().Be(1);
        clone.StartMode.Should().Be(2);
        clone.WorkingDir.Should().Be("C:/work");
        clone.ExecCount.Should().Be(17);
        clone.IconPath.Should().Be("icon.png");
    }
}
