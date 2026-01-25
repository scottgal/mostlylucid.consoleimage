// Matrix Digital Rain Renderer
// Creates the iconic "Matrix" falling code effect, optionally overlaying source image data
// Based on the visual effect from The Matrix films

using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Core;

/// <summary>
///     Options specific to Matrix rendering mode.
/// </summary>
public class MatrixOptions
{
    /// <summary>
    ///     Base color for the Matrix effect. Default is classic Matrix green.
    ///     Set to null to use full-color mode (colors from source image).
    /// </summary>
    public Rgba32? BaseColor { get; set; } = new Rgba32(0, 255, 0, 255);

    /// <summary>
    ///     Use full color mode - takes colors from source image while applying Matrix lighting.
    ///     When true, BaseColor is ignored and source image colors are used.
    /// </summary>
    public bool UseFullColor { get; set; }

    /// <summary>
    ///     Rain density (0.0 - 1.0). Higher = more raindrops.
    /// </summary>
    public float Density { get; set; } = 0.5f;

    /// <summary>
    ///     Rain speed multiplier. 1.0 = normal, 2.0 = twice as fast.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1.0f;

    /// <summary>
    ///     Trail length multiplier. 1.0 = normal length.
    /// </summary>
    public float TrailLengthMultiplier { get; set; } = 1.0f;

    /// <summary>
    ///     Head brightness boost (1.0 - 2.0). Makes the leading character brighter.
    /// </summary>
    public float HeadBrightness { get; set; } = 1.5f;

    /// <summary>
    ///     Character change rate (0.0 - 1.0). How often characters flicker/change.
    /// </summary>
    public float CharacterFlickerRate { get; set; } = 0.05f;

    /// <summary>
    ///     Use ASCII characters only (no katakana). Useful when fonts don't support Japanese.
    /// </summary>
    public bool UseAsciiOnly { get; set; } = false;

    /// <summary>
    ///     Use block-based rendering (higher resolution, 2 pixels per character).
    /// </summary>
    public bool UseBlockMode { get; set; } = false;

    /// <summary>
    ///     Target frames per second for Matrix animation (default 20 FPS for smooth rain).
    /// </summary>
    public int TargetFps { get; set; } = 20;

    /// <summary>
    ///     Custom character set for the rain. If set, overrides UseAsciiOnly.
    ///     Can be a word, phrase, or any string of characters.
    /// </summary>
    public string? CustomAlphabet { get; set; }

    /// <summary>
    ///     Preset for classic Matrix green.
    /// </summary>
    public static MatrixOptions ClassicGreen => new()
    {
        BaseColor = new Rgba32(0, 255, 0, 255),
        UseFullColor = false
    };

    /// <summary>
    ///     Preset for Matrix Resurrections blue-green tint.
    /// </summary>
    public static MatrixOptions Resurrections => new()
    {
        BaseColor = new Rgba32(0, 200, 180, 255),
        UseFullColor = false
    };

    /// <summary>
    ///     Preset for full color mode - uses source image colors.
    /// </summary>
    public static MatrixOptions FullColor => new()
    {
        UseFullColor = true,
        BaseColor = null
    };

    /// <summary>
    ///     Preset for red pill mode.
    /// </summary>
    public static MatrixOptions RedPill => new()
    {
        BaseColor = new Rgba32(255, 50, 50, 255),
        UseFullColor = false
    };

    /// <summary>
    ///     Preset for blue pill mode.
    /// </summary>
    public static MatrixOptions BluePill => new()
    {
        BaseColor = new Rgba32(50, 100, 255, 255),
        UseFullColor = false
    };

    /// <summary>
    ///     Preset for amber/gold retro terminal style.
    /// </summary>
    public static MatrixOptions Amber => new()
    {
        BaseColor = new Rgba32(255, 176, 0, 255),
        UseFullColor = false
    };

    /// <summary>
    ///     Preset for purple/cyberpunk style.
    /// </summary>
    public static MatrixOptions Cyberpunk => new()
    {
        BaseColor = new Rgba32(180, 0, 255, 255),
        UseFullColor = false
    };

    // === Image Reveal Effects ===

    /// <summary>
    ///     Enable edge detection to make rain persist/accumulate at image edges.
    ///     This reveals the shape of the underlying image through the rain pattern.
    /// </summary>
    public bool EnableEdgeDetection { get; set; }

    /// <summary>
    ///     How strongly edges affect rain persistence (0.0 - 1.0).
    ///     Higher values make rain slow down more dramatically at edges.
    /// </summary>
    public float EdgePersistence { get; set; } = 0.7f;

    /// <summary>
    ///     Enable brightness-based phosphor persistence.
    ///     Brighter areas of the image glow longer, revealing the image shape.
    /// </summary>
    public bool EnableBrightnessPersistence { get; set; }

    /// <summary>
    ///     How strongly brightness affects persistence (0.0 - 1.0).
    /// </summary>
    public float BrightnessPersistence { get; set; } = 0.5f;

