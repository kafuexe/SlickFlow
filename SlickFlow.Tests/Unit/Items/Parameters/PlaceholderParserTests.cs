using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

namespace SlickFlow.Tests.Unit.Items.Parameters;

public class PlaceholderParserTests
{
    [Fact]
    public void Extract_NoPlaceholders_ReturnsEmpty()
    {
        PlaceholderParser.Extract("hello world").Should().BeEmpty();
    }

    [Fact]
    public void Extract_EmptyString_ReturnsEmpty()
    {
        PlaceholderParser.Extract("").Should().BeEmpty();
    }

    [Fact]
    public void Extract_NullString_ReturnsEmpty()
    {
        PlaceholderParser.Extract(null!).Should().BeEmpty();
    }

    [Fact]
    public void Extract_NameOnly_ReturnsPlaceholderWithNullDefaultAndHint()
    {
        var result = PlaceholderParser.Extract("http://localhost:<<port>>").ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("port");
        result[0].Default.Should().BeNull();
        result[0].Hint.Should().BeNull();
    }

    [Fact]
    public void Extract_NameAndDefault_ParsesBoth()
    {
        var result = PlaceholderParser.Extract("<<port=8080>>").ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("port");
        result[0].Default.Should().Be("8080");
        result[0].Hint.Should().BeNull();
    }

    [Fact]
    public void Extract_NameAndHint_ParsesBoth()
    {
        var result = PlaceholderParser.Extract("<<port|web server port>>").ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("port");
        result[0].Default.Should().BeNull();
        result[0].Hint.Should().Be("web server port");
    }

    [Fact]
    public void Extract_NameDefaultAndHint_ParsesAll()
    {
        var result = PlaceholderParser.Extract("<<port=8080|web server port>>").ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("port");
        result[0].Default.Should().Be("8080");
        result[0].Hint.Should().Be("web server port");
    }

    [Fact]
    public void Extract_MultiplePlaceholders_PreservesOrder()
    {
        var result = PlaceholderParser.Extract("<<a>> middle <<b=2>> end <<c|hint>>").ToList();

        result.Should().HaveCount(3);
        result[0].Name.Should().Be("a");
        result[1].Name.Should().Be("b");
        result[1].Default.Should().Be("2");
        result[2].Name.Should().Be("c");
        result[2].Hint.Should().Be("hint");
    }

    [Fact]
    public void Extract_RepeatedName_ReturnsBothOccurrences()
    {
        // Parser is the raw extractor - it returns every occurrence.
        // Deduplication by name is the schema's responsibility, not the parser's.
        var result = PlaceholderParser.Extract("<<port>>:<<port>>").ToList();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("port");
        result[1].Name.Should().Be("port");
    }

    [Theory]
    [InlineData("<port>")]              // single angle brackets, not a placeholder
    [InlineData("<<>>")]                // empty name
    [InlineData("<<a")]                 // unterminated
    [InlineData("a>>")]                 // no opening
    public void Extract_Malformed_ReturnsEmpty(string input)
    {
        PlaceholderParser.Extract(input).Should().BeEmpty();
    }

    [Fact]
    public void Extract_GarbageBeforeValidPlaceholder_StillFindsValidOne()
    {
        // The parser finds placeholders wherever they appear; surrounding garbage is harmless.
        var result = PlaceholderParser.Extract("<<a<<b>>").ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("b");
    }

    [Fact]
    public void Extract_EmptyDefault_AllowedAsEmptyString()
    {
        // <<port=>> means port has an explicit empty default.
        var result = PlaceholderParser.Extract("<<port=>>").ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("port");
        result[0].Default.Should().Be("");
    }

    [Fact]
    public void ContainsPlaceholders_True_WhenAny()
    {
        PlaceholderParser.ContainsPlaceholders("hi <<x>>").Should().BeTrue();
    }

    [Fact]
    public void ContainsPlaceholders_False_WhenNone()
    {
        PlaceholderParser.ContainsPlaceholders("hi there").Should().BeFalse();
    }

    [Fact]
    public void ContainsPlaceholders_False_OnNullOrEmpty()
    {
        PlaceholderParser.ContainsPlaceholders("").Should().BeFalse();
        PlaceholderParser.ContainsPlaceholders(null!).Should().BeFalse();
    }
}
