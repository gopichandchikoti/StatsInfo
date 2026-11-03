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

    [Fact]
    public async Task Coordinator_RecordsCollectorErrors_AndIgnoresDuplicateStart()
    {
        var collector = new Mock<IMetricCollector>();
        collector.Setup(c => c.Key).Returns("CPU");
        collector.Setup(c => c.DisplayName).Returns("CPU");
        collector.Setup(c => c.Source).Returns("Mock");
        collector.Setup(c => c.CollectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensor failed"));

        using var coordinator = new SamplingCoordinator(new[] { collector.Object })
        {
            IntervalMs = 50
        };
        var errorReported = new TaskCompletionSource<MetricSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.MetricsUpdated += snapshots =>
        {
            if (snapshots.TryGetValue("CPU", out var snapshot) && snapshot.Status == MetricStatus.Error)
            {
                errorReported.TrySetResult(snapshot);
            }
        };

        coordinator.Start();
        coordinator.Start();
        Assert.True(coordinator.IsRunning);
        var errorSnapshot = await errorReported.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Stop();

        Assert.Equal("InvalidOperationException", errorSnapshot.ErrorCode);
        Assert.True(coordinator.GetLatestSnapshots().ContainsKey("CPU"));
        Assert.False(coordinator.IsRunning);
    }

    [Fact]
    public async Task Coordinator_ContinuesSampling_WhenMetricsUpdatedSubscriberThrows()
    {
        using var coordinator = new SamplingCoordinator(Array.Empty<IMetricCollector>())
        {
            IntervalMs = 10
        };
        var updateReported = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var updateCount = 0;
        coordinator.MetricsUpdated += _ =>
        {
            if (Interlocked.Increment(ref updateCount) == 1)
            {
                throw new InvalidOperationException("subscriber failed");
            }

            updateReported.TrySetResult();
        };

        coordinator.Start();
        await updateReported.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Stop();

        Assert.True(updateCount >= 2);
    }

    [Fact]
    public void Coordinator_DisposesCollectors_OnlyOnce()
    {
        var collector = new Mock<IMetricCollector>();
        using var coordinator = new SamplingCoordinator(new[] { collector.Object });

        coordinator.Dispose();
        coordinator.Dispose();

        collector.Verify(c => c.Dispose(), Times.Once);
    }
}
