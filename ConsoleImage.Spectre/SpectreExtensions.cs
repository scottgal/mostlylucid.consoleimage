using ConsoleImage.Core;
using Spectre.Console;
using Spectre.Console.Rendering;
using CoreRenderOptions = ConsoleImage.Core.RenderOptions;
using SpectreRenderOptions = Spectre.Console.Rendering.RenderOptions;

namespace ConsoleImage.Spectre;

/// <summary>
/// Factory for creating ConsoleImage renderables with a unified API.
/// </summary>
/// <remarks>
/// <para>
/// Use this factory when you need to create images dynamically based on a mode selection,
/// or when you want a consistent way to create any type of ConsoleImage renderable.
/// </para>
/// <para>
/// <b>Example - Create image based on user selection:</b>
/// <code>
/// AnimationMode mode = GetUserSelection(); // Ascii, ColorBlock, Braille, or Matrix
/// var image = ConsoleImageFactory.CreateImage("photo.jpg", mode);
/// AnsiConsole.Write(image);
/// </code>
/// </para>
/// <para>
/// <b>Example - Create animation:</b>
/// <code>
/// var animation = ConsoleImageFactory.CreateAnimation("cat.gif", AnimationMode.Braille);
/// await AnsiConsole.Live(animation).StartAsync(async ctx => { ... });
/// </code>
/// </para>
/// </remarks>
public static class ConsoleImageFactory
{
    /// <summary>
    /// Create a static image renderable in any mode.
    /// </summary>
    /// <param name="filePath">Path to the image file (JPG, PNG, GIF, WebP, etc.)</param>
    /// <param name="mode">Render mode: ASCII, ColorBlock, Braille, or Matrix</param>
    /// <param name="options">Render options (dimensions, colors, contrast, etc.)</param>
    /// <param name="matrixOptions">Matrix-specific options (only used when mode is Matrix)</param>
    /// <returns>An IRenderable that can be used with AnsiConsole.Write() or in Spectre layouts</returns>
    /// <example>
    /// <code>
    /// // Simple usage
    /// var image = ConsoleImageFactory.CreateImage("photo.jpg", AnimationMode.Braille);
    /// AnsiConsole.Write(image);
    ///
    /// // With options
    /// var opts = new RenderOptions { MaxWidth = 80, ContrastPower = 3.0f };
    /// var image = ConsoleImageFactory.CreateImage("photo.jpg", AnimationMode.ColorBlock, opts);
    /// </code>
    /// </example>
    public static IRenderable CreateImage(string filePath, AnimationMode mode, CoreRenderOptions? options = null, MatrixOptions? matrixOptions = null)
    {
        options ??= new CoreRenderOptions { UseColor = true };

        return mode switch
        {
            AnimationMode.Ascii => new AsciiImage(filePath, options),
            AnimationMode.ColorBlock => new ColorBlockImage(filePath, options),
            AnimationMode.Braille => new BrailleImage(filePath, options),
            AnimationMode.Matrix => new MatrixImage(filePath, options, matrixOptions),
            _ => new AsciiImage(filePath, options)
        };
    }

    /// <summary>
    /// Create an animated image renderable in any mode.
    /// </summary>
    /// <param name="filePath">Path to the image or GIF file</param>
    /// <param name="mode">Render mode for animation frames</param>
    /// <param name="options">Render options</param>
    /// <param name="matrixOptions">Matrix-specific options (only used when mode is Matrix)</param>
    /// <returns>An IAnimatedRenderable for use with Spectre's Live display</returns>
    /// <example>
    /// <code>
    /// var animation = ConsoleImageFactory.CreateAnimation("cat.gif", AnimationMode.Braille);
    ///
    /// // Play with extension method
    /// if (animation is AnimatedImage anim)
    ///     await anim.PlayAsync(cancellationToken);
    ///
    /// // Or manual control
    /// await AnsiConsole.Live(animation).StartAsync(async ctx => {
    ///     while (!token.IsCancellationRequested) {
    ///         animation.TryAdvanceFrame();
    ///         ctx.Refresh();
    ///         await Task.Delay(16);
    ///     }
    /// });
    /// </code>
    /// </example>
    public static IAnimatedRenderable CreateAnimation(string filePath, AnimationMode mode, CoreRenderOptions? options = null, MatrixOptions? matrixOptions = null)
    {
        options ??= new CoreRenderOptions { UseColor = true };

        return mode switch
        {
            AnimationMode.Matrix => new AnimatedMatrixImage(filePath, options, matrixOptions),
            _ => new AnimatedImage(filePath, mode, options)
        };
    }
}

