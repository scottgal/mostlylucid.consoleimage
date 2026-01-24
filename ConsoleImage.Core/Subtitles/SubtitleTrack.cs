namespace ConsoleImage.Core.Subtitles;

/// <summary>
/// Represents a complete subtitle track with multiple entries.
/// </summary>
public class SubtitleTrack
{
    /// <summary>
    /// List of subtitle entries sorted by start time.
    /// </summary>
    public List<SubtitleEntry> Entries { get; set; } = new();

    /// <summary>
    /// Language code (e.g., "en", "es", "jp").
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Original source file path.
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    /// Total number of subtitle entries.
    /// </summary>
    public int Count => Entries.Count;

    /// <summary>
    /// Check if the track has any entries.
    /// </summary>
    public bool HasEntries => Entries.Count > 0;

    /// <summary>
    /// Get the subtitle active at the given timestamp.
    /// </summary>
    /// <param name="timestamp">The timestamp to check.</param>
    /// <returns>The active subtitle entry, or null if none.</returns>
    public SubtitleEntry? GetActiveAt(TimeSpan timestamp)
    {
        // Binary search for efficiency with large subtitle files
        var low = 0;
        var high = Entries.Count - 1;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            var entry = Entries[mid];

            if (entry.IsActiveAt(timestamp))
                return entry;

            if (timestamp < entry.StartTime)
                high = mid - 1;
            else
                low = mid + 1;
        }

        return null;
    }

    /// <summary>
    /// Get the subtitle active at the given timestamp in seconds.
    /// </summary>
    /// <param name="seconds">The timestamp in seconds.</param>
    /// <returns>The active subtitle entry, or null if none.</returns>
    public SubtitleEntry? GetActiveAt(double seconds) => GetActiveAt(TimeSpan.FromSeconds(seconds));

    /// <summary>
    /// Get formatted display lines for the subtitle at the given timestamp.
    /// </summary>
    /// <param name="timestamp">The timestamp to check.</param>
    /// <param name="maxWidth">Maximum width in characters for wrapping.</param>
    /// <param name="maxLines">Maximum number of lines to return (default: 2).</param>
    /// <returns>Array of lines to display, or empty array if no subtitle active.</returns>
    public string[] GetDisplayLines(TimeSpan timestamp, int maxWidth, int maxLines = 2)
    {
        var entry = GetActiveAt(timestamp);
        if (entry == null)
            return Array.Empty<string>();

        return FormatForDisplay(entry.Text, maxWidth, maxLines);
    }

    /// <summary>
    /// Get formatted display lines for the subtitle at the given timestamp in seconds.
    /// </summary>
    /// <param name="seconds">The timestamp in seconds.</param>
    /// <param name="maxWidth">Maximum width in characters for wrapping.</param>
    /// <param name="maxLines">Maximum number of lines to return (default: 2).</param>
    /// <returns>Array of lines to display, or empty array if no subtitle active.</returns>
    public string[] GetDisplayLines(double seconds, int maxWidth, int maxLines = 2)
        => GetDisplayLines(TimeSpan.FromSeconds(seconds), maxWidth, maxLines);

    /// <summary>
    /// Format subtitle text for display with word wrapping and line limits.
    /// </summary>
    private static string[] FormatForDisplay(string text, int maxWidth, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        // Split into source lines first
        var sourceLines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        foreach (var sourceLine in sourceLines)
        {
            if (result.Count >= maxLines)
                break;

            var trimmed = sourceLine.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Word wrap if needed
            if (trimmed.Length <= maxWidth)
            {
                result.Add(trimmed);
            }
            else
            {
                // Simple word wrap
                var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var currentLine = "";

                foreach (var word in words)
                {
                    if (result.Count >= maxLines)
                        break;

                    if (string.IsNullOrEmpty(currentLine))
                    {
                        currentLine = word;
                    }
                    else if (currentLine.Length + 1 + word.Length <= maxWidth)
                    {
                        currentLine += " " + word;
                    }
                    else
                    {
                        result.Add(currentLine);
                        currentLine = word;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine) && result.Count < maxLines)
                {
                    result.Add(currentLine);
                }
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Add a subtitle entry, maintaining sort order by start time.
    /// </summary>
    public void AddEntry(SubtitleEntry entry)
    {
        // Find insertion point to maintain sorted order
        var index = Entries.BinarySearch(entry, Comparer<SubtitleEntry>.Create(
            (a, b) => a.StartTime.CompareTo(b.StartTime)));

        if (index < 0)
            index = ~index;

        Entries.Insert(index, entry);
    }

    /// <summary>
    /// Get total duration from first subtitle start to last subtitle end.
    /// </summary>
    public TimeSpan GetTotalDuration()
    {
        if (Entries.Count == 0)
            return TimeSpan.Zero;

        var start = Entries.Min(e => e.StartTime);
        var end = Entries.Max(e => e.EndTime);
        return end - start;
    }
}
