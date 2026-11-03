using System.Globalization;
using System.Windows;
using StatsInfo.Converters;
using Xunit;

namespace StatsInfo.Tests.Converters;

public class BooleanToVisibilityConverterTests
{
    private readonly BooleanToVisibilityConverter _converter = new();

    [Theory]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    public void Convert_MapsBooleanToVisibility(bool value, Visibility expected)
    {
        Assert.Equal(expected, _converter.Convert(value, typeof(Visibility), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Convert_ReturnsVisible_ForNonBooleanValues()
    {
        Assert.Equal(Visibility.Visible,
            _converter.Convert("true", typeof(Visibility), null!, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(Visibility.Visible, true)]
    [InlineData(Visibility.Hidden, false)]
    [InlineData(Visibility.Collapsed, false)]
    public void ConvertBack_MapsVisibilityToBoolean(Visibility value, bool expected)
    {
        Assert.Equal(expected, _converter.ConvertBack(value, typeof(bool), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_ReturnsFalse_ForNonVisibilityValues()
    {
        Assert.False((bool)_converter.ConvertBack("Visible", typeof(bool), null!, CultureInfo.InvariantCulture));
    }
}