/// <summary>
/// Interface for animated renderables that can advance frames.
/// </summary>
/// <remarks>
/// Implemented by <see cref="AnimatedImage"/> and <see cref="AnimatedMatrixImage"/>.
/// Use this interface for polymorphic animation handling.
/// </remarks>
public interface IAnimatedRenderable : IRenderable
{
    /// <summary>Current frame index (0-based).</summary>
    int CurrentFrame { get; }

    /// <summary>Total number of frames in the animation.</summary>
    int FrameCount { get; }

    /// <summary>
    /// Advance to next frame if enough time has elapsed based on frame delay.
    /// </summary>
    /// <returns>True if frame changed, false if not enough time elapsed</returns>
    bool TryAdvanceFrame();

    /// <summary>Reset animation to the first frame.</summary>
    void Reset();

    /// <summary>Jump to a specific frame.</summary>
    /// <param name="frameIndex">Zero-based frame index</param>
    void SetFrame(int frameIndex);
}

/// <summary>
/// Plays multiple animations simultaneously with different render modes.
/// </summary>
/// <remarks>
/// <para>
/// Use this class to display multiple GIFs or images side-by-side, each potentially
/// using a different render mode. Perfect for comparisons or dashboard-style displays.
/// </para>
/// <para>
/// <b>Example - Compare render modes:</b>
/// <code>
/// var player = new MultiAnimationPlayer()
///     .Add("cat.gif", AnimationMode.Ascii, "ASCII")
///     .Add("cat.gif", AnimationMode.Braille, "Braille")
///     .Add("cat.gif", AnimationMode.Matrix, "Matrix");
///
/// await player.PlayAsync(cancellationToken);
/// </code>
/// </para>
/// <para>
/// <b>Example - Multiple different files:</b>
/// <code>
/// var player = new MultiAnimationPlayer()
///     .Add("earth.gif", AnimationMode.ColorBlock)
///     .Add("cat.gif", AnimationMode.Braille)
///     .Add("fire.gif", AnimationMode.Matrix);
///
/// await player.PlayAsync(loopCount: 3);
/// </code>
/// </para>
/// </remarks>
public class MultiAnimationPlayer
{
    private readonly List<(IAnimatedRenderable Animation, string Label)> _animations = new();

    /// <summary>
    /// Add an animation from a file path.
    /// </summary>
    /// <param name="filePath">Path to image or GIF file</param>
    /// <param name="mode">Render mode for this animation</param>
    /// <param name="label">Display label (defaults to filename)</param>
    /// <param name="options">Render options for this animation</param>
    /// <returns>This player for fluent chaining</returns>
    public MultiAnimationPlayer Add(string filePath, AnimationMode mode, string? label = null, CoreRenderOptions? options = null)
    {
        var animation = ConsoleImageFactory.CreateAnimation(filePath, mode, options);
        _animations.Add((animation, label ?? Path.GetFileName(filePath)));
        return this;
    }

    /// <summary>
    /// Add a pre-created animation.
    /// </summary>
    /// <param name="animation">Pre-created animated renderable</param>
    /// <param name="label">Display label for the panel header</param>
    /// <returns>This player for fluent chaining</returns>
    public MultiAnimationPlayer Add(IAnimatedRenderable animation, string label)
    {
        _animations.Add((animation, label));
        return this;
    }

    /// <summary>
    /// Play all animations side by side using Spectre's Live display.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel playback</param>
    /// <param name="loopCount">Number of loops (0 = infinite)</param>
    /// <returns>Task that completes when playback ends</returns>
    public async Task PlayAsync(CancellationToken cancellationToken = default, int loopCount = 0)
    {
        if (_animations.Count == 0) return;

        int loops = 0;
        bool anyFrameZero = false;

        IRenderable CreateLayout()
        {
            var panels = new List<Panel>();
            foreach (var (anim, label) in _animations)
            {
                var header = anim.FrameCount > 1
                    ? $"[cyan]{Markup.Escape(label)}[/] [dim]{anim.CurrentFrame + 1}/{anim.FrameCount}[/]"
                    : $"[cyan]{Markup.Escape(label)}[/]";

                var panel = new Panel(anim)
                    .Header(header)
                    .Border(BoxBorder.Rounded)
                    .Expand();
                panels.Add(panel);
            }
            return new Columns(panels);
        }

        await AnsiConsole.Live(CreateLayout())
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Advance all animations
                    bool anyAtZero = false;
                    foreach (var (anim, _) in _animations)
                    {
                        anim.TryAdvanceFrame();
                        if (anim.CurrentFrame == 0) anyAtZero = true;
                    }

                    ctx.UpdateTarget(CreateLayout());
                    ctx.Refresh();

                    // Loop counting
                    if (anyAtZero && anyFrameZero)
                    {
                        loops++;
                        if (loopCount > 0 && loops >= loopCount)
                            break;
                    }
                    anyFrameZero = anyAtZero;

                    try
                    {
                        await Task.Delay(16, cancellationToken);
                    }
                    catch (OperationCanceledException) { break; }
                }
            });
    }
}

