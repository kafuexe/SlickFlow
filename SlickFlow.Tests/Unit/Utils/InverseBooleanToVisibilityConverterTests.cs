using FluentAssertions;
using System.Globalization;
using System.Windows;

namespace SlickFlow.Tests.Unit.Utils;

public class InverseBooleanToVisibilityConverterTests
{
    private readonly InverseBooleanToVisibilityConverter _converter = new();

    [Fact]
    public void Convert_True_ReturnsCollapsed()
    {
        _converter.Convert(true, typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_False_ReturnsVisible()
    {
        _converter.Convert(false, typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_NonBool_ReturnsVisible()
    {
        _converter.Convert("not a bool", typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);
    }

    [Fact]
    public void ConvertBack_Visible_ReturnsFalse()
    {
        _converter.ConvertBack(Visibility.Visible, typeof(bool), null!, CultureInfo.InvariantCulture)
            .Should().Be(false);
    }

    [Fact]
    public void ConvertBack_Collapsed_ReturnsTrue()
    {
        _converter.ConvertBack(Visibility.Collapsed, typeof(bool), null!, CultureInfo.InvariantCulture)
            .Should().Be(true);
    }

    [Fact]
    public void ConvertBack_Hidden_ReturnsTrue()
    {
        _converter.ConvertBack(Visibility.Hidden, typeof(bool), null!, CultureInfo.InvariantCulture)
            .Should().Be(true);
    }
}