    /// <summary>
    ///     Minimum brightness for background characters to show (0.0 - 1.0).
    ///     Lower values show more of the image through the rain.
    /// </summary>
    public float BackgroundThreshold { get; set; } = 0.1f;

    /// <summary>
    ///     Background character brightness multiplier (0.0 - 1.0).
    ///     Higher values make the underlying image more visible.
    /// </summary>
    public float BackgroundBrightness { get; set; } = 0.25f;

    /// <summary>
    ///     Preset for image reveal mode - optimized to show underlying image clearly.
    ///     Uses edge detection and brightness persistence to make image discernible.
    ///     Rain characters "flash" brightly when crossing edges, revealing the shape.
    /// </summary>
    public static MatrixOptions ImageReveal => new()
    {
        BaseColor = new Rgba32(0, 255, 0, 255),
        EnableEdgeDetection = true,
        EdgePersistence = 0.85f,
        EnableBrightnessPersistence = true,
        BrightnessPersistence = 0.7f,
        BackgroundThreshold = 0.03f,
        BackgroundBrightness = 0.45f,
        Density = 0.5f,
        TrailLengthMultiplier = 1.3f
    };
}

/// <summary>
///     Renders images/videos with the Matrix digital rain effect.
///     Characters fall in columns with varying speeds, using customizable color palette
///     with bright head characters. Source image brightness influences rain appearance.
///     Supports single-color mode (classic green or custom) and full-color mode.
/// </summary>
public class MatrixRenderer : IDisposable
{
    // Matrix character sets - katakana version and ASCII fallback
    private static readonly char[] KatakanaCharacters = BuildKatakanaCharacterSet();
    private static readonly char[] AsciiCharacters = BuildAsciiCharacterSet();

    // Default background
    private static readonly Rgba32 BackgroundColor = new(0, 0, 0, 255);

    // Active character set (based on options)
    private readonly char[] _matrixCharacters;
    private readonly MatrixOptions _matrixOptions;
    private readonly RenderOptions _options;
    private readonly Random _random;
    private readonly MatrixRainState _state;
    private bool _disposed;

    public MatrixRenderer(RenderOptions? options = null, MatrixOptions? matrixOptions = null, int? seed = null)
    {
        _options = options ?? new RenderOptions();
        _matrixOptions = matrixOptions ?? new MatrixOptions();
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _state = new MatrixRainState();

        // Select character set based on options (priority: CustomAlphabet > UseAsciiOnly > default katakana)
        if (!string.IsNullOrEmpty(_matrixOptions.CustomAlphabet))
            // Use custom alphabet - deduplicate characters
            _matrixCharacters = _matrixOptions.CustomAlphabet.Distinct().ToArray();
        else
            _matrixCharacters = _matrixOptions.UseAsciiOnly ? AsciiCharacters : KatakanaCharacters;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static char[] BuildKatakanaCharacterSet()
    {
        var chars = new List<char>();

        // Half-width katakana (1 column wide in terminals, same as ASCII)
        // These are the iconic Matrix film characters: ｦ ｧ ｨ ｩ ｪ ｫ ｬ ｭ ｮ ｯ ｰ ｱ ｲ ｳ ｴ ｵ etc.
        for (var c = '\uFF66'; c <= '\uFF9D'; c++)
            chars.Add(c);

        // Numbers
        for (var c = '0'; c <= '9'; c++)
            chars.Add(c);

        // Some symbols used in the Matrix films
        chars.AddRange(new[] { ':', '.', '"', '=', '*', '+', '-', '<', '>', '|', '_', '\\', '/', '^' });

        // Some Latin letters (often mirrored/reversed in the film, but we use normal)
        chars.AddRange(new[] { 'Z', 'Y', 'X', 'W', 'V', 'U', 'T', 'S', 'R', 'Q', 'P', 'O', 'N', 'M' });

        return chars.ToArray();
    }

    private static char[] BuildAsciiCharacterSet()
    {
        var chars = new List<char>();

        // Numbers
        for (var c = '0'; c <= '9'; c++)
            chars.Add(c);

        // Uppercase letters (reversed style like in the film)
        chars.AddRange(new[]
        {
            'Z', 'Y', 'X', 'W', 'V', 'U', 'T', 'S', 'R', 'Q', 'P', 'O', 'N', 'M', 'L', 'K', 'J', 'I', 'H', 'G', 'F',
            'E', 'D', 'C', 'B', 'A'
        });

        // Lowercase letters
        chars.AddRange(new[]
        {
            'z', 'y', 'x', 'w', 'v', 'u', 't', 's', 'r', 'q', 'p', 'o', 'n', 'm', 'l', 'k', 'j', 'i', 'h', 'g', 'f',
            'e', 'd', 'c', 'b', 'a'
        });

        // Symbols commonly seen in code/Matrix style
        chars.AddRange(new[]
        {
            ':', '.', '"', '=', '*', '+', '-', '<', '>', '|', '_', '\\', '/', '^', '@', '#', '$', '%', '&', '!', '?',
            '~', ';', '{', '}', '[', ']', '(', ')'
        });

        return chars.ToArray();
    }

    /// <summary>
    ///     Render a single frame with Matrix effect applied to the source image.
    ///     The image brightness influences rain density and appearance.
    ///     In full-color mode, source colors are used with Matrix lighting.
    /// </summary>
    public MatrixFrame RenderImage(Image<Rgba32> image)
    {
        var (charWidth, charHeight) = CalculateMatrixDimensions(image.Width, image.Height);

        // Initialize state if needed
        if (!_state.IsInitialized || _state.Width != charWidth || _state.Height != charHeight)
            _state.Initialize(charWidth, charHeight, _random, _matrixOptions);

        // Resize image for sampling
        using var resized = image.Clone(ctx => ctx.Resize(charWidth, charHeight));

        // Sample brightness, colors, and edges from image
        var (brightness, colors, edges) = SampleImageData(resized);

        // Advance rain state for animation (drops move down each frame)
        // Pass edges so rain can slow down at edge locations
        _state.Advance(_random, _matrixOptions, edges);

        // Generate frame content with edge-aware rendering
        var content = RenderFrame(charWidth, charHeight, brightness, colors, edges);

        // Calculate delay from target FPS
        var delayMs = 1000 / Math.Max(1, _matrixOptions.TargetFps);
        return new MatrixFrame(content, delayMs);
    }

    /// <summary>
    ///     Render a file to a Matrix frame.
    /// </summary>
    public MatrixFrame RenderFile(string filePath)
    {
        using var image = Image.Load<Rgba32>(filePath);
        return RenderImage(image);
    }

    /// <summary>
    ///     Render a stream to a Matrix frame.
    /// </summary>
    public MatrixFrame RenderStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderImage(image);
    }