/// <summary>
/// Displays the same image in multiple render modes side-by-side for comparison.
/// </summary>
/// <remarks>
/// <para>
/// Perfect for demonstrating the differences between ASCII, ColorBlock, Braille,
/// and Matrix rendering modes.
/// </para>
/// <para>
/// <b>Example:</b>
/// <code>
/// // Compare specific modes
/// var comparison = RenderModeComparison.FromFile("photo.jpg", null,
///     AnimationMode.Ascii, AnimationMode.Braille);
/// AnsiConsole.Write(comparison);
///
/// // Or compare all four modes
/// AnsiConsole.Write(RenderModeComparison.AllModes("photo.jpg"));
/// </code>
/// </para>
/// </remarks>
public class RenderModeComparison : IRenderable
{
    private readonly List<(IRenderable Image, string Label)> _images = new();

    /// <summary>
    /// Create a comparison of the same file in specified render modes.
    /// </summary>
    /// <param name="filePath">Path to image file</param>
    /// <param name="options">Render options (MaxWidth is automatically reduced for side-by-side)</param>
    /// <param name="modes">Render modes to compare</param>
    /// <returns>A renderable comparison layout</returns>
    public static RenderModeComparison FromFile(string filePath, CoreRenderOptions? options = null, params AnimationMode[] modes)
    {
        var comparison = new RenderModeComparison();

        // Auto-adjust width for side-by-side display
        if (options == null)
        {
            options = new CoreRenderOptions { UseColor = true, MaxWidth = 40 };
        }
        else if (options.MaxWidth > 50)
        {
            options.MaxWidth = 40;
        }

        foreach (var mode in modes)
        {
            var image = ConsoleImageFactory.CreateImage(filePath, mode, options);
            comparison._images.Add((image, mode.ToString()));
        }

        return comparison;
    }

    /// <summary>
    /// Create a comparison showing all four render modes (ASCII, ColorBlock, Braille, Matrix).
    /// </summary>
    /// <param name="filePath">Path to image file</param>
    /// <param name="options">Render options</param>
    /// <returns>A renderable comparison layout with all modes</returns>
    public static RenderModeComparison AllModes(string filePath, CoreRenderOptions? options = null)
    {
        return FromFile(filePath, options, AnimationMode.Ascii, AnimationMode.ColorBlock, AnimationMode.Braille, AnimationMode.Matrix);
    }

    /// <inheritdoc/>
    public Measurement Measure(SpectreRenderOptions options, int maxWidth)
    {
        return new Measurement(maxWidth, maxWidth);
    }

    /// <inheritdoc/>
    public IEnumerable<Segment> Render(SpectreRenderOptions options, int maxWidth)
    {
        var panels = _images.Select(x => new Panel(x.Image)
            .Header($"[cyan]{x.Label}[/]")
            .Border(BoxBorder.Rounded)
            .Expand()).ToList();

        var columns = new Columns(panels);
        return ((IRenderable)columns).Render(options, maxWidth);
    }
}

/// <summary>
/// Extension methods for AnsiConsole to easily display ConsoleImage content.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide a fluent, convenient API for displaying images
/// without needing to manually create renderable instances.
/// </para>
/// <para>
/// <b>Quick display:</b>
/// <code>
/// AnsiConsole.WriteImage("photo.jpg", AnimationMode.Braille);
/// </code>
/// </para>
/// <para>
/// <b>Compare modes:</b>
/// <code>
/// AnsiConsole.WriteComparison("photo.jpg");
/// </code>
/// </para>
/// <para>
/// <b>Play animation:</b>
/// <code>
/// await AnsiConsole.PlayAnimationAsync("cat.gif", AnimationMode.ColorBlock);
/// </code>
/// </para>
/// </remarks>
public static class AnsiConsoleExtensions
{
    /// <summary>
    /// Display an image file in the specified render mode.
    /// </summary>
    /// <param name="console">The AnsiConsole instance</param>
    /// <param name="filePath">Path to image file</param>
    /// <param name="mode">Render mode (default: ASCII)</param>
    /// <param name="options">Render options</param>
    /// <example>
    /// <code>
    /// AnsiConsole.WriteImage("photo.jpg");
    /// AnsiConsole.WriteImage("photo.jpg", AnimationMode.Braille);
    /// AnsiConsole.WriteImage("photo.jpg", AnimationMode.Matrix, new RenderOptions { MaxWidth = 100 });
    /// </code>
    /// </example>
    public static void WriteImage(this IAnsiConsole console, string filePath, AnimationMode mode = AnimationMode.Ascii, CoreRenderOptions? options = null)
    {
        var image = ConsoleImageFactory.CreateImage(filePath, mode, options);
        console.Write(image);
    }

