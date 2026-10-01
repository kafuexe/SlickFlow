using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

namespace SlickFlow.Tests.Unit.Items.Parameters;

public class PlaceholderParserTokenTests
{
    private static readonly IReadOnlyDictionary<string, string> NoValues =
        new Dictionary<string, string>();

    [Fact]
    public void Extract_TokenWithColonAndSpacesInFormat_ParsesWholeNameAsToken()
    {
        var result = PlaceholderParser.Extract("<<$date:yyyy-MM-dd HH:mm>>").ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("$date:yyyy-MM-dd HH:mm");
        result[0].Default.Should().BeNull();
    }

    [Fact]
    public void Extract_TokenAsDefault_KeepsRegularName()
    {
        var result = PlaceholderParser.Extract("<<query=$clip|search term>>").ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("query");
        result[0].Default.Should().Be("$clip");
        result[0].Hint.Should().Be("search term");
    }

    [Fact]
    public void Extract_TokenNextToRegularPlaceholders_ParsesEachSeparately()
    {
        var result = PlaceholderParser.Extract("<<host>>/<<$env:USERNAME>>/<<port=80>>").ToList();

        result.Select(p => p.Name).Should().Equal("host", "$env:USERNAME", "port");
    }

    [Fact]
    public void Substitute_WithTokenSources_ResolvesTokens()
    {
        var sources = new FakeTokenSources { Clipboard = "hello" };
        sources.Env["USERNAME"] = "ofek";

        var result = PlaceholderParser.Substitute(
            "<<$clip>> <<$date>> <<$time>> <<$env:USERNAME>>", NoValues, sources);

        result.Should().Be("hello 2024-03-07 09:05 ofek");
    }

    [Fact]
    public void Substitute_WithoutTokenSources_LeavesTokensLiteral()
    {
        PlaceholderParser.Substitute("x <<$clip>>", NoValues).Should().Be("x <<$clip>>");
    }

    [Fact]
    public void Substitute_UnknownToken_IsLeftLiteral()
    {
        var result = PlaceholderParser.Substitute("a <<$nope>> b", NoValues, new FakeTokenSources());

        result.Should().Be("a <<$nope>> b");
    }

    [Fact]
    public void Substitute_TokenAndRegularPlaceholderTogether()
    {
        var result = PlaceholderParser.Substitute(
            "log-<<$date:yyyyMMdd>>-<<name>>.txt",
            new Dictionary<string, string> { ["name"] = "build" },
            new FakeTokenSources());

        result.Should().Be("log-20240307-build.txt");
    }

    [Fact]
    public void Substitute_TokenIsNotOverriddenBySuppliedValueWithSameName()
    {
        // Tokens are never prompted; a stray value keyed by the token name must not leak in.
        var result = PlaceholderParser.Substitute(
            "<<$clip>>",
            new Dictionary<string, string> { ["$clip"] = "typed" },
            new FakeTokenSources { Clipboard = "clip" });

        result.Should().Be("clip");
    }

    [Fact]
    public void Substitute_TokenDefault_UsedWhenValueMissing()
    {
        var result = PlaceholderParser.Substitute(
            "q=<<query=$clip>>", NoValues, new FakeTokenSources { Clipboard = "cats" });

        result.Should().Be("q=cats");
    }

    [Fact]
    public void Substitute_SuppliedValue_WinsOverTokenDefault()
    {
        var sources = new FakeTokenSources { Clipboard = "cats" };

        var result = PlaceholderParser.Substitute(
            "q=<<query=$clip>>",
            new Dictionary<string, string> { ["query"] = "dogs" },
            sources);

        result.Should().Be("q=dogs");
        sources.ClipboardReads.Should().Be(0);
    }

    [Fact]
    public void Substitute_ExistingDefaultsStillWork_WithTokenSources()
    {
        PlaceholderParser.Substitute("<<port=8080>>", NoValues, new FakeTokenSources())
            .Should().Be("8080");
    }
}
