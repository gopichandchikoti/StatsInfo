using System.IO;
using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class HistoryStoreRecoveryTests
{
    private static string NewDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public void Restore_ReportsACorruptSnapshot_InsteadOfThrowing()
    {
        string directory = NewDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "history"));
            File.WriteAllText(Path.Combine(directory, "history", "history-2026-01-01.json"), "{ this is not json");
            var store = new HistoryStore(Path.Combine(directory, "settings.json"));
            var restored = new MetricHistory();
            store.Restore(restored, new AppSettings { PersistHistory = true });
            Assert.Empty(restored.GetAll());
            Assert.Contains("History restore failed", store.Error);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Save_ReportsAnUnwritableHistoryFolder_AndKeepsRunning()
    {
        string directory = NewDirectory();
        try
        {
            // A file where the history folder should be makes the folder impossible to create.
            File.WriteAllText(Path.Combine(directory, "history"), "not a folder");
            var store = new HistoryStore(Path.Combine(directory, "settings.json"));
            var history = new MetricHistory();
            history.Add(MetricSample.Number("cpu.usage.total", 10, "test"));
            store.Save(history, new AppSettings { PersistHistory = true }, force: true);
            Assert.Contains("History persistence failed", store.Error);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Save_RemovesSnapshotsOlderThanTheRetentionPeriod_AndKeepsToday()
    {
        string directory = NewDirectory();
        try
        {
            string folder = Path.Combine(directory, "history");
            Directory.CreateDirectory(folder);
            string old = Path.Combine(folder, "history-2020-01-01.json");
            File.WriteAllText(old, "[]");
            File.SetLastWriteTime(old, DateTime.Now.AddDays(-30));
            var store = new HistoryStore(Path.Combine(directory, "settings.json"));
            var history = new MetricHistory();
            history.Add(MetricSample.Number("cpu.usage.total", 10, "test"));
            store.Save(history, new AppSettings { PersistHistory = true, HistoryRetentionDays = 7 }, force: true);
            Assert.Null(store.Error);
            Assert.False(File.Exists(old));
            Assert.Single(Directory.GetFiles(folder));
        }
        finally { Directory.Delete(directory, true); }
    }
}
