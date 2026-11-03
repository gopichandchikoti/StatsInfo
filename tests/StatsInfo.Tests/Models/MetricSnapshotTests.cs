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
    public void Unavailable_CreatesProperStatusAndOverride()
    {
        var snapshot = MetricSnapshot.Unavailable("GPU", "GPU", "%", "LibreHW", "DeviceNotFound");

        Assert.Equal(MetricStatus.Unavailable, snapshot.Status);
        Assert.Equal("N/A", snapshot.FormattedValue);
        Assert.Equal("DeviceNotFound", snapshot.ErrorCode);
    }
}
