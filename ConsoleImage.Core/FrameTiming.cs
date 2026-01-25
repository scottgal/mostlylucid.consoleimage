// FrameTiming - Shared adaptive frame timing for all animation players.
// Compensates for render time and drops frames when playback falls behind.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ConsoleImage.Core;

/// <summary>
///     Adaptive frame timing utilities for animation playback.
///     Provides render-time compensation and frame skipping to maintain correct playback speed.
/// </summary>
public static class FrameTiming
{
    /// <summary>
    ///     Calculate the remaining delay after compensating for render time and accumulated debt.
    ///     Returns the remaining delay to wait and the updated time debt.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int remainingDelayMs, double newDebtMs) CalculateAdaptiveDelay(
        double targetDelayMs,
        long renderStartTimestamp,
        double currentDebtMs)
    {
        var renderTimeMs = Stopwatch.GetElapsedTime(renderStartTimestamp).TotalMilliseconds;
        var remaining = targetDelayMs - renderTimeMs - currentDebtMs;

        if (remaining > 0)
            return ((int)remaining, 0);

        // Behind schedule - accumulate debt for next frame
        return (0, -remaining);
    }

    /// <summary>
    ///     Check if a frame should be skipped due to accumulated time debt.
    ///     When behind by more than one frame, skipping keeps playback on schedule.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldSkipFrame(double targetDelayMs, ref double timeDebtMs)
    {
        if (timeDebtMs > targetDelayMs)
        {
            timeDebtMs -= targetDelayMs;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Delay with responsive cancellation (checks every 50ms).
    ///     Common base delay used by all animation players.
    /// </summary>
    public static async Task ResponsiveDelayAsync(int totalMs, CancellationToken ct)
    {
        const int chunkMs = 50;
        var remaining = totalMs;

        while (remaining > 0 && !ct.IsCancellationRequested)
        {
            var delay = Math.Min(remaining, chunkMs);
            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            remaining -= delay;
        }
    }
}
