// ConsolePlayer - Plays PlayerDocument frames to the console
// Zero dependencies beyond .NET runtime

namespace ConsoleImage.Player;

/// <summary>
///     Plays ConsoleImage documents directly to the console.
///     Supports smooth animation with DECSET 2026 synchronized output.
/// </summary>
public class ConsolePlayer : IDisposable
{
    private readonly PlayerDocument _document;
    private readonly int _loopCount;
    private readonly float _speedMultiplier;
    private bool _disposed;

    /// <summary>
    ///     Create a player for the given document.
    /// </summary>
    /// <param name="document">The document to play</param>
    /// <param name="speedMultiplier">Speed override (null = use document settings)</param>
    /// <param name="loopCount">Loop count override (null = use document settings, 0 = infinite)</param>
    public ConsolePlayer(
        PlayerDocument document,
        float? speedMultiplier = null,
        int? loopCount = null)
    {
        _document = document;
        _speedMultiplier = speedMultiplier ?? document.Settings.AnimationSpeedMultiplier;
        _loopCount = loopCount ?? document.Settings.LoopCount;
    }

    /// <summary>
    ///     Event raised before each frame is displayed
    /// </summary>
    public event Action<int, int>? OnFrameChanged;

    /// <summary>
    ///     Event raised when a loop completes
    /// </summary>
    public event Action<int>? OnLoopComplete;

    /// <summary>
    ///     The document being played
    /// </summary>
    public PlayerDocument Document => _document;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Play the document asynchronously with animation support.
    /// </summary>
    public async Task PlayAsync(CancellationToken ct = default)
    {
        if (_document.Frames.Count == 0)
            return;

        // Single frame - just display it
        if (!_document.IsAnimated)
        {
            Console.Write(_document.Frames[0].Content);
            return;
        }

        var maxHeight = _document.Frames.Max(f => f.Height);

        // Hide cursor during animation
        Console.Write("\x1b[?25l");

        try
        {
            var loopsRemaining = _loopCount == 0 ? int.MaxValue : _loopCount;
            var currentLoop = 0;

            while (loopsRemaining > 0 && !ct.IsCancellationRequested)
            {
                for (var i = 0; i < _document.Frames.Count && !ct.IsCancellationRequested; i++)
                {
                    var frame = _document.Frames[i];

                    OnFrameChanged?.Invoke(i, _document.Frames.Count);

                    // Move cursor to start (except first frame of first loop)
                    if (i > 0 || currentLoop > 0)
                        Console.Write($"\x1b[{maxHeight}A\r");

                    // Synchronized output for flicker-free rendering
                    Console.Write("\x1b[?2026h");
                    Console.Write(frame.Content);
                    Console.Write("\x1b[?2026l");
                    Console.Out.Flush();

                    // Frame delay
                    if (frame.DelayMs > 0)
                    {
                        var delay = (int)(frame.DelayMs / _speedMultiplier);
                        await Task.Delay(delay, ct);
                    }
                }

                currentLoop++;
                OnLoopComplete?.Invoke(currentLoop);

                if (_loopCount != 0)
                    loopsRemaining--;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        finally
        {
            // Restore cursor and reset colors
            Console.Write("\x1b[?25h");
            Console.Write("\x1b[0m");
        }
    }

    /// <summary>
    ///     Display a single frame (or all frames without animation).
    /// </summary>
    public void Display(bool showAllFrames = false)
    {
        if (_document.Frames.Count == 0)
            return;

        if (!showAllFrames || !_document.IsAnimated)
        {
            Console.Write(_document.Frames[0].Content);
            return;
        }

        foreach (var frame in _document.Frames)
        {
            Console.Write(frame.Content);
            Console.WriteLine();
            Console.WriteLine($"--- Frame (delay: {frame.DelayMs}ms) ---");
        }
    }

    /// <summary>
    ///     Get document info as a string.
    /// </summary>
    public string GetInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"Version: {_document.Version}");
        info.AppendLine($"Created: {_document.Created:O}");
        if (!string.IsNullOrEmpty(_document.SourceFile))
            info.AppendLine($"Source: {_document.SourceFile}");
        info.AppendLine($"Render Mode: {_document.RenderMode}");
        info.AppendLine($"Frames: {_document.FrameCount}");
        if (_document.IsAnimated)
        {
            info.AppendLine($"Duration: {_document.TotalDurationMs}ms");
            info.AppendLine($"Speed: {_document.Settings.AnimationSpeedMultiplier}x");
            info.AppendLine($"Loop Count: {(_document.Settings.LoopCount == 0 ? "infinite" : _document.Settings.LoopCount.ToString())}");
        }

        info.AppendLine($"Size: {_document.Settings.MaxWidth}x{_document.Settings.MaxHeight}");
        info.AppendLine($"Color: {(_document.Settings.UseColor ? "yes" : "no")}");

        return info.ToString();
    }

    /// <summary>
    ///     Create a player directly from a JSON file.
    /// </summary>
    public static async Task<ConsolePlayer> FromFileAsync(
        string path,
        float? speedMultiplier = null,
        int? loopCount = null,
        CancellationToken ct = default)
    {
        var doc = await PlayerDocument.LoadAsync(path, ct);
        return new ConsolePlayer(doc, speedMultiplier, loopCount);
    }

    /// <summary>
    ///     Create a player directly from a JSON string.
    /// </summary>
    public static ConsolePlayer FromJson(
        string json,
        float? speedMultiplier = null,
        int? loopCount = null)
    {
        var doc = PlayerDocument.FromJson(json);
        return new ConsolePlayer(doc, speedMultiplier, loopCount);
    }
}
