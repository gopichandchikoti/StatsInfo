using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class FrameWindowTests
{
    [Fact]
    public void FrameStatistics_RequireSufficientSamples_AndEvictOldFrames()
    {
        var frames = new FrameWindow();
        frames.Add(0, 10);
        Assert.Equal(100, frames.Statistics().Fps);
        Assert.Null(frames.Statistics().Low1);
        for (int i = 1; i < 1000; i++) frames.Add(i / 100.0, i == 999 ? 100 : 10);
        Assert.Equal(1000 / 19.0, frames.Statistics().Low1);
        Assert.Equal(10, frames.Statistics().Low01);
        frames.Add(100, 20);
        Assert.Equal(1, frames.Count);
        Assert.Equal(50, frames.Statistics().Fps);
    }

    [Fact]
    public void CsvReader_FiltersProcessAndSupportsQuotedNames_WithoutGuessingColumns()
    {
        var header = FrameWindow.ParseCsv("Application,ProcessID,SwapChainAddress,MsBetweenPresents");
        var row = FrameWindow.ParseCsv("\"a,b.exe\",123,0x1,16.5");
        Assert.True(FrameWindow.TryInterval(header, row, 123, out var swap, out var ms));
        Assert.Equal("0x1", swap);
        Assert.Equal(16.5, ms);
        Assert.False(FrameWindow.TryInterval(header, row, 999, out _, out _));
        Assert.False(FrameWindow.TryInterval(header, ["a", "123"], 123, out _, out _));
        Assert.False(FrameWindow.TryInterval(header, ["a", "123", "0x1", "NaN"], 123, out _, out _));
    }
}
