using StatsInfo.Models;
using Xunit;

namespace StatsInfo.Tests.Models;

public class MetricSnapshotTests
{
    [Fact]
    public void FormattedValue_ReturnsFormattedOverride_WhenProvided()
    {
        var snapshot = new MetricSnapshot(
            Key: "TEST",
            DisplayName: "Test",
            Value: 42.5,
            Unit: "%",
            Timestamp: DateTime.UtcNow,
            Status: MetricStatus.Active,
            Source: "UnitTests",
            FormattedOverride: "Custom 42.5%"
        );

        Assert.Equal("Custom 42.5%", snapshot.FormattedValue);
    }

    [Fact]
    public void FormattedValue_ReturnsFormattedValueAndUnit_WhenOverrideNull()
    {
        var snapshot = new MetricSnapshot(
            Key: "TEST",
            DisplayName: "Test",
            Value: 42.5,
            Unit: "%",
            Timestamp: DateTime.UtcNow,
            Status: MetricStatus.Active,
            Source: "UnitTests"
        );

        Assert.Equal("42.5 %", snapshot.FormattedValue);
    }

    [Fact]
    public void IsFresh_ReturnsTrue_WhenWithinTimeWindowAndActive()
    {
        var snapshot = new MetricSnapshot(
            Key: "TEST",
            DisplayName: "Test",
            Value: 10.0,
            Unit: "%",
            Timestamp: DateTime.UtcNow,
            Status: MetricStatus.Active,
            Source: "UnitTests"
        );

        Assert.True(snapshot.IsFresh(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void IsFresh_ReturnsFalse_WhenStale()
    {
        var snapshot = new MetricSnapshot(
            Key: "TEST",
            DisplayName: "Test",
            Value: 10.0,
            Unit: "%",
            Timestamp: DateTime.UtcNow.AddMinutes(-2),
            Status: MetricStatus.Active,
            Source: "UnitTests"
        );

        Assert.False(snapshot.IsFresh(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void IsFresh_ReturnsFalse_WhenStatusIsNotActive()
    {
        var snapshot = MetricSnapshot.Empty("TEST", "Test", "%", "UnitTests");

        Assert.False(snapshot.IsFresh(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Empty_CreatesInitializingSnapshot()
    {
        var snapshot = MetricSnapshot.Empty("TEST", "Test", "%", "UnitTests");

        Assert.Equal("TEST", snapshot.Key);
        Assert.Equal("Test", snapshot.DisplayName);
        Assert.Equal("%", snapshot.Unit);
        Assert.Equal(MetricStatus.Initializing, snapshot.Status);
        Assert.Equal("0.0 %", snapshot.FormattedValue);
    }

    [Fact]
    public void Unavailable_CreatesProperStatusAndOverride()
    {
        var snapshot = MetricSnapshot.Unavailable("GPU", "GPU", "%", "LibreHW", "DeviceNotFound");

        Assert.Equal(MetricStatus.Unavailable, snapshot.Status);
        Assert.Equal("N/A", snapshot.FormattedValue);
        Assert.Equal("DeviceNotFound", snapshot.ErrorCode);
    }

    [Fact]
    public void SecondaryFormattedValue_UsesItsOwnValueAndUnit()
    {
        var snapshot = new MetricSnapshot(
            "GPU_METRICS",
            "GPU",
            72,
            "%",
            DateTime.UtcNow,
            MetricStatus.Active,
            "UnitTests",
            SecondaryValue: 64,
            SecondaryUnit: "°C");

        Assert.Equal("64 °C", snapshot.SecondaryFormattedValue);
    }

    [Fact]
    public void SecondaryFormattedValue_IsNullWhenSensorValueIsUnavailable()
    {
        var snapshot = new MetricSnapshot(
            "GPU_METRICS",
            "GPU",
            72,
            "%",
            DateTime.UtcNow,
            MetricStatus.Active,
            "UnitTests",
            SecondaryUnit: "°C");

        Assert.Null(snapshot.SecondaryFormattedValue);
    }
}
