// Document file handling for consoleimage CLI

using ConsoleImage.Core;

namespace ConsoleImage.Cli.Handlers;

/// <summary>
/// Handles cidz/json document playback.
/// </summary>
public static class DocumentHandler
{
    /// <summary>
    /// Handle JSON/cidz document playback and conversion.
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
            {
                return await ConvertToGif(doc, outputGif, effectiveSpeed, effectiveLoop,
                    gifFontSize, gifScale, gifColors, ct);
            }

            // Play the document
            using var player = new DocumentPlayer(doc, effectiveSpeed, effectiveLoop);

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
