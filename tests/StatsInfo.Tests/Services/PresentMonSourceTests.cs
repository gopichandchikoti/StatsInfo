using System.Diagnostics;
using System.IO;
using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class PresentMonSourceTests
{
    [Fact]
    public async Task Adapter_RequiresExplicitValidTarget_ParsesOnlySelectedSwapChain_AndCleansUp()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        using var source = new PresentMonSource();
        Process? target = null;
        try
        {
            var requested = source.MetricIds.ToHashSet();
            Assert.False(source.IsCapturing);
            Assert.All(await source.ReadAsync(new(), requested, default), m => Assert.Equal(SampleQuality.Unavailable, m.Quality));
            Assert.Throws<ArgumentException>(() => source.Start("nonexistent.exe", 123, 10));
            string exe = Path.Combine(dir, "PresentMonFixture.exe");
            string compiler = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe");
            var compileInfo = new ProcessStartInfo(compiler)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            compileInfo.ArgumentList.Add("/nologo"); compileInfo.ArgumentList.Add("/out:" + exe);
            compileInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PresentMonFixture.cs"));
            using var compile = Process.Start(compileInfo)!;
            string output = await compile.StandardOutput.ReadToEndAsync();
            await compile.WaitForExitAsync();
            Assert.True(compile.ExitCode == 0, output);
            Assert.Throws<ArgumentException>(() => source.Start(exe, Environment.ProcessId, 10));
            target = Process.Start(new ProcessStartInfo(exe, "--wait") { UseShellExecute = false, CreateNoWindow = true })!;
            source.Start(exe, target.Id, 10);
            Assert.True(source.IsCapturing);
            IReadOnlyList<MetricSample> samples = [];
            for (int i = 0; i < 30; i++)
            {
                samples = await source.ReadAsync(new(), requested, default);
                if (samples.Any(s => s.Id == "game.fps.low01" && s.IsNumeric)) break;
                await Task.Delay(100);
            }
            Assert.Equal(100, samples.Single(m => m.Id == "game.fps").Value);
            Assert.Equal(10, samples.Single(m => m.Id == "game.frame_time").Value);
            Assert.Equal(100, samples.Single(m => m.Id == "game.fps.low01").Value);
            Assert.Equal(SampleQuality.Unavailable, samples.Single(m => m.Id == "game.render_gpu").Quality);
            Assert.Contains("0x1", source.Status);
            await source.ReadAsync(new() { Paused = true }, requested, default);
            Assert.False(source.IsCapturing);
            source.Stop();
            source.Start(exe, target.Id, 10);
            for (int i = 0; i < 150 && (source.IsCapturing || !source.Status.Contains("ended")); i++)
                await Task.Delay(100);
            Assert.False(source.IsCapturing);
            Assert.Contains("ended", source.Status);
        }
        finally
        {
            source.Dispose();
            if (target != null)
            {
                if (!target.HasExited) target.Kill();
                // Kill requests termination; Windows can still hold the executable until exit completes.
                await target.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                target.Dispose();
            }
            // Windows image mappings or a scanner can briefly retain a just-exited executable.
            // Keep cleanup mandatory, but allow that transient lock to clear on hosted runners.
            for (int attempt = 0; ; attempt++)
            {
                try { Directory.Delete(dir, true); break; }
                catch (Exception ex) when (attempt < 49 && ex is IOException or UnauthorizedAccessException)
                { await Task.Delay(100); }
            }
        }
    }
}