    /// <summary>
    ///     Render a GIF as Matrix animation frames.
    ///     Each source frame influences the rain, but the rain animation is continuous.
    /// </summary>
    public List<MatrixFrame> RenderGif(string filePath)
    {
        using var image = Image.Load<Rgba32>(filePath);
        return RenderGifInternal(image);
    }

    /// <summary>
    ///     Render a GIF stream as Matrix animation frames.
    /// </summary>
    public List<MatrixFrame> RenderGifStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderGifInternal(image);
    }

    /// <summary>
    ///     Render pure Matrix rain effect without any source image.
    ///     Just the iconic falling code effect.
    /// </summary>
    /// <param name="width">Character width (default: from RenderOptions or 80)</param>
    /// <param name="height">Character height (default: from RenderOptions or 40)</param>
    public MatrixFrame RenderPureRain(int? width = null, int? height = null)
    {
        var charWidth = width ?? _options.Width ?? _options.MaxWidth;
        var charHeight = height ?? _options.Height ?? _options.MaxHeight;

        // Initialize state if needed
        if (!_state.IsInitialized || _state.Width != charWidth || _state.Height != charHeight)
            _state.Initialize(charWidth, charHeight, _random, _matrixOptions);

        // No image data - use null brightness/colors (pure rain)
        _state.Advance(_random, _matrixOptions, null);

        // Generate frame with no image influence
        var content = RenderFrame(charWidth, charHeight, null, null, null);

        var delayMs = 1000 / Math.Max(1, _matrixOptions.TargetFps);
        return new MatrixFrame(content, delayMs);
    }

    /// <summary>
    ///     Generate continuous Matrix animation frames from a static image.
    ///     The rain keeps falling over the image indefinitely.
    /// </summary>
    /// <param name="image">Source image to overlay rain on</param>
    /// <param name="frameCount">Number of frames to generate</param>
    public IEnumerable<MatrixFrame> RenderContinuous(Image<Rgba32> image, int frameCount)
    {
        var (charWidth, charHeight) = CalculateMatrixDimensions(image.Width, image.Height);

        // Initialize state
        if (!_state.IsInitialized || _state.Width != charWidth || _state.Height != charHeight)
            _state.Initialize(charWidth, charHeight, _random, _matrixOptions);

        // Resize image once for sampling
        using var resized = image.Clone(ctx => ctx.Resize(charWidth, charHeight));
        var (brightness, colors, edges) = SampleImageData(resized);

        var delayMs = 1000 / Math.Max(1, _matrixOptions.TargetFps);

        for (var i = 0; i < frameCount; i++)
        {
            _state.Advance(_random, _matrixOptions, edges);
            var content = RenderFrame(charWidth, charHeight, brightness, colors, edges);
            yield return new MatrixFrame(content, delayMs);
        }
    }

    /// <summary>
    ///     Generate continuous pure Matrix rain frames (no image).
    /// </summary>
    /// <param name="frameCount">Number of frames to generate</param>
    /// <param name="width">Character width</param>
    /// <param name="height">Character height</param>
    public IEnumerable<MatrixFrame> RenderPureRainContinuous(int frameCount, int? width = null, int? height = null)
    {
        var charWidth = width ?? _options.Width ?? _options.MaxWidth;
        var charHeight = height ?? _options.Height ?? _options.MaxHeight;

        // Initialize state
        if (!_state.IsInitialized || _state.Width != charWidth || _state.Height != charHeight)
            _state.Initialize(charWidth, charHeight, _random, _matrixOptions);

        var delayMs = 1000 / Math.Max(1, _matrixOptions.TargetFps);

        for (var i = 0; i < frameCount; i++)
        {
            _state.Advance(_random, _matrixOptions, null);
            var content = RenderFrame(charWidth, charHeight, null, null, null);
            yield return new MatrixFrame(content, delayMs);
        }
    }

    private List<MatrixFrame> RenderGifInternal(Image<Rgba32> image)
    {
        var frames = new List<MatrixFrame>();
        var (charWidth, charHeight) = CalculateMatrixDimensions(image.Width, image.Height);

        // Initialize state
        _state.Initialize(charWidth, charHeight, _random, _matrixOptions);

        // For GIFs, we generate multiple rain frames per source frame for smoother animation
        var frameStep = Math.Max(1, _options.FrameSampleRate);

        for (var i = 0; i < image.Frames.Count; i += frameStep)
        {
            using var frameImage = image.Frames.CloneFrame(i);
            using var resized = frameImage.Clone(ctx => ctx.Resize(charWidth, charHeight));

            var (brightness, colors, edges) = SampleImageData(resized);

            // Get frame delay from GIF metadata
            var metadata = image.Frames[i].Metadata.GetGifMetadata();
            var sourceDelayMs = metadata.FrameDelay * 10;
            if (sourceDelayMs == 0) sourceDelayMs = 100;

            // Generate rain frames to fill the source frame duration
            // Use configured target FPS
            var targetRainDelayMs = 1000 / Math.Max(1, _matrixOptions.TargetFps);
            var rainFramesNeeded = Math.Max(1, sourceDelayMs / targetRainDelayMs);

            for (var r = 0; r < rainFramesNeeded; r++)
            {
                // Advance rain state (pass edges for persistence behavior)
                _state.Advance(_random, _matrixOptions, edges);

                // Render frame
                var content = RenderFrame(charWidth, charHeight, brightness, colors, edges);
                var delayMs = (int)(targetRainDelayMs * frameStep / _options.AnimationSpeedMultiplier);
                frames.Add(new MatrixFrame(content, delayMs));
            }
        }

        return frames;
    }

    /// <summary>
    ///     Generate Matrix rain frames without a source image (pure rain effect).
    /// </summary>
    public List<MatrixFrame> GenerateRainFrames(int width, int height, int frameCount, int delayMs = 50)
    {
        var frames = new List<MatrixFrame>();
        _state.Initialize(width, height, _random, _matrixOptions);

        // No source image - all cells are equally "active"
        // Use base color for all cells in full-color mode
        var brightness = new float[width * height];
        var colors = new Rgba32[width * height];
        var edges = new float[width * height]; // No edges for pure rain
        Array.Fill(brightness, 0.5f);
        var defaultColor = _matrixOptions.BaseColor ?? new Rgba32(0, 255, 0, 255);
        Array.Fill(colors, defaultColor);

        for (var i = 0; i < frameCount; i++)
        {
            _state.Advance(_random, _matrixOptions, edges);
            var content = RenderFrame(width, height, brightness, colors, edges);
            frames.Add(new MatrixFrame(content, delayMs));
        }

        return frames;
    }

    private (float[] brightness, Rgba32[] colors, float[] edges) SampleImageData(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var brightness = new float[width * height];
        var colors = new Rgba32[width * height];
        var edges = new float[width * height];

        // First pass: sample brightness and colors
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var offset = y * width;
                for (var x = 0; x < row.Length; x++)
                {
                    brightness[offset + x] = BrightnessHelper.GetBrightness(row[x]);
                    colors[offset + x] = row[x];
                }
            }
        });

        // Second pass: compute edge strength using Sobel operator
        if (_matrixOptions.EnableEdgeDetection) ComputeEdgeStrength(brightness, edges, width, height);

        return (brightness, colors, edges);
    }

    /// <summary>
    ///     Compute edge strength using Sobel operator.
    ///     This detects where brightness changes rapidly (edges in the image).
    ///     Returns primarily HORIZONTAL edges (where rain would "collect" like on shoulders).
    /// </summary>
    private static void ComputeEdgeStrength(float[] brightness, float[] edges, int width, int height)
    {
        // Sobel kernels
        // Gx: [-1, 0, 1]    Gy: [-1, -2, -1]  (detects horizontal edges - bright above, dark below)
        //     [-2, 0, 2]        [ 0,  0,  0]
        //     [-1, 0, 1]        [ 1,  2,  1]

        for (var y = 1; y < height - 1; y++)
        for (var x = 1; x < width - 1; x++)
        {
            // Sample 3x3 neighborhood
            var tl = brightness[(y - 1) * width + (x - 1)];
            var tc = brightness[(y - 1) * width + x];
            var tr = brightness[(y - 1) * width + x + 1];
            var ml = brightness[y * width + (x - 1)];
            var mr = brightness[y * width + x + 1];
            var bl = brightness[(y + 1) * width + (x - 1)];
            var bc = brightness[(y + 1) * width + x];
            var br = brightness[(y + 1) * width + x + 1];

            // Apply Sobel kernels
            var gx = -tl + tr - 2 * ml + 2 * mr - bl + br; // Vertical edges
            var gy = -tl - 2 * tc - tr + bl + 2 * bc + br; // Horizontal edges

            // For rain "collecting", we care MORE about horizontal edges
            // (where there's a transition from bright above to dark below - like shoulders/ledges)
            // A positive gy means brighter above, darker below (a "ledge" for rain to collect)
            // Horizontal edge strength (rain collects on these)
            var horizontalEdge = Math.Max(0, gy); // Only care about bright-above-dark-below

            // Also include some vertical edge for shape definition
            var verticalEdge = MathF.Abs(gx);

            // Combined: prioritize horizontal edges but include vertical for shape
            // Horizontal edges get 2x weight (where rain collects)
            var combined = horizontalEdge * 2.0f + verticalEdge * 0.5f;

            edges[y * width + x] = Math.Min(1.0f, combined);
        }

        // Handle borders (copy from adjacent cells)
        for (var x = 0; x < width; x++)
        {
            edges[x] = edges[width + x]; // Top row
            edges[(height - 1) * width + x] = edges[(height - 2) * width + x]; // Bottom row
        }

        for (var y = 0; y < height; y++)
        {
            edges[y * width] = edges[y * width + 1]; // Left column
            edges[y * width + (width - 1)] = edges[y * width + (width - 2)]; // Right column
        }
    }

    private string RenderFrame(int width, int height, float[]? brightness, Rgba32[]? sourceColors, float[]? edges)
    {
        // Pre-size StringBuilder
        var estimatedSize = width * height * 25 + height * 10;
        var sb = new StringBuilder(estimatedSize);

        Rgba32? lastColor = null;

        // For pure rain mode (no image), use default values
        var isPureRain = brightness == null;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = y * width + x;
                var column = _state.Columns[x];

                // Get character and color for this cell
                // For pure rain: use 0.5 brightness (neutral), base color (or random color in fullcolor mode), no edges
                var cellBrightness = isPureRain ? 0.5f : brightness![idx];
                Rgba32 cellColor;
                if (isPureRain)
                {
                    // In fullcolor mode without image: use random per-column colors
                    // Otherwise: use the base color (default green)
                    cellColor = _matrixOptions.UseFullColor
                        ? column.RandomColor
                        : (_matrixOptions.BaseColor ?? new Rgba32(0, 255, 0, 255));
                }
                else
                {
                    cellColor = sourceColors![idx];
                }
                var cellEdge = isPureRain ? 0f : edges![idx];
                var (character, color) = GetCellDisplay(column, y, cellBrightness, cellColor, cellEdge);

                // Output color change if needed
                if (_options.UseColor)
                    if (lastColor == null || !ColorsEqual(lastColor.Value, color))
                    {
                        sb.Append("\x1b[38;2;");
                        sb.Append(color.R);
                        sb.Append(';');
                        sb.Append(color.G);
                        sb.Append(';');
                        sb.Append(color.B);
                        sb.Append('m');
                        lastColor = color;
                    }

                sb.Append(character);
            }

            // Reset at end of line
            if (_options.UseColor)
            {
                sb.Append("\x1b[0m");
                lastColor = null;
            }

            if (y < height - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (char character, Rgba32 color) GetCellDisplay(MatrixColumn column, int y, float imageBrightness,
        Rgba32 sourceColor, float edgeStrength)
    {
        // Calculate persistence factor based on edge strength and brightness
        // Edges and bright areas cause the rain to "stick" and glow longer
        var persistence = 0f;
        if (_matrixOptions.EnableEdgeDetection) persistence += edgeStrength * _matrixOptions.EdgePersistence;
        if (_matrixOptions.EnableBrightnessPersistence)
            persistence += imageBrightness * _matrixOptions.BrightnessPersistence;
        persistence = Math.Min(1.0f, persistence);

        // Check each raindrop in this column
        foreach (var drop in column.Drops)
        {
            var dropY = (int)drop.Position;

            // Edge persistence extends trail length dynamically
            var effectiveLength = drop.Length;
            if (persistence > 0.1f)
                // Trails are longer at edges/bright areas (phosphor persistence effect)
                effectiveLength = (int)(drop.Length * (1.0f + persistence * 0.8f));

            var tailEnd = dropY - effectiveLength;

            if (y > dropY || y <= tailEnd)
                continue;

            // Cell is within this raindrop
            var distFromHead = dropY - y;
            var normalizedDist = (float)distFromHead / effectiveLength;

            // Modulate by image brightness + edge strength (edges glow brighter)
            var brightnessMod = 0.5f + imageBrightness * 0.3f;

            // EDGE FLASH EFFECT: When rain passes over an edge, make it dramatically brighter
            // This "reveals" the image as rain cascades over the shape
            if (edgeStrength > 0.15f)
            {
                // Strong brightness boost at edges - the "read" effect
                var edgeBoost = edgeStrength * 0.8f;

                // Head of raindrop on edge gets EXTRA flash (the "phosphor burst")
                if (distFromHead <= 2) edgeBoost *= 1.5f; // 50% brighter flash at head crossing edge

                brightnessMod += edgeBoost;
            }

            if (persistence > 0.3f)
                // Extra brightness at persistent areas (the "phosphor glow" effect)
                brightnessMod += persistence * 0.4f;
            brightnessMod = Math.Min(2.0f, brightnessMod); // Allow higher max for dramatic edge reveals

            // Get character (changes randomly based on drop state)
            var c = GetCharacterForCell(column, y, drop);

            // Calculate color based on mode and position in trail
            var color = CalculateRainColor(distFromHead, normalizedDist, imageBrightness, sourceColor);

            // Apply brightness modulation
            color = ModulateColor(color, brightnessMod);

            return (c, color);
        }

        // Not in any raindrop - check for persistence glow (residual phosphor effect)
        // At edges and bright areas, show a lingering character even when no rain is present
        var bgThreshold = _matrixOptions.BackgroundThreshold;
        var bgBrightness = _matrixOptions.BackgroundBrightness;

        // Show background if brightness OR edge strength exceeds threshold
        var effectiveVisibility = Math.Max(imageBrightness, edgeStrength * 0.8f);

        if (effectiveVisibility > bgThreshold)
        {
            // Show a dim character based on image brightness and edge strength
            var c = GetBackgroundChar(column, y, effectiveVisibility);

            // Color: dim version of the base color, modulated by image
            Rgba32 baseColor;
            if (_matrixOptions.UseFullColor)
                baseColor = sourceColor;
            else
                baseColor = _matrixOptions.BaseColor ?? new Rgba32(0, 255, 65, 255);

            // Calculate dim factor - edges get MORE brightness for clear shape definition
            var dimFactor = effectiveVisibility * bgBrightness;

            // Edges get significant extra brightness to define the shape clearly
            if (edgeStrength > 0.2f)
                // Strong edge glow - this is what "holds" the shape visible
                dimFactor += edgeStrength * 0.4f;

            dimFactor = Math.Min(0.8f, dimFactor); // Allow brighter backgrounds for edge definition

            var dimColor = new Rgba32(
                (byte)(baseColor.R * dimFactor),
                (byte)(baseColor.G * dimFactor),
                (byte)(baseColor.B * dimFactor),
                255
            );

            return (c, dimColor);
        }

        // Very dark area - empty cell
        return (' ', BackgroundColor);
    }

    /// <summary>
    ///     Get a character for background areas (showing through the rain).
    ///     Uses simpler characters for the "residue" effect.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char GetBackgroundChar(MatrixColumn column, int y, float brightness)
    {
        // Use brightness to select character density
        // Brighter = denser character
        if (brightness < 0.2f) return '.';
        if (brightness < 0.4f) return ':';
        if (brightness < 0.6f) return _matrixCharacters[(column.CharacterSeed + y) % _matrixCharacters.Length];
        return _matrixCharacters[(column.CharacterSeed + y * 3) % _matrixCharacters.Length];
    }

    /// <summary>
    ///     Calculate the color for a rain cell based on mode and position.
    ///     Uses authentic Matrix color scheme: white head -> bright green -> dark green
    ///     Based on analysis from https://carlnewton.github.io/digital-rain-analysis/
    ///
    ///     In full-color/reveal mode: dark areas use base color (green), bright/colored
    ///     areas "reveal" the source image by blending toward source color based on brightness.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Rgba32 CalculateRainColor(int distFromHead, float normalizedDist, float imageBrightness, Rgba32 sourceColor)
    {
        // Base color is always used as the starting point
        var matrixBaseColor = _matrixOptions.BaseColor ?? new Rgba32(0, 255, 65, 255); // #00FF41 Malachite

        // Determine effective color based on mode
        Rgba32 baseColor;
        if (_matrixOptions.UseFullColor)
        {
            // Full color "reveal" mode: blend from base color to source color based on brightness
            // Dark areas (black) = show normal matrix rain (base color)
            // Bright/colored areas = reveal the source image color
            //
            // Use a power curve to make the reveal more dramatic - bright areas pop more
            var revealFactor = MathF.Pow(imageBrightness, 0.7f); // Power < 1 makes brights reveal faster

            // Also consider source color saturation - colorful areas should reveal more
            var srcMax = Math.Max(sourceColor.R, Math.Max(sourceColor.G, sourceColor.B));
            var srcMin = Math.Min(sourceColor.R, Math.Min(sourceColor.G, sourceColor.B));
            var saturation = srcMax > 0 ? (srcMax - srcMin) / (float)srcMax : 0;

            // Boost reveal factor for saturated colors
            revealFactor = Math.Min(1.0f, revealFactor + saturation * 0.3f);

            // Blend between base color and source color
            baseColor = new Rgba32(
                (byte)(matrixBaseColor.R + (sourceColor.R - matrixBaseColor.R) * revealFactor),
                (byte)(matrixBaseColor.G + (sourceColor.G - matrixBaseColor.G) * revealFactor),
                (byte)(matrixBaseColor.B + (sourceColor.B - matrixBaseColor.B) * revealFactor),
                255
            );
        }
        else
        {
            // Single color mode - use configured base color
            baseColor = matrixBaseColor;
        }

        // Authentic Matrix color scheme:
        // Head: White/cyan glow (the "phosphor burst")
        // Near head: Bright color (#00FF41 for green)
        // Mid trail: Medium color (#008F11 for green)
        // Trail end: Dark color (#003B00 for green)

        // Calculate color multipliers based on base color's dominant channel
        float maxChannel = Math.Max(baseColor.R, Math.Max(baseColor.G, baseColor.B));
        var rRatio = maxChannel > 0 ? baseColor.R / maxChannel : 0;
        var gRatio = maxChannel > 0 ? baseColor.G / maxChannel : 0;
        var bRatio = maxChannel > 0 ? baseColor.B / maxChannel : 0;

        // Head color - WHITE with slight tint of base color (the phosphor burst)
        var headColor = new Rgba32(
            (byte)Math.Min(255, 200 + 55 * rRatio),
            (byte)Math.Min(255, 255), // Always bright
            (byte)Math.Min(255, 200 + 55 * bRatio),
            255
        );

        // Near-head color - Very bright, almost white with color tint
        var nearHeadColor = new Rgba32(
            (byte)Math.Min(255, 150 + baseColor.R * 0.4f),
            (byte)Math.Min(255, 220 + baseColor.G * 0.14f),
            (byte)Math.Min(255, 150 + baseColor.B * 0.4f),
            255
        );

        // Bright trail color - Full saturation of base color
        var brightColor = new Rgba32(
            baseColor.R,
            baseColor.G,
            baseColor.B,
            255
        );

        // Mid trail color - Reduced brightness
        var midColor = new Rgba32(
            (byte)(baseColor.R * 0.56f),
            (byte)(baseColor.G * 0.56f),
            (byte)(baseColor.B * 0.56f),
            255
        );

        // Dark trail color - Very dim
        var darkColor = new Rgba32(
            (byte)(baseColor.R * 0.23f),
            (byte)(baseColor.G * 0.23f),
            (byte)(baseColor.B * 0.23f),
            255
        );

        // Calculate final color based on position in trail
        if (distFromHead == 0)
            // Head of raindrop - WHITE/cyan glow
            return headColor;

        if (distFromHead == 1)
            // Just after head - transition from white to near-head bright
            return nearHeadColor;

        if (distFromHead <= 3)
        {
            // Near head zone - bright with slight fade
            var t = (distFromHead - 1) / 2.0f;
            return InterpolateColor(nearHeadColor, brightColor, t);
        }

        if (normalizedDist < 0.5f)
        {
            // Upper trail - fade from bright to mid
            var t = normalizedDist * 2;
            return InterpolateColor(brightColor, midColor, t);
        }
        else
        {
            // Lower trail - fade from mid to dark
            var t = (normalizedDist - 0.5f) * 2;
            return InterpolateColor(midColor, darkColor, t);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char GetCharacterForCell(MatrixColumn column, int y, MatrixDrop drop)
    {
        // Characters change randomly - use position + tick for pseudo-random
        var charIndex = (column.CharacterSeed + y + drop.Tick) % _matrixCharacters.Length;
        return _matrixCharacters[Math.Abs(charIndex)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Rgba32 InterpolateColor(Rgba32 from, Rgba32 to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgba32(
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t),
            255
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Rgba32 ModulateColor(Rgba32 color, float factor)
    {
        return new Rgba32(
            (byte)Math.Clamp(color.R * factor, 0, 255),
            (byte)Math.Clamp(color.G * factor, 0, 255),
            (byte)Math.Clamp(color.B * factor, 0, 255),
            255
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ColorsEqual(Rgba32 a, Rgba32 b)
    {
        return a.R == b.R && a.G == b.G && a.B == b.B;
    }

    private (int width, int height) CalculateMatrixDimensions(int imageWidth, int imageHeight)
    {
        // Matrix uses 1:1 character cells (like ASCII mode)
        return _options.CalculateVisualDimensions(imageWidth, imageHeight, 1, 1);
    }
}

/// <summary>
///     Internal state for Matrix rain animation.
/// </summary>
internal class MatrixRainState
{
    private int _tick;
    public bool IsInitialized { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public MatrixColumn[] Columns { get; private set; } = Array.Empty<MatrixColumn>();

    public void Initialize(int width, int height, Random random, MatrixOptions options)
    {
        Width = width;
        Height = height;
        Columns = new MatrixColumn[width];

        var density = options.Density;

        for (var x = 0; x < width; x++)
        {
            // Generate column speed multiplier for authentic Matrix effect
            // Some columns are noticeably faster (1.5x) or slower (0.5x)
            // Distribution: 15% fast, 15% slow, 70% normal
            float speedMult;
            var speedRoll = random.NextDouble();
            if (speedRoll < 0.15)
                speedMult = 1.3f + (float)random.NextDouble() * 0.4f; // Fast: 1.3-1.7x
            else if (speedRoll < 0.30)
                speedMult = 0.4f + (float)random.NextDouble() * 0.3f; // Slow: 0.4-0.7x
            else
                speedMult = 0.85f + (float)random.NextDouble() * 0.3f; // Normal: 0.85-1.15x

            // Generate random color for pure rain fullcolor mode
            var randomColor = new Rgba32(
                (byte)random.Next(50, 256),
                (byte)random.Next(50, 256),
                (byte)random.Next(50, 256),
                255
            );

            Columns[x] = new MatrixColumn
            {
                CharacterSeed = random.Next(1000),
                Drops = new List<MatrixDrop>(),
                SpeedMultiplier = speedMult,
                RandomColor = randomColor
            };

            // Start with some raindrops already in progress (staggered)
            var initialDrops = random.Next(1, 3);
            for (var i = 0; i < initialDrops; i++)
                if (random.NextDouble() < 0.7 * density) // Density affects initial spawn
                    Columns[x].Drops.Add(CreateNewDrop(random, height, false, options));
        }

        IsInitialized = true;
        _tick = 0;
    }

    public void Advance(Random random, MatrixOptions options, float[]? edges = null)
    {
        _tick++;

        var speedMult = options.SpeedMultiplier;
        var density = options.Density;
        var flickerRate = options.CharacterFlickerRate;
        var useEdgeSlowdown = options.EnableEdgeDetection && edges != null;

        for (var x = 0; x < Width; x++)
        {
            var column = Columns[x];

            // Move all drops down
            for (var i = column.Drops.Count - 1; i >= 0; i--)
            {
                var drop = column.Drops[i];

                // Calculate effective speed - include column speed multiplier for authentic Matrix effect
                var effectiveSpeed = drop.Speed * speedMult * column.SpeedMultiplier;

                if (useEdgeSlowdown)
                {
                    // Sample edge strength at drop head position
                    var dropY = Math.Clamp((int)drop.Position, 0, Height - 1);
                    var edgeStrength = edges![dropY * Width + x];

                    // Slow down significantly at edges (accumulation effect)
                    // Strong edges can slow rain to 20% of normal speed
                    if (edgeStrength > 0.2f)
                    {
                        var slowdown = 1.0f - edgeStrength * options.EdgePersistence * 0.8f;
                        effectiveSpeed *= Math.Max(0.2f, slowdown);
                    }
                }

                drop.Position += effectiveSpeed;
                drop.Tick++;

                // Remove drops that have completely fallen off screen
                if (drop.Position - drop.Length > Height + 5) column.Drops.RemoveAt(i);
            }

            // Randomly spawn new drops at top
            // Chance increases when column has fewer active drops
            // Density affects spawn rate
            var baseSpawnChance = column.Drops.Count == 0 ? 0.15f : 0.03f;
            var spawnChance = baseSpawnChance * density * 2f;
            if (random.NextDouble() < spawnChance) column.Drops.Add(CreateNewDrop(random, Height, true, options));

            // Randomly change character seed (makes characters "flicker")
            if (random.NextDouble() < flickerRate) column.CharacterSeed = random.Next(1000);
        }
    }

    private static MatrixDrop CreateNewDrop(Random random, int height, bool startAtTop, MatrixOptions options)
    {
        var trailMult = options.TrailLengthMultiplier;
        var baseLength = random.Next(5, Math.Max(6, height / 2));
        var adjustedLength = Math.Max(3, (int)(baseLength * trailMult));

        return new MatrixDrop
        {
            Position = startAtTop ? -random.Next(0, height / 4) : random.Next(0, height),
            Speed = 0.3f + (float)random.NextDouble() * 0.7f, // 0.3 to 1.0
            Length = adjustedLength,
            Tick = 0
        };
    }
}

/// <summary>
///     State for a single column in the Matrix rain.
/// </summary>
internal class MatrixColumn
{
    public int CharacterSeed { get; set; }
    public List<MatrixDrop> Drops { get; set; } = new();

    /// <summary>
    /// Column speed multiplier - some columns are faster/slower for authentic Matrix effect.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Random color for this column (used in fullcolor pure rain mode).
    /// </summary>
    public Rgba32 RandomColor { get; set; }
}

/// <summary>
///     A single raindrop in a Matrix column.
/// </summary>
internal class MatrixDrop
{
    public float Position { get; set; } // Y position of the head
    public float Speed { get; set; } // Cells per tick
    public int Length { get; set; } // Tail length
    public int Tick { get; set; } // For character animation
}

/// <summary>
///     A single frame of Matrix-rendered content.
/// </summary>
public class MatrixFrame : IAnimationFrame
{
    public MatrixFrame(string content, int delayMs)
    {
        Content = content;
        DelayMs = delayMs;
    }

    public string Content { get; }
    public int DelayMs { get; }

    public override string ToString()
    {
        return Content;
    }
}