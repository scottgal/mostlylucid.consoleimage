// LineUtils - Shared zero-allocation line parsing utilities.
// Uses pre-computed offset arrays for O(1) per-line access instead of O(N) scanning.

using System.Runtime.CompilerServices;

namespace ConsoleImage.Core;

/// <summary>
///     Zero-allocation line parsing utilities using pre-computed offset arrays.
///     Provides O(N) total line access instead of O(N²) for repeated GetLineSpan calls.
/// </summary>
internal static class LineUtils
{
    /// <summary>
    ///     Build an array of line start positions in a single O(N) scan.
    ///     Returns the number of lines found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BuildLineStarts(string content, Span<int> starts)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        int count = 0;
        starts[count++] = 0;

        for (int i = 0; i < content.Length && count < starts.Length; i++)
        {
            if (content[i] == '\n')
                starts[count++] = i + 1;
        }

        return count;
    }

    /// <summary>
    ///     Get a line span from pre-computed start positions. O(1) per line.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> GetLineFromStarts(string content, Span<int> starts, int lineCount, int lineIndex)
    {
        if (lineIndex >= lineCount)
            return ReadOnlySpan<char>.Empty;

        int start = starts[lineIndex];
        int end;

        if (lineIndex + 1 < lineCount)
        {
            end = starts[lineIndex + 1] - 1; // Position of \n
            if (end > start && content[end - 1] == '\r') end--;
        }
        else
        {
            end = content.Length;
            if (end > start && content[end - 1] == '\r') end--;
        }

        return content.AsSpan(start, end - start);
    }
}
