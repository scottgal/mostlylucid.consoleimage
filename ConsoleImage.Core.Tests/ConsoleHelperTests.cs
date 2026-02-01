using System.Runtime.InteropServices;
using ConsoleImage.Player;

namespace ConsoleImage.Core.Tests;

public class ConsoleHelperTests
{
    [Fact]
    public void EnableAnsiSupport_CanBeCalledMultipleTimes()
    {
        // Should not throw when called multiple times
        var result1 = ConsoleHelper.EnableAnsiSupport();
        var result2 = ConsoleHelper.EnableAnsiSupport();

        // Both calls should return the same value
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void IsAnsiSupported_ReturnsConsistentValue()
    {
        var value1 = ConsoleHelper.IsAnsiSupported;
        var value2 = ConsoleHelper.IsAnsiSupported;

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void EnableAnsiSupport_OnNonWindows_ReturnsTrue()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Windows, ANSI support depends on console mode settings
            // Just ensure it doesn't throw
            var result = ConsoleHelper.EnableAnsiSupport();
            Assert.True(result || !result); // Always passes - just checking no exception
        }
        else
        {
            // On non-Windows, ANSI is always assumed supported
            var result = ConsoleHelper.EnableAnsiSupport();
            Assert.True(result);
        }
    }

    [Fact]
    public void IsAnsiSupported_ReturnsBool()
    {
        // Should not throw and should return a valid boolean
        var supported = ConsoleHelper.IsAnsiSupported;
        Assert.True(supported || !supported); // Always passes - verify no exception
    }
}