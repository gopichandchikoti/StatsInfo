using System.ComponentModel;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class OptionalDiscoveryTests
{
    [Fact]
    public void MissingWindowsCounterCategory_RetainsFallback_AndExplainsFailure()
    {
        string? error = null;
        var choices = OptionalDiscovery.Read<string>(() => throw new Win32Exception("Counter unavailable"),
            ["Auto"], message => error = message);
        Assert.Equal(new[] { "Auto" }, choices);
        Assert.Contains("Win32Exception", error);
    }

    [Fact]
    public void SuccessfulEnumeration_RetainsHardwareChoices()
    {
        var choices = OptionalDiscovery.Read(() => new[] { "Disk 0", "Disk 1" }, ["Auto"], _ =>
            throw new InvalidOperationException("Successful discovery must not report an error"));
        Assert.Equal(new[] { "Disk 0", "Disk 1" }, choices);
    }
}
