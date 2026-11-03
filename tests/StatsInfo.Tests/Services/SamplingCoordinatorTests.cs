using Moq;
using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class SamplingCoordinatorTests
{
    [Fact]
    public async Task Coordinator_CollectsMetrics_FromAllRegisteredCollectors()
    {
        var mockCollector1 = new Mock<IMetricCollector>();
        mockCollector1.Setup(c => c.Key).Returns("CPU");
        mockCollector1.Setup(c => c.DisplayName).Returns("CPU");
        mockCollector1.Setup(c => c.Source).Returns("Mock");
        mockCollector1.Setup(c => c.CollectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricSnapshot("CPU", "CPU", 25.0, "%", DateTime.UtcNow, MetricStatus.Active, "Mock"));

        var mockCollector2 = new Mock<IMetricCollector>();
        mockCollector2.Setup(c => c.Key).Returns("RAM");
        mockCollector2.Setup(c => c.DisplayName).Returns("RAM");
        mockCollector2.Setup(c => c.Source).Returns("Mock");
        mockCollector2.Setup(c => c.CollectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricSnapshot("RAM", "RAM", 8.0, "GB", DateTime.UtcNow, MetricStatus.Active, "Mock"));

        using var coordinator = new SamplingCoordinator(new[] { mockCollector1.Object, mockCollector2.Object })
        {
            IntervalMs = 50
        };

        var tcs = new TaskCompletionSource<bool>();
        coordinator.MetricsUpdated += (snapshots) =>
        {
            if (snapshots.ContainsKey("CPU") && snapshots.ContainsKey("RAM"))
            {
                tcs.TrySetResult(true);
            }
        };

        coordinator.Start();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        coordinator.Stop();

        Assert.Equal(tcs.Task, completed);
        var latest = coordinator.GetLatestSnapshots();
        Assert.Equal(25.0, latest["CPU"].Value);
        Assert.Equal(8.0, latest["RAM"].Value);
    }
}
