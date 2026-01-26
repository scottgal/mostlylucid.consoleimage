namespace ConsoleImage.Core.Subtitles;

/// <summary>
///     Splits long subtitle segments into readable chunks based on sentence boundaries
///     and reading speed. Standard subtitle guidelines: max 2 lines × 42 chars,
///     comfortable reading at ~20 characters/second.
/// </summary>
public static class SubtitleSplitter
{
    /// <summary>Maximum characters per subtitle entry (2 lines × 42 chars).</summary>
    private const int MaxCharsPerEntry = 84;

    /// <summary>Maximum duration for a single subtitle entry in seconds.</summary>
    private const double MaxDurationSeconds = 8.0;

    /// <summary>Minimum duration for a split subtitle entry in seconds.</summary>
    private const double MinDurationSeconds = 1.2;

    /// <summary>
    ///     Split a long subtitle entry into multiple shorter entries if needed.
    ///     Returns the original entry (with Index assigned) if no splitting is needed.
    ///     Assigns sequential Index values using the provided counter.
    /// </summary>
    /// <param name="entry">The entry to split (Index field is ignored/overwritten).</param>
    /// <param name="entryIndex">Running counter for sequential subtitle indices.</param>
    /// <returns>One or more subtitle entries with sequential indices.</returns>
    public static List<SubtitleEntry> Split(SubtitleEntry entry, ref int entryIndex)
    {
        var text = entry.Text;
        var duration = entry.Duration.TotalSeconds;

        // No splitting needed for short entries
        if (text.Length <= MaxCharsPerEntry && duration <= MaxDurationSeconds)
        {
            entry.Index = ++entryIndex;
            return [entry];
        }

        // Split text into sentences first
        var sentences = SplitIntoSentences(text);

        // If we only got one "sentence" that's still too long, split at clause boundaries
        if (sentences.Count == 1 && sentences[0].Length > MaxCharsPerEntry)
            sentences = SplitAtClauseBoundaries(sentences[0]);

        // Group sentences into chunks that fit within MaxCharsPerEntry
        var chunks = GroupIntoChunks(sentences);

        // If grouping produced only one chunk, no point splitting
        if (chunks.Count <= 1)
        {
            entry.Index = ++entryIndex;
            return [entry];
        }

        // Distribute timing proportionally by character count
        var totalChars = 0;
        foreach (var c in chunks)
            totalChars += c.Length;

        if (totalChars == 0)
        {
            entry.Index = ++entryIndex;
            return [entry];
        }

        var result = new List<SubtitleEntry>(chunks.Count);
        var currentStart = entry.StartTime.TotalSeconds;
        var totalDuration = duration;
        var originalEnd = entry.EndTime.TotalSeconds;

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var proportion = (double)chunk.Length / totalChars;
            var chunkDuration = totalDuration * proportion;

            // Enforce minimum duration (but don't exceed original end)
            if (chunkDuration < MinDurationSeconds)
                chunkDuration = MinDurationSeconds;

            var chunkEnd = currentStart + chunkDuration;

            // Last chunk always reaches the original end time
            if (i == chunks.Count - 1)
                chunkEnd = originalEnd;
            else if (chunkEnd > originalEnd)
                chunkEnd = originalEnd;

            // Skip zero or negative duration entries (can happen when min-duration
            // clamp causes earlier chunks to consume all available time)
            if (chunkEnd <= currentStart)
                break;

            result.Add(new SubtitleEntry
            {
                Index = ++entryIndex,
                StartTime = TimeSpan.FromSeconds(currentStart),
                EndTime = TimeSpan.FromSeconds(chunkEnd),
                Text = chunk,
                SpeakerId = entry.SpeakerId
            });

            currentStart = chunkEnd;

            // If we've reached the end, stop creating entries
            if (currentStart >= originalEnd)
                break;
        }

        return result;
    }

    /// <summary>
    ///     Split text at sentence boundaries (., !, ?).
    /// </summary>
    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var current = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch != '.' && ch != '!' && ch != '?')
                continue;

            // Check for ellipsis (...) — treat as single punctuation
            if (ch == '.' && i + 2 < text.Length && text[i + 1] == '.' && text[i + 2] == '.') i += 2; // Skip past ...

            // Sentence end: punctuation followed by space or end of text
            if (i + 1 >= text.Length || text[i + 1] == ' ')
            {
                var sentence = text[current..(i + 1)].Trim();
                if (sentence.Length > 0)
                    sentences.Add(sentence);
                current = i + 1;
            }
        }

        // Remaining text after last sentence boundary
        if (current < text.Length)
        {
            var remaining = text[current..].Trim();
            if (remaining.Length > 0)
                sentences.Add(remaining);
        }

        return sentences.Count > 0 ? sentences : [text];
    }

    /// <summary>
    ///     Split text at clause boundaries (commas, semicolons, dashes, ellipsis)
    ///     when no sentence boundaries are found. Only splits if the piece before
    ///     the boundary is at least 20 characters to avoid tiny fragments.
    /// </summary>
    private static List<string> SplitAtClauseBoundaries(string text)
    {
        var clauses = new List<string>();
        var current = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var isBoundary = false;
            var boundaryEnd = i + 1;

            if (text[i] == ',' || text[i] == ';')
            {
                isBoundary = true;
            }
            else if (text[i] == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                isBoundary = true;
                boundaryEnd = i + 2;
            }
            else if (text[i] == '.' && i + 2 < text.Length && text[i + 1] == '.' && text[i + 2] == '.')
            {
                isBoundary = true;
                boundaryEnd = i + 3;
            }

            // Only split if we have a reasonable amount of text
            if (isBoundary && i - current >= 20)
            {
                var clause = text[current..boundaryEnd].Trim();
                if (clause.Length > 0)
                    clauses.Add(clause);
                current = boundaryEnd;
            }
        }

        // Remaining text
        if (current < text.Length)
        {
            var remaining = text[current..].Trim();
            if (remaining.Length > 0)
                clauses.Add(remaining);
        }

        return clauses.Count > 1 ? clauses : [text];
    }

    /// <summary>
    ///     Group text pieces into chunks that fit within MaxCharsPerEntry.
    ///     Combines short sentences into single entries when they fit.
    /// </summary>
    private static List<string> GroupIntoChunks(List<string> pieces)
    {
        var chunks = new List<string>();
        var current = "";

        foreach (var piece in pieces)
            if (current.Length == 0)
            {
                current = piece;
            }
            else if (current.Length + 1 + piece.Length <= MaxCharsPerEntry)
            {
                current += " " + piece;
            }
            else
            {
                chunks.Add(current);
                current = piece;
            }

        if (current.Length > 0)
            chunks.Add(current);

        return chunks;
    }
}