    /// <summary>
    /// Display an image in a panel with optional header.
    /// </summary>
    /// <param name="console">The AnsiConsole instance</param>
    /// <param name="filePath">Path to image file</param>
    /// <param name="mode">Render mode</param>
    /// <param name="header">Optional panel header text</param>
    /// <param name="options">Render options</param>
    /// <example>
    /// <code>
    /// AnsiConsole.WriteImagePanel("photo.jpg", AnimationMode.Braille, "My Photo");
    /// </code>
    /// </example>
    public static void WriteImagePanel(this IAnsiConsole console, string filePath, AnimationMode mode = AnimationMode.Ascii, string? header = null, CoreRenderOptions? options = null)
    {
        var image = ConsoleImageFactory.CreateImage(filePath, mode, options);
        var panel = new Panel(image).Border(BoxBorder.Rounded);

        if (header != null)
            panel.Header(header);

        console.Write(panel);
    }

    /// <summary>
    /// Display a comparison of the same image in multiple render modes.
    /// </summary>
    /// <param name="console">The AnsiConsole instance</param>
    /// <param name="filePath">Path to image file</param>
    /// <param name="options">Render options</param>
    /// <param name="modes">Modes to compare (default: ASCII, ColorBlock, Braille)</param>
    /// <example>
    /// <code>
    /// // Default comparison (3 modes)
    /// AnsiConsole.WriteComparison("photo.jpg");
    ///
    /// // Specific modes
    /// AnsiConsole.WriteComparison("photo.jpg", null, AnimationMode.Ascii, AnimationMode.Matrix);
    /// </code>
    /// </example>
    public static void WriteComparison(this IAnsiConsole console, string filePath, CoreRenderOptions? options = null, params AnimationMode[] modes)
    {
        if (modes.Length == 0)
            modes = new[] { AnimationMode.Ascii, AnimationMode.ColorBlock, AnimationMode.Braille };

        var comparison = RenderModeComparison.FromFile(filePath, options, modes);
        console.Write(comparison);
    }

    /// <summary>
    /// Play an animated GIF or apply animation effect to a static image.
    /// </summary>
    /// <param name="console">The AnsiConsole instance</param>
    /// <param name="filePath">Path to image or GIF file</param>
    /// <param name="mode">Render mode</param>
    /// <param name="options">Render options</param>
    /// <param name="cancellationToken">Token to cancel playback</param>
    /// <example>
    /// <code>
    /// using var cts = new CancellationTokenSource();
    /// Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
    ///
    /// await AnsiConsole.PlayAnimationAsync("cat.gif", AnimationMode.Braille, cancellationToken: cts.Token);
    /// </code>
    /// </example>
    public static async Task PlayAnimationAsync(this IAnsiConsole console, string filePath, AnimationMode mode = AnimationMode.Ascii, CoreRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var animation = ConsoleImageFactory.CreateAnimation(filePath, mode, options);

        await AnsiConsole.Live(animation)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    animation.TryAdvanceFrame();
                    ctx.Refresh();

                    try { await Task.Delay(16, cancellationToken); }
                    catch (OperationCanceledException) { break; }
                }
            });
    }

    /// <summary>
    /// Create a multi-animation player for playing multiple animations simultaneously.
    /// </summary>
    /// <param name="console">The AnsiConsole instance</param>
    /// <returns>A fluent builder for adding animations</returns>
    /// <example>
    /// <code>
    /// await AnsiConsole.CreateMultiAnimation()
    ///     .Add("a.gif", AnimationMode.Ascii, "ASCII")
    ///     .Add("b.gif", AnimationMode.Braille, "Braille")
    ///     .Add("c.gif", AnimationMode.Matrix, "Matrix")
    ///     .PlayAsync(cancellationToken);
    /// </code>
    /// </example>
    public static MultiAnimationPlayer CreateMultiAnimation(this IAnsiConsole console)
    {
        return new MultiAnimationPlayer();
    }
}

// Partial class declarations to implement IAnimatedRenderable
public partial class AnimatedImage : IAnimatedRenderable { }
public partial class AnimatedMatrixImage : IAnimatedRenderable { }
