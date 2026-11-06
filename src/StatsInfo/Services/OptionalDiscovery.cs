using System.ComponentModel;
using System.IO;
using System.Security;

namespace StatsInfo.Services;

/// <summary>Optional Windows hardware discovery must never prevent Settings from opening.</summary>
public static class OptionalDiscovery
{
    public static T[] Read<T>(Func<IEnumerable<T>> read, IEnumerable<T> fallback, Action<string> report)
    {
        try { return read().ToArray(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException
            or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            report($"Choices unavailable ({ex.GetType().Name}). Monitoring and other settings remain available.");
            return fallback.ToArray();
        }
    }
}
