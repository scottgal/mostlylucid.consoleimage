// Document file handling for consoleimage CLI

using ConsoleImage.Core;
using ConsoleImage.Core.Subtitles;

namespace ConsoleImage.Cli.Handlers;

/// <summary>
///     Handles cidz/json document playback.
/// </summary>
public static class DocumentHandler
{
    /// <summary>
    ///     Handle JSON/cidz document playback and conversion.
    /// </summary>
    public static async Task<int> HandleAsync(
        string path,
        float speed,
        int loop,
        FileInfo? outputGif,
        int gifFontSize,
        float gifScale,
        int gifColors,
        CancellationToken ct)
    {
        try
        {
            Console.Error.WriteLine($"Loading document: {path}");
            var doc = await ConsoleImageDocument.LoadAsync(path, ct);

            // Display document info
            Console.Error.WriteLine($"Loaded: {doc.RenderMode} mode, {doc.FrameCount} frame(s)");
            if (doc.IsAnimated) Console.Error.WriteLine($"Duration: {doc.TotalDurationMs}ms");

            // Override settings if specified
            var effectiveSpeed = speed != 1.0f ? speed : doc.Settings.AnimationSpeedMultiplier;
            var effectiveLoop = loop != 1 ? loop : doc.Settings.LoopCount;

            // GIF output mode - convert document to GIF
            if (outputGif != null)
                return await ConvertToGif(doc, outputGif, effectiveSpeed, effectiveLoop,
                    gifFontSize, gifScale, gifColors, ct);

            // Auto-detect sidecar subtitle file for playback
            SubtitleTrack? subtitles = null;
            if (doc.Settings.SubtitlesEnabled || !string.IsNullOrEmpty(doc.Settings.SubtitleFile))
                subtitles = await FindSidecarSubtitles(path, doc.Settings, ct);

            // Play the document
            using var player = new DocumentPlayer(doc, effectiveSpeed, effectiveLoop, subtitles);

            if (doc.IsAnimated)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                await player.PlayAsync(cts.Token);
            }
            else
            {
                player.Display();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading document: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    ///     Find sidecar subtitle file (.vtt/.srt) alongside a document file.
    ///     Checks: 1) Explicit SubtitleFile from settings, 2) Same-name .vtt, 3) Same-name .srt
    /// </summary>
    private static async Task<SubtitleTrack?> FindSidecarSubtitles(
        string documentPath, DocumentRenderSettings settings, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(documentPath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(documentPath);

        // Strip double extensions like .cid from .cid.7z
        if (baseName.EndsWith(".cid", StringComparison.OrdinalIgnoreCase))
            baseName = baseName[..^4];

        // 1) Check explicit subtitle filename from document metadata
        if (!string.IsNullOrEmpty(settings.SubtitleFile))
        {
            var explicitPath = Path.Combine(dir, settings.SubtitleFile);
            if (File.Exists(explicitPath))
            {
                Console.Error.WriteLine($"Loading subtitles: {settings.SubtitleFile}");
                return await SubtitleParser.ParseAsync(explicitPath, ct);
            }
        }

        // 2) Check for same-name .vtt
        var vttPath = Path.Combine(dir, baseName + ".vtt");
        if (File.Exists(vttPath))
        {
            Console.Error.WriteLine($"Found sidecar subtitles: {Path.GetFileName(vttPath)}");
            return await SubtitleParser.ParseAsync(vttPath, ct);
        }

        // 3) Check for same-name .srt
        var srtPath = Path.Combine(dir, baseName + ".srt");
        if (File.Exists(srtPath))
        {
            Console.Error.WriteLine($"Found sidecar subtitles: {Path.GetFileName(srtPath)}");
            return await SubtitleParser.ParseAsync(srtPath, ct);
        }

        return null;
    }

    private static async Task<int> ConvertToGif(
        ConsoleImageDocument doc,
        FileInfo outputGif,
        float effectiveSpeed,
        int effectiveLoop,
        int gifFontSize,
        float gifScale,
        int gifColors,
        CancellationToken ct)
    {
        Console.Error.WriteLine($"Converting to GIF: {outputGif.FullName}");

        // GIF loop: 0 = infinite
        var gifLoopCount = effectiveLoop != 1 ? effectiveLoop : 0;

        var gifOptions = new GifWriterOptions
        {
            FontSize = gifFontSize,
            Scale = gifScale,
            MaxColors = Math.Clamp(gifColors, 16, 256),
            LoopCount = gifLoopCount
        };
        using var gifWriter = new GifWriter(gifOptions);

        var frameIndex = 0;
        foreach (var frame in doc.Frames)
        {
            var delayMs = (int)(frame.DelayMs / effectiveSpeed);
            gifWriter.AddFrame(frame.Content, delayMs);
            frameIndex++;
            Console.Write($"\rConverting frames: {frameIndex}/{doc.FrameCount}");
        }

        Console.WriteLine();

        await gifWriter.SaveAsync(outputGif.FullName, ct);
        Console.Error.WriteLine($"Saved to {outputGif.FullName}");
        return 0;
    }
}