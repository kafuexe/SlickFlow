using FluentAssertions;
using System.Globalization;

namespace SlickFlow.Tests.Unit.Utils;

public class InverseBooleanConverterTests
{
    private readonly InverseBooleanConverter _converter = new();

    [Fact]
    public void Convert_True_ReturnsFalse()
    {
        _converter.Convert(true, typeof(bool), null!, CultureInfo.InvariantCulture)
            .Should().Be(false);
    }

    [Fact]
    public void Convert_False_ReturnsTrue()
    {
        _converter.Convert(false, typeof(bool), null!, CultureInfo.InvariantCulture)
            .Should().Be(true);
    }

    [Fact]
    public void ConvertBack_True_ReturnsFalse()
    {
        _converter.ConvertBack(true, typeof(bool), null!, CultureInfo.InvariantCulture)
            .Should().Be(false);
    }

    [Fact]
    public void ConvertBack_False_ReturnsTrue()
    {
        _converter.ConvertBack(false, typeof(bool), null!, CultureInfo.InvariantCulture)
            .Should().Be(true);
    }
}
