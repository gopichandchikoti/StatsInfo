using System.IO;
using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class MetricRuntimeTests
{
    [Fact]
    public async Task Runtime_TimesOutWithoutOverlappingReads_AndReleasesSourceOnShutdown()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(Path.Combine(dir, "settings.json"));
        var source = new SlowSource();
        using var runtime = new MetricRuntime(settings, [source]);
        runtime.Start();
        await Task.Delay(5300);
        Assert.Equal(1, source.Calls);
        Assert.Contains("timeout", runtime.Snapshot()[source.MetricIds[0]].Reason);
        Assert.Equal(SampleQuality.Unavailable, runtime.Snapshot()["cpu.power.package"].Quality);
        runtime.Dispose();
        source.Completion.TrySetResult([MetricSample.Number(source.MetricIds[0], 30, "test")]);
        await Task.Delay(150);
        Assert.True(source.Disposed);
        Directory.Delete(dir, true);
    }

    private sealed class SlowSource : IMetricSource
    {
        public readonly TaskCompletionSource<IReadOnlyList<MetricSample>> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public bool Disposed;
        public string Category => "CPU";
        public IReadOnlyList<string> MetricIds => ["cpu.usage.total", "cpu.power.package"];
        public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
            CancellationToken token)
        { Interlocked.Increment(ref Calls); return Completion.Task; }
        public void Dispose() => Disposed = true;
    }
    [Fact]
    public async Task Runtime_HiddenMetricDoesNotPoll_AlertDependencyDoesPoll_AndPauseAndShutdownCancel()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(Path.Combine(dir, "settings.json"));
        var source = new TestSource("cpu.power.package");
        using var runtime = new MetricRuntime(settings, [source]);
        runtime.Start(); runtime.Start();
        await Task.Delay(550);
        Assert.Equal(0, source.Reads);
        settings.Settings.Alerts.Add(new()
        {
            MetricId = source.Id,
            Enabled = true,
            Warning = 10,
            Critical = 20,
            Clear = 5,
            SustainedSeconds = 1,
            CooldownSeconds = 1
        });
        settings.Settings.UpdateIntervalMs = 500;
        var alert = new TaskCompletionSource<AlertEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.AlertRaised += e => alert.TrySetResult(e);
        await alert.Task.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.False(settings.Settings.Metrics[source.Id].Visible);
        Assert.True(runtime.ActiveAlerts.ContainsKey(source.Id));
        Assert.NotEmpty(runtime.History.Get(source.Id));
        Assert.True(File.Exists(Path.Combine(dir, "alerts.jsonl")));
        settings.Settings.Paused = true;
        await Task.Delay(600);
        int paused = source.Reads;
        await Task.Delay(600);
        Assert.Equal(paused, source.Reads);
        runtime.ClearHistory();
        Assert.Empty(runtime.History.GetAll());
        runtime.Dispose(); runtime.Dispose();
        await Task.Delay(100);
        Assert.True(source.Disposed);
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task Runtime_ExposesPermissionErrorsAndStaleSamples_AndDetailsDependency()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(Path.Combine(dir, "settings.json"));
        settings.Settings.UpdateIntervalMs = 500;
        var source = new TestSource("cpu.power.package") { Error = new UnauthorizedAccessException() };
        using var runtime = new MetricRuntime(settings, [source]) { DiagnosticsOpen = true };
        runtime.Start();
        await Task.Delay(150);
        Assert.Equal(SampleQuality.PermissionDenied, runtime.Snapshot()[source.Id].Quality);
        source.Error = null;
        source.AgeSeconds = 120;
        await Task.Delay(700);
        Assert.Equal(SampleQuality.Stale, runtime.Snapshot()[source.Id].Quality);
        Assert.True(runtime.CollectorMilliseconds.ContainsKey("CPU"));
        Assert.Same(source, runtime.Source<TestSource>());
        runtime.Dispose();
        await Task.Delay(100);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Runtime_DisposesSourcesEvenIfNotStarted()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var source = new TestSource("cpu.usage.total");
        using var runtime = new MetricRuntime(new SettingsService(Path.Combine(dir, "settings.json")), [source]);
        runtime.Dispose();
        Assert.True(source.Disposed);
        Assert.Throws<ObjectDisposedException>(runtime.Start);
        Directory.Delete(dir, true);
    }

    private sealed class TestSource(string id) : IMetricSource
    {
        public string Id => id;
        public string Category => "CPU";
        public IReadOnlyList<string> MetricIds => [id];
        public int Reads;
        public bool Disposed;
        public Exception? Error;
        public int AgeSeconds;
        public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested, CancellationToken token)
        {
            Interlocked.Increment(ref Reads);
            if (Error != null) throw Error;
            IReadOnlyList<MetricSample> samples = [MetricSample.Number(id, 30, "test") with
                { SampledAt = DateTimeOffset.UtcNow.AddSeconds(-AgeSeconds) }];
            return Task.FromResult(samples);
        }
        public void Dispose() => Disposed = true;
    }
}
