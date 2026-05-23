using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

namespace SlickFlow.Tests.Unit.Items.Parameters;

public class PromptModeParserTests
{
    [Theory]
    [InlineData("")]
    [InlineData("server")]                       // no pipe yet, normal search
    [InlineData("  ")]
    [InlineData("server |")]                     // pipe but no current prompt
    [InlineData("server | port")]                // no colon
    [InlineData("server | nokey | port: 8080")]  // middle segment lacks '='
    [InlineData("| port: 8080")]                 // empty alias
    public void TryParse_NotInPromptMode_ReturnsNull(string query)
    {
        PromptModeParser.TryParse(query).Should().BeNull();
    }

    [Fact]
    public void TryParse_FirstPromptEmpty_ReturnsState()
    {
        var state = PromptModeParser.TryParse("server | port: ");

        state.Should().NotBeNull();
        state!.Alias.Should().Be("server");
        state.Filled.Should().BeEmpty();
        state.CurrentName.Should().Be("port");
        state.CurrentInput.Should().Be("");
    }

    [Fact]
    public void TryParse_FirstPromptWithInput_ReturnsInput()
    {
        var state = PromptModeParser.TryParse("server | port: 8080");

        state.Should().NotBeNull();
        state!.CurrentName.Should().Be("port");
        state.CurrentInput.Should().Be("8080");
    }

    [Fact]
    public void TryParse_OneFilledPlusCurrent_ReturnsBoth()
    {
        var state = PromptModeParser.TryParse("server | port=8080 | host: 0.0.0.0");

        state.Should().NotBeNull();
        state!.Alias.Should().Be("server");
        state.Filled.Should().HaveCount(1);
        state.Filled[0].Should().Be(("port", "8080"));
        state.CurrentName.Should().Be("host");
        state.CurrentInput.Should().Be("0.0.0.0");
    }

    [Fact]
    public void TryParse_MultipleFilled_PreservesOrder()
    {
        var state = PromptModeParser.TryParse("server | a=1 | b=2 | c=3 | d: 4");

        state.Should().NotBeNull();
        state!.Filled.Should().HaveCount(3);
        state.Filled[0].Should().Be(("a", "1"));
        state.Filled[1].Should().Be(("b", "2"));
        state.Filled[2].Should().Be(("c", "3"));
        state.CurrentName.Should().Be("d");
        state.CurrentInput.Should().Be("4");
    }

    [Fact]
    public void TryParse_FilledValueWithEquals_SplitsOnFirstEqualsOnly()
    {
        var state = PromptModeParser.TryParse("server | conn=user=admin | host: x");

        state.Should().NotBeNull();
        state!.Filled[0].Should().Be(("conn", "user=admin"));
    }

    [Fact]
    public void TryParse_CurrentInputWithColon_SplitsOnFirstColonOnly()
    {
        var state = PromptModeParser.TryParse("server | url: http://x:9000");

        state.Should().NotBeNull();
        state!.CurrentName.Should().Be("url");
        state.CurrentInput.Should().Be("http://x:9000");
    }

    [Fact]
    public void Format_NoFilled_BuildsFirstPromptString()
    {
        var formatted = PromptModeParser.Format("server", filled: Array.Empty<(string, string)>(), "port", "8080");

        formatted.Should().Be("server | port: 8080");
    }

    [Fact]
    public void Format_OneFilled_AppendsKeyValueThenCurrent()
    {
        var formatted = PromptModeParser.Format(
            "server",
            filled: new[] { ("port", "8080") },
            "host",
            "");

        formatted.Should().Be("server | port=8080 | host: ");
    }

    [Fact]
    public void Format_AndParse_RoundTrip()
    {
        var formatted = PromptModeParser.Format(
            "server",
            filled: new[] { ("port", "8080"), ("host", "0.0.0.0") },
            "user",
            "admin");

        var state = PromptModeParser.TryParse(formatted);

        state.Should().NotBeNull();
        state!.Alias.Should().Be("server");
        state.Filled.Should().BeEquivalentTo(new[] { ("port", "8080"), ("host", "0.0.0.0") });
        state.CurrentName.Should().Be("user");
        state.CurrentInput.Should().Be("admin");
    }
}
