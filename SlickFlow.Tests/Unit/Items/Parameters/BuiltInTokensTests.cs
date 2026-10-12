using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

namespace SlickFlow.Tests.Unit.Items.Parameters;

public class BuiltInTokensTests
{
    private static string? Resolve(string token, FakeTokenSources sources) =>
        BuiltInTokens.TryResolve(token, sources, out var value) ? value : null;

    [Theory]
    [InlineData("$clip", true)]
    [InlineData("$date:yyyy", true)]
    [InlineData("$whatever", true)]
    [InlineData("clip", false)]
    [InlineData("a$clip", false)]
    [InlineData("", false)]
    public void IsToken_DetectsDollarPrefix(string name, bool expected)
    {
        BuiltInTokens.IsToken(name).Should().Be(expected);
    }

    [Fact]
    public void TryResolve_Clip_ReturnsClipboardText()
    {
        var sources = new FakeTokenSources { Clipboard = "hello world" };
        Resolve("$clip", sources).Should().Be("hello world");
    }

    [Fact]
    public void TryResolve_Clip_IsCaseInsensitive()
    {
        var sources = new FakeTokenSources { Clipboard = "x" };
        Resolve("$CLIP", sources).Should().Be("x");
        Resolve("$Clip", sources).Should().Be("x");
    }

    [Fact]
    public void TryResolve_Clip_EmptyClipboard_ReturnsEmptyString()
    {
        Resolve("$clip", new FakeTokenSources { Clipboard = "" }).Should().Be("");
    }

    [Fact]
    public void TryResolve_Clip_WithArgument_IsUnknown()
    {
        Resolve("$clip:foo", new FakeTokenSources()).Should().BeNull();
    }

    [Fact]
    public void TryResolve_Date_DefaultFormat()
    {
        Resolve("$date", new FakeTokenSources()).Should().Be("2024-03-07");
    }

    [Fact]
    public void TryResolve_Date_CustomFormat()
    {
        Resolve("$date:yyyyMMdd", new FakeTokenSources()).Should().Be("20240307");
    }

    [Fact]
    public void TryResolve_Date_FormatMayContainColonsAndSpaces()
    {
        Resolve("$date:yyyy-MM-dd HH:mm:ss", new FakeTokenSources())
            .Should().Be("2024-03-07 09:05:42");
    }

    [Fact]
    public void TryResolve_Date_KeywordCaseInsensitive_FormatCasePreserved()
    {
        // "MM" is month and "mm" is minutes - the format must not be lowercased.
        Resolve("$DATE:MM-mm", new FakeTokenSources()).Should().Be("03-05");
    }

    [Fact]
    public void TryResolve_Date_EmptyFormat_FallsBackToDefault()
    {
        Resolve("$date:", new FakeTokenSources()).Should().Be("2024-03-07");
    }

    [Fact]
    public void TryResolve_Date_InvalidFormat_IsNotResolved()
    {
        // A lone "%" is an invalid custom format string and throws FormatException.
        Resolve("$date:%", new FakeTokenSources()).Should().BeNull();
    }

    [Fact]
    public void TryResolve_Time_DefaultFormat()
    {
        Resolve("$time", new FakeTokenSources()).Should().Be("09:05");
    }

    [Fact]
    public void TryResolve_Time_CustomFormat()
    {
        Resolve("$Time:HH-mm-ss", new FakeTokenSources()).Should().Be("09-05-42");
    }

    [Fact]
    public void TryResolve_Env_ReturnsVariableValue()
    {
        var sources = new FakeTokenSources();
        sources.Env["USERNAME"] = "ofek";
        Resolve("$env:USERNAME", sources).Should().Be("ofek");
        Resolve("$ENV:username", sources).Should().Be("ofek");
    }

    [Fact]
    public void TryResolve_Env_UnsetVariable_ReturnsEmptyString()
    {
        Resolve("$env:NOPE_NOT_SET", new FakeTokenSources()).Should().Be("");
    }

    [Theory]
    [InlineData("$env")]
    [InlineData("$env:")]
    public void TryResolve_Env_WithoutName_IsUnknown(string token)
    {
        Resolve(token, new FakeTokenSources()).Should().BeNull();
    }

    [Theory]
    [InlineData("$foo")]
    [InlineData("$selection")]
    [InlineData("$")]
    [InlineData("clip")]
    public void TryResolve_UnknownToken_ReturnsFalse(string token)
    {
        BuiltInTokens.TryResolve(token, new FakeTokenSources(), out _).Should().BeFalse();
    }

    [Fact]
    public void TryResolve_DoesNotReadClipboard_ForOtherTokens()
    {
        var sources = new FakeTokenSources();
        BuiltInTokens.TryResolve("$date", sources, out _);
        BuiltInTokens.TryResolve("$env:X", sources, out _);
        sources.ClipboardReads.Should().Be(0);
    }

    [Fact]
    public void ResolveDefault_TokenDefault_IsResolved()
    {
        BuiltInTokens.ResolveDefault("$clip", new FakeTokenSources { Clipboard = "abc" })
            .Should().Be("abc");
    }

    [Fact]
    public void ResolveDefault_PlainDefault_IsReturnedAsIs()
    {
        BuiltInTokens.ResolveDefault("8080", new FakeTokenSources()).Should().Be("8080");
    }

    [Fact]
    public void ResolveDefault_Null_ReturnsNull()
    {
        BuiltInTokens.ResolveDefault(null, new FakeTokenSources()).Should().BeNull();
    }

    [Fact]
    public void ResolveDefault_UnknownToken_IsReturnedLiterally()
    {
        BuiltInTokens.ResolveDefault("$nope", new FakeTokenSources()).Should().Be("$nope");
    }

    [Fact]
    public void CachingTokenSources_ReadsClipboardOnceAndFreezesClock()
    {
        var inner = new FakeTokenSources { Clipboard = "c" };
        var cached = new CachingTokenSources(inner);

        cached.GetClipboardText().Should().Be("c");
        inner.Clipboard = "changed";
        cached.GetClipboardText().Should().Be("c");
        inner.ClipboardReads.Should().Be(1);

        var first = cached.Now;
        inner.Now = inner.Now.AddDays(1);
        cached.Now.Should().Be(first);
    }

    [Fact]
    public void CachingTokenSources_DoesNotReadClipboardUntilAsked()
    {
        var inner = new FakeTokenSources();
        _ = new CachingTokenSources(inner);
        inner.ClipboardReads.Should().Be(0);
    }
}
